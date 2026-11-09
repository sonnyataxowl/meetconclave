// DB 연결 헬퍼 — 엣지 함수는 Pages 시크릿 DATABASE_URL 로 Postgres 에 직접 연결한다.
// 요청마다 연결을 열고 끝나면 닫는다 (엣지 isolate 간에 연결을 공유하지 않는다).
import postgres from 'postgres';

export function hasDatabase(env) {
  return typeof env?.DATABASE_URL === 'string' && env.DATABASE_URL.length > 0;
}

export function connect(env) {
  if (!hasDatabase(env)) {
    throw new Error('DATABASE_URL 시크릿이 설정되어 있지 않습니다.');
  }
  return postgres(env.DATABASE_URL, { max: 1, idle_timeout: 5, connect_timeout: 5, prepare: false });
}

// sql 연결을 열어 fn(sql) 을 실행하고 항상 닫는다.
export async function withDb(env, fn) {
  const sql = connect(env);
  try {
    return await fn(sql);
  } finally {
    await sql.end({ timeout: 1 });
  }
}
