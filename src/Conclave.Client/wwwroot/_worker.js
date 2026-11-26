// Conclave — Cloudflare Pages advanced-mode worker (no npm dependencies).
//   /api/*  → meetings, LiveKit tokens, recording seals, public verification
//   else    → static Blazor WebAssembly assets (SPA fallback to /index.html)
// Secrets (Pages → Settings → Variables and secrets): DATABASE_URL, LIVEKIT_URL, LIVEKIT_API_KEY, LIVEKIT_API_SECRET, AXOWL_API_KEY
// Optional: AXOWL_API_BASE (defaults to AXOWL_API below), SEAL_RETRY_DELAY_MS.

const AXOWL_API = 'https://testapi.axowl.com';
const AXOWL_ORG = 'hackertones';
const AXOWL_CLIENT = 'client_6f8eb765181d45ff938e';
const JWKS_URL = `${AXOWL_API}/api/public/orgs/${AXOWL_ORG}/.well-known/jwks.json`;

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    if (url.pathname.startsWith('/api/')) {
      try {
        return await api(request, url, env);
      } catch (e) {
        const status = (e && e.status) || 500;
        return json({ error: status === 401 ? 'unauthorized' : 'internal', detail: String(e && e.message || e) }, status);
      }
    }
    const res = await env.ASSETS.fetch(request);
    if (res.status === 404 && request.method === 'GET' && (request.headers.get('accept') || '').includes('text/html')) {
      return env.ASSETS.fetch(new Request(new URL('/index.html', url).toString(), request));
    }
    return res;
  },
};

// ───────────────────────── routing ─────────────────────────
async function api(request, url, env) {
  const p = url.pathname.replace(/\/+$/, '');
  const m = request.method;
  if (m === 'OPTIONS') return new Response(null, { status: 204 });

  if (p === '/api/health') return json({ ok: true, time: new Date().toISOString() });
  if (p === '/api/health/db') { await ensureSchema(env); const r = await sql(env, 'select now() as now'); return json({ ok: true, now: r[0].now }); }

  if (p === '/api/meetings' && m === 'POST') {
    const user = await requireUser(request);
    const body = await request.json().catch(() => ({}));
    const title = String(body.title || '제목 없는 회의').slice(0, 120);
    const id = shortId();
    await ensureSchema(env);
    await sql(env, 'insert into conclave_meetings (id, title, host_sub, host_name) values ($1,$2,$3,$4)', [id, title, user.sub, user.name]);
    return json({ id, title, hostName: user.name, joinUrl: `${url.origin}/m/${id}` }, 201);
  }
  if (p === '/api/meetings' && m === 'GET') {
    const user = await requireUser(request);
    await ensureSchema(env);
    const rows = await sql(env, 'select id, title, host_name, created_at, started_at, ended_at, seal_id, sealed_at from conclave_meetings where host_sub=$1 order by created_at desc limit 50', [user.sub]);
    return json({ meetings: rows.map(pub) });
  }

  let mm = p.match(/^\/api\/meetings\/([A-Za-z0-9_-]{4,40})$/);
  if (mm && m === 'GET') {
    await ensureSchema(env);
    const rows = await sql(env, 'select * from conclave_meetings where id=$1', [mm[1]]);
    if (!rows.length) return json({ error: 'not_found' }, 404);
    return json(pub(rows[0]));
  }

  mm = p.match(/^\/api\/meetings\/([A-Za-z0-9_-]{4,40})\/token$/);
  if (mm && m === 'POST') {
    await ensureSchema(env);
    const rows = await sql(env, 'select * from conclave_meetings where id=$1', [mm[1]]);
    if (!rows.length) return json({ error: 'not_found' }, 404);
    const meeting = rows[0];
    if (meeting.ended_at) return json({ error: 'ended', detail: '이 회의는 끝났습니다.' }, 409);
    const body = await request.json().catch(() => ({}));
    const user = await optionalUser(request);
    const isHost = !!user && user.sub === meeting.host_sub;
    const name = String((user && user.name) || body.name || '참가자').slice(0, 40);
    const identity = `${isHost ? 'host' : 'guest'}-${name}-${shortId(4)}`;
    if (isHost && !meeting.started_at) await sql(env, 'update conclave_meetings set started_at=now() where id=$1 and started_at is null', [meeting.id]);
    const token = await livekitToken(env, { identity, name, room: meeting.id, host: isHost });
    return json({ token, url: env.LIVEKIT_URL, identity, name, isHost, room: meeting.id });
  }

  mm = p.match(/^\/api\/meetings\/([A-Za-z0-9_-]{4,40})\/end$/);
  if (mm && m === 'POST') {
    const user = await requireUser(request);
    const rows = await sql(env, 'update conclave_meetings set ended_at=coalesce(ended_at, now()) where id=$1 and host_sub=$2 returning *', [mm[1], user.sub]);
    if (!rows.length) return json({ error: 'not_found_or_not_host' }, 404);
    return json(pub(rows[0]));
  }

  mm = p.match(/^\/api\/meetings\/([A-Za-z0-9_-]{4,40})\/seal$/);
  if (mm && m === 'POST') {
    const user = await requireUser(request);
    const body = await request.json().catch(() => ({}));
    const sha = String(body.sha256 || '').toLowerCase();
    if (!/^[0-9a-f]{64}$/.test(sha)) return json({ error: 'bad_sha256' }, 400);
    const bytes = Number(body.bytes || 0), durationMs = Number(body.durationMs || 0);
    if (!Number.isFinite(bytes) || bytes < 0 || !Number.isFinite(durationMs) || durationMs < 0) return json({ error: 'bad_number' }, 400);
    return sealRecording(env, url.origin, user, mm[1], { sha256: sha, bytes: Math.floor(bytes), durationMs: Math.floor(durationMs) });
  }

  mm = p.match(/^\/api\/verify\/([A-Za-z0-9_-]{4,40})$/);
  if (mm && m === 'GET') {
    await ensureSchema(env);
    const rows = await sql(env, 'select * from conclave_meetings where seal_id=$1', [mm[1]]);
    if (!rows.length) return json({ error: 'not_found' }, 404);
    const r = rows[0];
    return json({ sealId: r.seal_id, meetingId: r.id, title: r.title, hostName: r.host_name, sha256: r.recording_sha256,
      bytes: Number(r.recording_bytes || 0), durationMs: Number(r.recording_ms || 0), startedAt: iso(r.started_at), endedAt: iso(r.ended_at),
      sealedAt: iso(r.sealed_at), axowlEventId: r.axowl_event_id, ledger: 'axowl', org: AXOWL_ORG });
  }

  return json({ error: 'not_found' }, 404);
}

