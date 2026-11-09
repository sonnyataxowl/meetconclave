// 엣지 함수 공통 헬퍼 — 모든 /api 응답은 이 모듈의 json()/fail() 로 만든다.
// 성공: { ok: true, data: ... }   실패: { ok: false, error: { code, message } }

const BASE_HEADERS = {
  'content-type': 'application/json; charset=utf-8',
  'cache-control': 'no-store',
};

export function json(data, status = 200, headers = {}) {
  return new Response(JSON.stringify({ ok: true, data }), {
    status,
    headers: { ...BASE_HEADERS, ...headers },
  });
}

export function fail(status, code, message, headers = {}) {
  return new Response(JSON.stringify({ ok: false, error: { code, message } }), {
    status,
    headers: { ...BASE_HEADERS, ...headers },
  });
}

export const notFound = () => fail(404, 'not_found', '존재하지 않는 경로입니다.');

export const methodNotAllowed = (allowed) =>
  fail(405, 'method_not_allowed', '허용되지 않는 메서드입니다.', { allow: allowed.join(', ') });
