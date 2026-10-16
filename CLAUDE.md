# CLAUDE.md

Pokepedia의 퀴즈 → 컬렉션 → **배틀** 중 배틀을 맡는 Unity 6.3 LTS(6000.3.26f1) 2D 턴제 게임. WebGL 빌드를 Pokepedia(React)가 iframe으로 띄운다. 에이전트 안내는 이 파일 하나만 둔다(`AGENTS.md` 없음).

## 문서

- [docs/ROADMAP.md](docs/ROADMAP.md): 마일스톤 현황, 진행 기록, M0 결과. **작업 전에 현재 상태를 확인하고, 단계가 바뀌거나 완료·주요 결정이 생기면 여기에 기록한다**(해당 PR에 포함).
- [docs/PROJECT.md](docs/PROJECT.md): 기획·설계·Pokepedia 연동 규약(9.2). 13장 확정, 14장 미정, **15장 에이전트 지침**.
- [README.md](README.md): 구조, 처음 설정, 웹 빌드·로컬 실행 명령.

## 지킬 것 (상세는 PROJECT.md 15장)

- 규칙은 **테스트 먼저**(TDD). `Domain`·`Content`·`Application`은 순수 C#이라 `UnityEngine`을 쓰지 않는다. `Presentation`은 규칙을 판단하지 않는다.
- 엔진 코드에 포켓몬 이름·스프라이트·문자열을 넣지 않는다. **엔진은 로스터 크기를 모른다.**
- 사실을 추측으로 채우지 않는다. 외부 제안(GPT 등)의 기술 이름·규칙·라이선스는 공식 문서나 코드로 확인하고, 문서에 **출처와 미확인 부분**을 적는다.
- PROJECT.md 14장의 미정 사항은 임의로 확정하지 않고 추천안과 이유를 제시한다.
- Pokepedia 저장소·DB는 함부로 수정하지 않는다(변경은 그 저장소의 별도 PR).

## 명령

```sh
dotnet test Tests.Dotnet              # 순수 C# 테스트, Unity 불필요 (CI와 동일)
unity test . --mode EditMode          # Unity EditMode 테스트 (Unity CLI)
```

- C# 9 고정(최신 문법은 `Tests.Dotnet`이 CI에서 먼저 잡는다). `Pokepedia.Application` 어셈블리의 네임스페이스는 `Pokepedia.App`이다(`UnityEngine.Application` 가림 방지).
- `LF will be replaced by CRLF` 경고는 무시한다.

## CI

- 워크플로는 `domain-tests` 하나(ubuntu, .NET 8, `dotnet test Tests.Dotnet`). **Unity 빌드·테스트는 CI에 없고** 로컬에서 확인한다.
- 경로 필터가 있어 `Domain`·`Content`·`Application`·`Assets/Tests/EditMode`·`Tests.Dotnet`·워크플로 파일이 바뀔 때만 돈다. **문서·에셋·화면 코드만 바꾼 PR에는 체크가 없는 게 정상**이다.
- 실패하면 원인을 고친다. 테스트를 약화하거나 삭제하지 않는다.

## Git·PR

1. 작업마다 새 브랜치(`feat/…`, `fix/…`, `docs/…`)를 만들고 `main`에 직접 커밋하지 않는다.
2. 커밋 메시지는 한국어 `type: 요약`(`docs:`, `ci:`, `feat:` 등), 끝에 시스템이 안내하는 `Co-Authored-By` 문구. PR 본문도 한국어로 변경 내용과 확인 사항을 적는다.
3. 작업이 끝나면 **큰 문제가 없는 한 항상 push → PR → 병합까지 진행**하고 되묻지 않는다. 병합은 squash + 브랜치 삭제, 병합 뒤 `main`을 최신으로 맞춘다. 리뷰 없이 병합해도 되고, Codex 리뷰 반영은 선택이다.
   - **문서만 바꾼 PR은 CI를 기다리지 않고 바로 병합한다.** "문서만"은 PR 전체 diff에 위 CI 경로 파일이 하나도 없다는 뜻이며, 이때는 체크가 아예 생기지 않는다.
   - **CI 경로 파일이 하나라도 있는 PR**(주석만 바꿔도 해당)은 이후 문서만 추가 push해도 `pull_request` 체크가 다시 도므로, **마지막 push의 체크가 통과한 뒤** 병합한다. 통과하면 병합하는 것이 기본이며 따로 묻지 않는다.
   - 이 저장소는 GitHub auto-merge를 쓸 수 없다(설정을 켜도 `false`로 유지됨, 비공개 저장소의 플랜 제한으로 추정). 그래서 push 후 앱의 `get_status`로 체크를 확인하고(`gh`로 폴링하지 않는다), 통과했으면 `gh pr merge <번호> --squash --delete-branch`로 직접 병합한다. 아직 도는 중이면 다른 일을 한 뒤 다시 확인한다.
4. 다음은 큰 문제이니 병합하지 말고 알린다: CI 실패, 충돌, 요구사항 해석이 불확실, 되돌리기 어려운 변경(설정·권한·배포·외부 서비스), 14장 미정 사항 확정.
5. `gh pr merge`가 자동 모드 분류기에 막히면 우회하지 않고 알린다. 에이전트가 스스로 권한 규칙을 추가하지 않는다(사용자가 `Bash(gh pr merge:*)`를 허용해야 한다).

## Unity 에디터와 병렬 작업

- 에디터는 프로젝트 폴더당 하나만 열 수 있다(`Library/` 점유). **에디터가 열린 폴더에서는 batchmode 실행(컴파일·테스트·빌드)이 실패한다.**
- 에디터 조작 작업(씬, 파티클, UI)은 에이전트 한 명이 순서대로 맡고, 순수 C#·`dotnet test`는 별도 worktree에서 병렬로 해도 된다(worktree마다 첫 임포트가 오래 걸린다).
- 에디터 연동은 MCP보다 Unity CLI 직접 호출(`unity command`, `unity eval`)이 기본이다. `unity pipeline install`은 아직 안 했다. 검증된 사실과 미확인 항목은 PROJECT.md 15장 "Unity CLI·에이전트 연동".