// Neon HTTP returns timestamptz as '2026-11-09 03:20:01.12+00' — .NET wants ISO-8601, so normalize here.
function iso(v) { if (!v) return null; const d = new Date(String(v).replace(' ', 'T').replace(/\+00$/, 'Z')); return isNaN(d) ? null : d.toISOString(); }
function pub(r) {
  return { id: r.id, title: r.title, hostName: r.host_name, createdAt: iso(r.created_at), startedAt: iso(r.started_at), endedAt: iso(r.ended_at),
    sealId: r.seal_id, sealedAt: iso(r.sealed_at), sha256: r.recording_sha256 || null, bytes: r.recording_bytes ? Number(r.recording_bytes) : null,
    durationMs: r.recording_ms ? Number(r.recording_ms) : null };
}

// ───────────────────────── recording seal (Axowl audit ledger) ─────────────────────────
// POST {AXOWL_API_BASE}/api/public/apps/{client}/ledger/events  (docs: guides/seal-anything). The ledger is append-only and NOT idempotent —
// a repeated call writes a second entry — so we guard against duplicates ourselves with a claim kept in conclave_meetings.axowl_event_id:
//   null → 'pending:<ms>:<rand>' (claimed, ledger call in flight) → the ledger event id (sealed; seal_id/sealed_at set in the same update).
// A failed ledger call releases the claim; a claim older than PENDING_TTL_MS (worker died mid-call) may be taken over.
const PENDING_TTL_MS = 60_000;

