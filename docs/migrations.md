# 마이그레이션 요청 규약

DB 는 에이전트에게 읽기 전용이다. 테이블 변경은 항상 `axowl_request_migration` 으로 요청하고 사람이 승인한다.

## 규칙

- 마이그레이션 SQL 은 코드 PR 에 넣을 수 없다 (`axowl_propose_code_change` 가 `migrations/` 경로와 .sql 을 막는다). SQL 은 요청 본문에만 둔다.
- 요청 제목은 `NNNN 설명` — 4자리 번호를 올리기만 한다. 이미 요청한 번호는 고치지 않고 새 번호로 보낸다.
- 적용된 번호는 이 문서 아래 목록에 적는다.

## 요청 틀

`axowl_request_migration` 에 넣을 내용:

1. 제목: `NNNN 설명`
2. SQL: `create table if not exists` 처럼 다시 실행해도 안전하게 쓴다.
3. 이유(reason): 어떤 스토리·기능에 필요한지, 바뀌는 테이블·컬럼, 기존 행에 생기는 일, 되돌릴 수 있는지.

## 순서

1. `axowl_request_migration` 으로 요청한다 (승인은 사람이 한다).
2. `axowl_approval_status` 로 적용을 확인한다.
3. 적용된 뒤에 그 테이블을 쓰는 함수 PR 을 연다.

## 적용된 마이그레이션

(아직 없음)

## 연결 확인

`GET /api/health/db` 가 200 과 `database: "connected"` 를 돌려주면 DATABASE_URL 연결이 된 것이다.
