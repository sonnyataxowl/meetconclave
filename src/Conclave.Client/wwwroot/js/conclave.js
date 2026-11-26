// Conclave browser glue — Axowl hosted login, LiveKit room, host recording + SHA-256 seal.
// Loaded as an ES module from Blazor (IJSRuntime.InvokeAsync("import", "./js/conclave.js")).

export const AXOWL_API = 'https://testapi.axowl.com';
export const AXOWL_PORTAL = 'https://login.axowl.com';
export const CLIENT_KEY = 'client_6f8eb765181d45ff938e';
const TOKEN_KEY = 'axowl_access_token';          // same key as @axowl/sdk-core → interoperable
const RETURN_KEY = 'conclave_return_to';

// ───────── auth ─────────
export function login(returnTo) {
  try { localStorage.setItem(RETURN_KEY, returnTo || location.pathname); } catch {}
  const redirect = encodeURIComponent(location.origin + '/');
  location.href = `${AXOWL_PORTAL}/login/${CLIENT_KEY}?redirect_url=${redirect}`;
}

/** Call once on app start. If the hosted portal sent us back with ?code=, exchange it for the token. Returns the path to continue on (or null). */
export async function completeLogin() {
  const u = new URL(location.href);
  const code = u.searchParams.get('code');
  if (!code) return null;
  const res = await fetch(`${AXOWL_API}/api/public/apps/${CLIENT_KEY}/auth/session/exchange`, {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ code }),
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok || !data.accessToken) throw new Error(data.error || data.message || `exchange failed (${res.status})`);
  localStorage.setItem(TOKEN_KEY, data.accessToken);
  if (data.refreshToken) localStorage.setItem('axowl_refresh_token', data.refreshToken);
  u.searchParams.delete('code'); u.searchParams.delete('state');
  history.replaceState(null, '', u.pathname + (u.search || '') + u.hash);
  let back = null;
  try { back = localStorage.getItem(RETURN_KEY); localStorage.removeItem(RETURN_KEY); } catch {}
  return back && back !== '/' ? back : '/meetings';
}

export function token() { try { return localStorage.getItem(TOKEN_KEY); } catch { return null; } }