async function sealRecording(env, origin, user, meetingId, rec) {
  await ensureSchema(env);
  const rows = await sql(env, 'select * from conclave_meetings where id=$1', [meetingId]);
  if (!rows.length) return json({ error: 'not_found' }, 404);
  const meeting = rows[0];
  if (meeting.host_sub !== user.sub) return json({ error: 'not_host', detail: '호스트만 녹화를 봉인할 수 있습니다.' }, 403);

  const done = (row) => json({ ...pub(row), verifyUrl: `${origin}/verify/${row.seal_id}` });
  if (meeting.seal_id) {
    // Same fingerprint again = a retry of a seal that already succeeded: answer with the stored result, write nothing.
    if (meeting.recording_sha256 === rec.sha256) return done(meeting);
    return json({ error: 'already_sealed', detail: '이 회의는 이미 다른 녹화로 봉인되었습니다.' }, 409);
  }
  if (!env.AXOWL_API_KEY) return json({ error: 'seal_not_configured', detail: 'AXOWL_API_KEY 시크릿이 설정되지 않았습니다.' }, 503);

  const cur = meeting.axowl_event_id || null;
  const pending = cur && /^pending:(\d+):/.exec(cur);
  if (cur && (!pending || Date.now() - Number(pending[1]) < PENDING_TTL_MS)) {
    return json({ error: 'sealing_in_progress', detail: '봉인이 진행 중입니다. 잠시 뒤 다시 시도하세요.' }, 409);
  }
  const claim = `pending:${Date.now()}:${shortId(6)}`;
  const claimed = await sql(env,
    'update conclave_meetings set axowl_event_id=$2 where id=$1 and host_sub=$3 and seal_id is null and axowl_event_id is not distinct from $4::text returning id',
    [meetingId, claim, user.sub, cur]);
  if (!claimed.length) return json({ error: 'sealing_in_progress', detail: '봉인이 진행 중입니다. 잠시 뒤 다시 시도하세요.' }, 409);

  let ev;
  try {
    ev = await ledgerSeal(env, {
      type: 'custom.recording_sealed',
      subjectId: meetingId,
      sha256: rec.sha256,
      data: { meetingId, title: meeting.title, hostSub: meeting.host_sub, bytes: rec.bytes, durationMs: rec.durationMs,
        startedAt: iso(meeting.started_at), endedAt: iso(meeting.ended_at) || new Date().toISOString(), alg: 'SHA-256' },
    });
  } catch (e) {
    await sql(env, 'update conclave_meetings set axowl_event_id=null where id=$1 and axowl_event_id=$2 and seal_id is null', [meetingId, claim]).catch(() => {});
    return json({ error: 'seal_failed', detail: String(e && e.message || e) }, 502);
  }

  const sealedAt = ev.createdAt && !isNaN(new Date(ev.createdAt)) ? new Date(ev.createdAt).toISOString() : new Date().toISOString();
  const save = () => sql(env,
    `update conclave_meetings set recording_sha256=$3, recording_bytes=$4, recording_ms=$5, seal_id=$6, sealed_at=$7,
       ended_at=coalesce(ended_at, now()), axowl_event_id=$8
     where id=$1 and axowl_event_id=$2 and seal_id is null returning *`,
    [meetingId, claim, rec.sha256, rec.bytes, rec.durationMs, shortId(10), sealedAt, String(ev.id).slice(0, 64)]);
  let saved;
  try { saved = await save(); } catch { saved = await save().catch(() => null); }
  if (!saved) {
    // Sealed at Axowl but we could not record it; the claim stays so a retry does not write a second ledger entry before it expires.
    return json({ error: 'seal_not_saved', detail: `Axowl 에는 봉인됐지만 저장에 실패했습니다 (이벤트 ${ev.id}).` }, 502);
  }
  if (!saved.length) return json({ error: 'sealing_in_progress' }, 409);
  return done(saved[0]);
}

// Retries only 503/429 (Axowl says nothing was written); anything else is surfaced so we never guess whether an entry exists.
async function ledgerSeal(env, payload) {
  const base = String(env.AXOWL_API_BASE || AXOWL_API).replace(/\/+$/, '');
  const delay = Number(env.SEAL_RETRY_DELAY_MS ?? 400);
  for (let attempt = 0; ; attempt++) {
    let res;
    try {
      res = await fetch(`${base}/api/public/apps/${AXOWL_CLIENT}/ledger/events`, {
        method: 'POST',
        headers: { 'X-Api-Key': env.AXOWL_API_KEY, 'content-type': 'application/json' },
        body: JSON.stringify(payload),
      });
    } catch (e) { throw new Error('Axowl 에 연결하지 못했습니다: ' + (e && e.message || e)); }
    if (res.status === 200 || res.status === 201) {
      const d = await res.json().catch(() => null);
      if (!d || !d.id || d.sealed === false) throw new Error('Axowl 가 봉인 결과를 돌려주지 않았습니다.');
      return d;
    }
    if ((res.status === 503 || res.status === 429) && attempt < 2) { await new Promise(r => setTimeout(r, delay * (attempt + 1))); continue; }
    const t = await res.json().catch(() => ({}));
    throw new Error(`Axowl 봉인 실패 (HTTP ${res.status}${t.error ? ' ' + t.error : ''})`);
  }
}

// ───────────────────────── database (Neon HTTP SQL, no driver) ─────────────────────────
let schemaReady = false;
async function ensureSchema(env) {
  if (schemaReady) return;
  await sql(env, `create table if not exists conclave_meetings (
      id text primary key,
      title text not null,
      host_sub text not null,
      host_name text,
      created_at timestamptz not null default now(),
      started_at timestamptz,
      ended_at timestamptz,
      recording_sha256 text,
      recording_bytes bigint,
      recording_ms bigint,
      seal_id text unique,
      sealed_at timestamptz,
      axowl_event_id text)`);
  schemaReady = true;
}

