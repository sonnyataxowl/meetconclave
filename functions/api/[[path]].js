// 정의되지 않은 /api/* 경로는 모두 여기로 와서 규약된 404 를 돌려준다.
import { notFound } from '../_lib/http.js';

export function onRequest() {
  return notFound();
}