export function user() {
  const t = token(); if (!t) return null;
  try {
    const p = JSON.parse(atob(t.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    if (p.exp && p.exp * 1000 < Date.now()) { localStorage.removeItem(TOKEN_KEY); return null; }
    return { sub: p.sub, name: p.name || p.display_name || p.given_name || (p.email ? String(p.email).split('@')[0] : '사용자'), email: p.email || null };
  } catch { return null; }
}

export async function logout() {
  const t = token();
  try {
    if (t) await fetch(`${AXOWL_API}/api/public/apps/${CLIENT_KEY}/auth/logout`, { method: 'POST', headers: { authorization: `Bearer ${t}`, 'content-type': 'application/json' }, body: '{}' });
  } catch {}
  localStorage.removeItem(TOKEN_KEY); localStorage.removeItem('axowl_refresh_token');
  location.href = '/';
}

// ───────── LiveKit room ─────────
let room = null, localStream = null, grid = null, dotnet = null;

export async function joinRoom(gridId, url, lkToken, dotnetRef) {
  const LK = window.LivekitClient;
  if (!LK) throw new Error('livekit-client not loaded');
  grid = document.getElementById(gridId);
  dotnet = dotnetRef;
  room = new LK.Room({ adaptiveStream: true, dynacast: true });
  room.on(LK.RoomEvent.TrackSubscribed, (track, pub, participant) => attach(track, participant));
  room.on(LK.RoomEvent.TrackUnsubscribed, (track) => track.detach().forEach(el => el.remove()));
  room.on(LK.RoomEvent.ParticipantDisconnected, (p) => { const tile = document.getElementById('tile-' + p.identity); if (tile) tile.remove(); notify(); });
  room.on(LK.RoomEvent.ParticipantConnected, () => notify());
  room.on(LK.RoomEvent.LocalTrackPublished, (pub) => { if (pub.track) attach(pub.track, room.localParticipant, true); });
  await room.connect(url, lkToken);
  // Camera/mic may be blocked (no device, denied, or an insecure context). Join anyway as a viewer and say what happened.
  let media = 'both';
  try { await room.localParticipant.enableCameraAndMicrophone(); }
  catch (e1) {
    try { await room.localParticipant.setMicrophoneEnabled(true); media = 'mic-only'; }
    catch (e2) { media = 'none:' + ((e1 && e1.message) || e1); }
  }
  if (media !== 'both') tileFor(room.localParticipant, true);
  notify();
  return media;
}

function tileFor(participant, isLocal) {
  let tile = document.getElementById('tile-' + participant.identity);
  if (!tile) {
    tile = document.createElement('div'); tile.id = 'tile-' + participant.identity; tile.className = 'cc-tile' + (isLocal ? ' cc-tile-local' : '');
    const name = document.createElement('div'); name.className = 'cc-tile-name'; name.textContent = (participant.name || participant.identity) + (isLocal ? ' (나)' : '');
    tile.appendChild(name); grid.appendChild(tile);
  }
  return tile;
}
function attach(track, participant, isLocal = false) {
  const tile = tileFor(participant, isLocal);
  const el = track.attach();
  if (track.kind === 'video') { el.className = 'cc-video'; tile.prepend(el); }
  else if (!isLocal) { el.className = 'cc-audio'; tile.appendChild(el); } // don't play our own mic back
}
function notify() { try { dotnet && dotnet.invokeMethodAsync('OnParticipants', participants()); } catch {} }
export function participants() {
  if (!room) return [];
  const list = [{ identity: room.localParticipant.identity, name: room.localParticipant.name || room.localParticipant.identity, local: true }];
  room.remoteParticipants.forEach(p => list.push({ identity: p.identity, name: p.name || p.identity, local: false }));
  return list;
}
export async function setMic(on) { if (room) await room.localParticipant.setMicrophoneEnabled(on); }
export async function setCamera(on) { if (room) await room.localParticipant.setCameraEnabled(on); }
export async function leaveRoom() { stopRecordingSilently(); if (room) { await room.disconnect(); room = null; } if (grid) grid.innerHTML = ''; }

// ───────── recording (host) ─────────
let recorder = null, chunks = [], recStart = 0, lastBlobUrl = null, recError = null;
export function startRecording() {
  if (!room) throw new Error('회의에 입장한 뒤에 녹화할 수 있습니다.');
  if (recorder && recorder.state !== 'inactive') throw new Error('이미 녹화 중입니다.');
  if (typeof MediaRecorder === 'undefined') throw new Error('이 브라우저는 녹화(MediaRecorder)를 지원하지 않습니다.');
  const tracks = [];
  room.localParticipant.trackPublications.forEach(pub => { if (pub.track && pub.track.mediaStreamTrack) tracks.push(pub.track.mediaStreamTrack); });
  // remote audio too, so the recording holds the whole conversation
  room.remoteParticipants.forEach(p => p.trackPublications.forEach(pub => { if (pub.track && pub.track.kind === 'audio' && pub.track.mediaStreamTrack) tracks.push(pub.track.mediaStreamTrack); }));
  if (!tracks.length) throw new Error('녹화할 영상·음성이 없습니다. 카메라·마이크를 켠 뒤 다시 시도하세요.');
  localStream = new MediaStream(tracks);
  const mime = MediaRecorder.isTypeSupported('video/webm;codecs=vp9,opus') ? 'video/webm;codecs=vp9,opus' : 'video/webm';
  recorder = new MediaRecorder(localStream, { mimeType: mime });
  chunks = []; recStart = Date.now(); recError = null;
  recorder.ondataavailable = (e) => { if (e.data && e.data.size) chunks.push(e.data); };
  recorder.onerror = (e) => { recError = (e && e.error && e.error.message) || '녹화 중 오류가 발생했습니다.'; };
  recorder.start(1000);
  return mime;
}
function stopRecordingSilently() { try { if (recorder && recorder.state !== 'inactive') recorder.stop(); } catch {} }

/** Stops the recording, hashes it in the browser (SHA-256) and returns the fingerprint. The file never leaves this device. */
export async function stopRecording() {
  if (!recorder) throw new Error('녹화 중이 아닙니다.');
  const rec = recorder; recorder = null;
  if (rec.state !== 'inactive') {
    const done = new Promise(res => { rec.onstop = res; });
    rec.stop(); await done;
  }
  if (recError) throw new Error(recError);
  const blob = new Blob(chunks, { type: rec.mimeType });
  if (!blob.size) throw new Error('녹화된 데이터가 없습니다. 조금 더 길게 녹화해 주세요.');
  const durationMs = Date.now() - recStart;
  const sha256 = await sha256Hex(await blob.arrayBuffer());
  if (lastBlobUrl) URL.revokeObjectURL(lastBlobUrl);
  lastBlobUrl = URL.createObjectURL(blob);
  return { sha256, bytes: blob.size, durationMs, downloadUrl: lastBlobUrl, fileName: `conclave-${new Date(recStart).toISOString().replace(/[:.]/g, '-')}.webm` };
}

export async function sha256Hex(buf) {
  const h = await crypto.subtle.digest('SHA-256', buf);
  return Array.from(new Uint8Array(h), b => b.toString(16).padStart(2, '0')).join('');
}
/** Verify page: hash a file picked by the user (input element id). */
export async function hashFileInput(inputId) {
  const input = document.getElementById(inputId);
  const f = input && input.files && input.files[0];
  if (!f) return null;
  return { sha256: await sha256Hex(await f.arrayBuffer()), bytes: f.size, name: f.name };
}

// ───────── Axowl ledger event (sealed on write in Axowl) ─────────
export async function sendSealEvent(meetingId, sha256, bytes, durationMs) {
  const u = user();
  const id = crypto.randomUUID().replace(/-/g, '');
  const body = { events: [{ id, type: 'custom.recording_sealed', ts: new Date().toISOString(), euid: u && u.sub, sid: sessionId(), path: location.pathname,
    data: { meetingId, sha256, bytes, durationMs, alg: 'SHA-256' } }] };
  try {
    const res = await fetch(`${AXOWL_API}/api/public/apps/${CLIENT_KEY}/analytics/events`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body), keepalive: true });
    return res.ok || res.status === 202 ? id : null;
  } catch { return null; }
}
function sessionId() { let s = sessionStorage.getItem('conclave_sid'); if (!s) { s = crypto.randomUUID().replace(/-/g, ''); sessionStorage.setItem('conclave_sid', s); } return s; }

export async function copyText(t) { try { await navigator.clipboard.writeText(t); return true; } catch { return false; } }
