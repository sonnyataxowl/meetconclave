// Runs the real _worker.js /seal route against a fake Neon, fake JWKS and fake Axowl ledger. Prints 'OK' when every scenario passes.
import assert from 'node:assert/strict';

const worker = (await import(process.argv[2])).default;
const realFetch = globalThis.fetch;

// ── signing keys + tokens ──
const kp = await crypto.subtle.generateKey({ name: 'RSASSA-PKCS1-v1_5', modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: 'SHA-256' }, true, ['sign', 'verify']);
const jwk = { ...(await crypto.subtle.exportKey('jwk', kp.publicKey)), kid: 'k1' };
const b64u = (b) => Buffer.from(b).toString('base64url');
async function token(sub) {
  const h = b64u(JSON.stringify({ alg: 'RS256', kid: 'k1' }));
  const p = b64u(JSON.stringify({ sub, email: `${sub}@x.io`, exp: Math.floor(Date.now() / 1000) + 600 }));
  const sig = await crypto.subtle.sign('RSASSA-PKCS1-v1_5', kp.privateKey, new TextEncoder().encode(`${h}.${p}`));
  return `${h}.${p}.${b64u(new Uint8Array(sig))}`;
}

// ── fake database: just the statements the seal path issues ──
const db = new Map();
const addMeeting = (id, host) => db.set(id, { id, title: 'T ' + id, host_sub: host, host_name: host, created_at: new Date().toISOString(), started_at: null, ended_at: null,
  recording_sha256: null, recording_bytes: null, recording_ms: null, seal_id: null, sealed_at: null, axowl_event_id: null });
function runSql(query, params) {
  const q = query.replace(/\s+/g, ' ');
  if (q.startsWith('create table')) return [];
  if (q.startsWith('select * from conclave_meetings where id=$1')) return db.has(params[0]) ? [{ ...db.get(params[0]) }] : [];
  if (q.includes('set axowl_event_id=$2 where')) {
    const r = db.get(params[0]);
    if (r && r.host_sub === params[2] && !r.seal_id && (r.axowl_event_id ?? null) === (params[3] ?? null)) { r.axowl_event_id = params[1]; return [{ id: r.id }]; }
    return [];
  }
  if (q.includes('set axowl_event_id=null')) {
    const r = db.get(params[0]);
    if (r && r.axowl_event_id === params[1] && !r.seal_id) r.axowl_event_id = null;
    return [];
  }
  if (q.includes('set recording_sha256=$3')) {
    const r = db.get(params[0]);
    if (!r || r.axowl_event_id !== params[1] || r.seal_id) return [];
    Object.assign(r, { recording_sha256: params[2], recording_bytes: params[3], recording_ms: params[4], seal_id: params[5], sealed_at: params[6], ended_at: r.ended_at || new Date().toISOString(), axowl_event_id: params[7] });
    return [{ ...r }];
  }
  throw new Error('unexpected sql: ' + q);
}

// ── fake Axowl ledger: a queue of statuses per scenario ──
let ledgerCalls = [], ledgerQueue = [];
globalThis.fetch = async (url, init = {}) => {
  url = String(url);
  if (url.includes('.well-known/jwks.json')) return Response.json({ keys: [jwk] });
  if (url.endsWith('/sql')) {
    const { query, params } = JSON.parse(init.body);
    return Response.json({ rows: runSql(query, params) });
  }
  if (url.includes('/ledger/events')) {
    ledgerCalls.push({ url, key: init.headers['X-Api-Key'], body: JSON.parse(init.body) });
    const status = ledgerQueue.length ? ledgerQueue.shift() : 201;
    return status === 201
      ? Response.json({ id: 'ev-' + ledgerCalls.length, sealed: true, createdAt: '2026-11-26T10:00:00.000Z' }, { status })
      : Response.json({ error: 'SEAL_UNAVAILABLE' }, { status });
  }
  return realFetch(url, init);
};

