# Pages Functions 규약

- 라우트는 `functions/api/**` 파일 경로가 곧 URL (`functions/api/health.js` → `/api/health`).
- 공통 코드는 `functions/_lib/` 에 둔다 (라우트가 되지 않는다).
- 응답은 항상 `_lib/http.js` 의 `json()` / `fail()` 로 만든다.
  - 성공: `{ "ok": true, "data": ... }`
  - 실패: `{ "ok": false, "error": { "code": "...", "message": "..." } }`
- `functions/api/_middleware.js` 가 처리되지 않은 예외를 500 `internal_error` 로 바꾼다.
- `functions/api/[[path]].js` 가 알 수 없는 경로에 404 `not_found` 를 돌려준다.
- 시크릿(DATABASE_URL, LIVEKIT_* 등)은 `context.env` 로만 읽는다.