async function sql(env, query, params = []) {
  const cs = env.DATABASE_URL;
  if (!cs) throw new Error('DATABASE_URL is not set');
  const host = new URL(cs.replace(/^postgres(ql)?:/, 'https:')).hostname;
  const res = await fetch(`https://${host}/sql`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Neon-Connection-String': cs, 'Neon-Raw-Text-Output': 'false', 'Neon-Array-Mode': 'false' },
    body: JSON.stringify({ query, params }),
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(`db ${res.status}: ${data.message || JSON.stringify(data)}`);
  return data.rows || [];
}

// ───────────────────────── Axowl end-user token (RS256 via JWKS) ─────────────────────────
let jwksCache = { at: 0, keys: [] };
async function jwks() {
  if (Date.now() - jwksCache.at < 10 * 60_000 && jwksCache.keys.length) return jwksCache.keys;
  const res = await fetch(JWKS_URL);
  const data = await res.json();
  jwksCache = { at: Date.now(), keys: data.keys || [] };
  return jwksCache.keys;
}

async function optionalUser(request) {
  const h = request.headers.get('authorization') || '';
  const t = h.startsWith('Bearer ') ? h.slice(7).trim() : '';
  if (!t) return null;
  try { return await verifyAxowlJwt(t); } catch { return null; }
}
async function requireUser(request) {
  const u = await optionalUser(request);
  if (!u) throw Object.assign(new Error('unauthorized'), { status: 401 });
  return u;
}

async function verifyAxowlJwt(token) {
  const [h, p, s] = token.split('.');
  if (!h || !p || !s) throw new Error('malformed');
  const header = JSON.parse(b64uToStr(h));
  const payload = JSON.parse(b64uToStr(p));
  if (payload.exp && payload.exp * 1000 < Date.now()) throw new Error('expired');
  const keys = await jwks();
  const jwk = keys.find(k => k.kid === header.kid) || keys[0];
  if (!jwk) throw new Error('no jwks');
  const key = await crypto.subtle.importKey('jwk', { ...jwk, alg: 'RS256', ext: true }, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['verify']);
  const ok = await crypto.subtle.verify('RSASSA-PKCS1-v1_5', key, b64uToBytes(s), new TextEncoder().encode(`${h}.${p}`));
  if (!ok) throw new Error('bad signature');
  // Axowl end-user tokens carry email but no display name — show the local part (sonny@… → sonny) rather than a raw address.
  const name = payload.name || payload.display_name || payload.given_name || (payload.email ? String(payload.email).split('@')[0] : payload.sub);
  return { sub: String(payload.sub), name: String(name).slice(0, 80), email: payload.email || null };
}

// ───────────────────────── LiveKit access token (HS256) ─────────────────────────
async function livekitToken(env, { identity, name, room, host }) {
  if (!env.LIVEKIT_API_KEY || !env.LIVEKIT_API_SECRET) throw new Error('LIVEKIT keys are not set');
  const now = Math.floor(Date.now() / 1000);
  const payload = {
    iss: env.LIVEKIT_API_KEY, sub: identity, name, nbf: now - 10, exp: now + 4 * 3600, jti: shortId(8),
    video: { room, roomJoin: true, canPublish: true, canSubscribe: true, canPublishData: true, roomAdmin: !!host, roomRecord: !!host },
  };
  const enc = (o) => b64u(new TextEncoder().encode(JSON.stringify(o)));
  const data = `${enc({ alg: 'HS256', typ: 'JWT' })}.${enc(payload)}`;
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(env.LIVEKIT_API_SECRET), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const sig = new Uint8Array(await crypto.subtle.sign('HMAC', key, new TextEncoder().encode(data)));
  return `${data}.${b64u(sig)}`;
}

// ───────────────────────── helpers ─────────────────────────
function json(obj, status = 200) {
  return new Response(JSON.stringify(obj), { status, headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' } });
}
function shortId(n = 8) {
  const a = new Uint8Array(n); crypto.getRandomValues(a);
  const alphabet = 'abcdefghjkmnpqrstuvwxyz23456789';
  return Array.from(a, b => alphabet[b % alphabet.length]).join('');
}
function b64u(bytes) { let s = ''; for (const b of bytes) s += String.fromCharCode(b); return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, ''); }
function b64uToBytes(s) { s = s.replace(/-/g, '+').replace(/_/g, '/'); while (s.length % 4) s += '='; const bin = atob(s); return Uint8Array.from(bin, c => c.charCodeAt(0)); }
function b64uToStr(s) { return new TextDecoder().decode(b64uToBytes(s)); }