const env = { DATABASE_URL: 'postgres://u:p@db.example.neon.tech/x', AXOWL_API_KEY: 'ah_live_test', SEAL_RETRY_DELAY_MS: 0, ASSETS: {} };
const SHA = 'a'.repeat(64), SHA2 = 'b'.repeat(64);
async function seal(id, who, body = { sha256: SHA, bytes: 10, durationMs: 5000 }, e = env) {
  const headers = { 'content-type': 'application/json' };
  if (who) headers.authorization = 'Bearer ' + await token(who);
  const res = await worker.fetch(new Request(`https://conclave.test/api/meetings/${id}/seal`, { method: 'POST', headers, body: JSON.stringify(body) }), e, {});
  return { status: res.status, body: await res.json() };
}

// 1. auth + validation + host check
addMeeting('m1xx', 'host1');
assert.equal((await seal('m1xx', null)).status, 401);
assert.equal((await seal('m1xx', 'host1', { sha256: 'xyz' })).status, 400);
assert.equal((await seal('nope', 'host1')).status, 404);
assert.equal((await seal('m1xx', 'guest9')).status, 403);
assert.equal(ledgerCalls.length, 0);

// 2. host seals: 200, ledger called once with key + fingerprint, row saved
const ok = await seal('m1xx', 'host1');
assert.equal(ok.status, 200);
assert.equal(ok.body.sha256, SHA);
assert.equal(ok.body.sealedAt, '2026-11-26T10:00:00.000Z');
assert.match(ok.body.sealId, /^[a-z0-9]{10}$/);
assert.equal(ledgerCalls.length, 1);
assert.equal(ledgerCalls[0].key, 'ah_live_test');
assert.equal(ledgerCalls[0].body.type, 'custom.recording_sealed');
assert.equal(ledgerCalls[0].body.sha256, SHA);
assert.equal(ledgerCalls[0].body.subjectId, 'm1xx');
assert.equal(db.get('m1xx').axowl_event_id, 'ev-1');

// 3. duplicate: same fingerprint returns the stored result, different one is refused, no new ledger entry
const again = await seal('m1xx', 'host1');
assert.equal(again.status, 200);
assert.equal(again.body.sealId, ok.body.sealId);
assert.equal((await seal('m1xx', 'host1', { sha256: SHA2, bytes: 1, durationMs: 1 })).status, 409);
assert.equal(ledgerCalls.length, 1);

// 4. ledger 503 twice then ok → retried
addMeeting('m2xx', 'host1'); ledgerQueue = [503, 503];
const retried = await seal('m2xx', 'host1');
assert.equal(retried.status, 200);
assert.equal(ledgerCalls.length, 4);

// 5. ledger hard failure → 502, claim released, a later retry works
addMeeting('m3xx', 'host1'); ledgerQueue = [500];
const failed = await seal('m3xx', 'host1');
assert.equal(failed.status, 502);
assert.equal(failed.body.error, 'seal_failed');
assert.equal(db.get('m3xx').axowl_event_id, null);
assert.equal(db.get('m3xx').seal_id, null);
assert.equal((await seal('m3xx', 'host1')).status, 200);

// 6. no API key → 503 and nothing claimed
addMeeting('m4xx', 'host1');
const before = ledgerCalls.length;
assert.equal((await seal('m4xx', 'host1', undefined, { ...env, AXOWL_API_KEY: '' })).status, 503);
assert.equal(ledgerCalls.length, before);
assert.equal(db.get('m4xx').axowl_event_id, null);

// 7. a fresh claim by someone else blocks (409); a stale one is taken over
addMeeting('m5xx', 'host1'); db.get('m5xx').axowl_event_id = `pending:${Date.now()}:abcdef`;
assert.equal((await seal('m5xx', 'host1')).status, 409);
db.get('m5xx').axowl_event_id = `pending:${Date.now() - 120000}:abcdef`;
assert.equal((await seal('m5xx', 'host1')).status, 200);

console.log('OK');
