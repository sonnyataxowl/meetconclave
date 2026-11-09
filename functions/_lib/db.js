// DB 연결 헬퍼 — 엣지 함수는 Pages 시크릿 DATABASE_URL 로 Neon 에 HTTP 모드로 질의한다.
// HTTP 드라이버는 요청마다 fetch 로 질의하므로 열고 닫을 연결이 없다.
import { neon } from '@neondatabase/serverless';

export function hasDatabase(env) {
  return typeof env?.DATABASE_URL === 'string' && env.DATABASE_URL.length > 0;
}

// sql`select ...` 형태의 태그드 템플릿 함수를 돌려준다.
export function connect(env) {
  if (!hasDatabase(env)) {
    throw new Error('DATABASE_URL 시크릿이 설정되어 있지 않습니다.');
  }
  return neon(env.DATABASE_URL);
}

// fn(sql) 을 실행한다. (기존 호출부와 같은 모양을 유지한다.)
export async function withDb(env, fn) {
  return fn(connect(env));
}
