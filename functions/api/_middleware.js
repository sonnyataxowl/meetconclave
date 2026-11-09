// /api 아래 모든 함수를 감싼다 — 처리되지 않은 예외도 규약된 오류 형식으로 돌려준다.
import { fail } from '../_lib/http.js';

export async function onRequest(context) {
  try {
    return await context.next();
  } catch (err) {
    console.error('unhandled error', err);
    return fail(500, 'internal_error', '서버 내부 오류가 발생했습니다.');
  }
}
