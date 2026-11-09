import { json, fail, methodNotAllowed } from '../../_lib/http.js';
import { hasDatabase, withDb } from '../../_lib/db.js';

export async function onRequestGet({ env }) {
  if (!hasDatabase(env)) {
    return fail(503, 'db_not_configured', 'DATABASE_URL 시크릿이 설정되어 있지 않습니다.');
  }
  const started = Date.now();
  try {
    await withDb(env, (sql) => sql`select 1 as ok`);
  } catch (err) {
    console.error('db health failed', err);
    return fail(503, 'db_unreachable', '데이터베이스에 연결하지 못했습니다.');
  }
  return json({ database: 'connected', latencyMs: Date.now() - started });
}

export function onRequest() {
  return methodNotAllowed(['GET']);
}
