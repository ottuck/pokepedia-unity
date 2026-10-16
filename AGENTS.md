# AGENTS.md — 에이전트용 프로젝트 안내

Pokepedia의 **퀴즈 → 컬렉션 → 배틀** 루프 중 배틀을 맡는 Unity 6.3 LTS(6000.3.26f1) 2D 턴제 배틀 게임. 웹(WebGL) 빌드를 Pokepedia(React)가 iframe으로 띄운다. 이 파일은 Claude Code와 Codex 등 모든 에이전트가 공통으로 읽는다(`CLAUDE.md`는 이 파일을 가져온다). 짧게 유지하고, 상세는 문서로 연결한다.

## 먼저 읽을 것

| 문서 | 내용 |
|---|---|
| [README.md](README.md) | 구조, 처음 설정, 테스트·웹 빌드 명령 |
| [docs/design.md](docs/design.md) | 기획·설계 전체. 13장 확정 / 14장 미정(임의로 확정 금지) / **15장 에이전트 지침** |
| [docs/bridge-protocol.md](docs/bridge-protocol.md) | Pokepedia ↔ Unity `postMessage` 규약(단일 출처) |
| [docs/M0-poc-result.md](docs/M0-poc-result.md) | M0 PoC 검증 결과 |

작업 전에 design.md 15장을 읽는다. 핵심만 옮기면 다음과 같다.

- 규칙은 **테스트를 먼저** 쓴다(TDD). 엔진은 에셋 없이 동작하고, **포켓몬 이름·스프라이트·문자열을 넣지 않으며 로스터 크기를 모른다.**
- 순수 C# 영역(`Domain`, `Content`, `Application`)에서 `UnityEngine`을 쓰지 않는다. `Presentation`은 규칙을 판단하지 않는다.
- 사실을 추측으로 채우지 않는다. 외부 제안(GPT 등)의 기술 이름·규칙·라이선스는 그대로 믿지 말고 공식 문서나 코드로 확인한 뒤 문서에 **출처와 미확인 부분**을 함께 적는다.
- 14장의 미정 사항과 사용자 결정이 필요한 것은 임의로 확정하지 않고 추천안과 이유를 제시한다. 규칙·용어는 먼저 설명하고, 새 C#/Unity 개념은 Java와 비교해 설명한다.
- Pokepedia 저장소·DB는 함부로 수정하지 않는다. 연동 변경은 pokepedia 저장소의 별도 PR로 하고 그쪽 `CLAUDE.md` 정책을 따른다.

## 명령

```sh
# 순수 C# 테스트 (Unity 불필요, CI와 동일)
dotnet test Tests.Dotnet
# Unity EditMode 테스트 (Unity CLI 1.0.0-beta.13 설치됨)
unity test . --mode EditMode
# 웹 빌드 (Release, 에디터가 이 폴더를 열고 있으면 실패)
"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod Pokepedia.EditorTools.WebBuild.Release -logFile Logs/build-web.log
```

- Python 도구는 `py -3`, 격리 실행은 `python -I`. 스프라이트는 저장소에 없고 `Tools/sprites/build_sprites.py`로 재현한다([Tools/sprites/README.md](Tools/sprites/README.md)).
- C# 언어 버전은 C# 9로 고정한다. `Tests.Dotnet`이 Unity에서 컴파일되지 않는 최신 문법을 CI에서 먼저 잡는다.
- `Pokepedia.Application` 어셈블리의 네임스페이스는 `Pokepedia.App`이다(`UnityEngine.Application`이 가려지는 것을 피하려는 것).
- 줄바꿈은 저장소에 LF로 저장한다. `LF will be replaced by CRLF` 경고는 무시해도 된다.

## CI

- 워크플로는 [domain-tests.yml](.github/workflows/domain-tests.yml) 하나다. ubuntu + .NET 8에서 `dotnet test Tests.Dotnet`만 실행한다. **Unity 라이선스가 필요한 빌드·테스트는 CI에 없다**(Unity 에디터 검증은 로컬에서).
- 경로 필터가 있어 `Assets/Scripts/{Domain,Content,Application}/**`, `Assets/Tests/EditMode/**`, `Tests.Dotnet/**`, 워크플로 파일이 바뀔 때만 돈다. **문서·에셋·화면 코드만 바꾼 PR에는 체크가 없는 게 정상**이다.
- CI가 도는 PR은 통과를 확인하고 병합한다. 실패하면 원인을 고친다(테스트를 약화하거나 삭제하지 않는다).

## Git·PR 워크플로

1. 작업마다 새 브랜치(`feat/…`, `fix/…`, `docs/…`)를 만든다. `main`에 직접 커밋하지 않는다.
2. 커밋 메시지는 한국어, `type: 요약` 형식(`docs:`, `ci:`, `feat:` 등). 메시지 끝에는 시스템이 안내하는 `Co-Authored-By` 문구를 따른다.
3. 사용자가 시키면 **커밋 → push → PR 생성 → 병합까지 진행**한다. 병합은 squash + 브랜치 삭제(`gh pr merge <번호> --squash --delete-branch`). PR 본문은 한국어로 변경 내용과 확인 사항을 적는다.
4. **리뷰 없이 병합해도 된다.** Codex 리뷰가 달리면 반영할지는 선택이다. 반영 여부는 사용자 판단에 맡기고, 요구사항 해석·되돌리기 어려운 변경·불확실한 부분은 PR 본문에 명시한다.
5. 병합 명령(`gh pr merge`)이 자동 모드 분류기에 막힐 수 있다. 막히면 우회하지 말고 사용자에게 알린다. 사용자가 settings에 `Bash(gh pr merge:*)` 허용 규칙을 직접 추가해야 자동 병합이 된다. 에이전트가 스스로 권한을 바꾸지 않는다.
6. 병합 뒤에는 `main`으로 돌아가 최신으로 맞춘다.

저장소에 커밋하지 않는 것: `Library/`, `Builds/`, `Logs/`, 생성된 `*.csproj`(`Tests.Dotnet` 제외), `Assets/Content/Sprites/`(생성물).

## Unity 에디터와 병렬 작업

- Unity 에디터는 프로젝트 폴더당 하나만 열 수 있다(`Library/` 점유). 에디터가 열린 폴더에서는 batchmode 실행(컴파일·테스트·빌드)이 실패한다.
- 에디터 조작이 필요한 작업(씬, 파티클, UI)은 에이전트 한 명이 순서대로 맡는다. 순수 C# 로직과 `dotnet test`는 별도 worktree에서 병렬로 해도 된다. worktree를 새로 만들면 첫 임포트가 오래 걸린다.
- IDE(Rider·VS Code)는 에디터와 별개라 같이 열어도 된다.
- 에이전트의 Unity 연동은 MCP보다 **Unity CLI 직접 호출**(`unity command`, `unity eval`)을 기본으로 한다. 현재 `unity pipeline install`은 하지 않았다. 검증된 사실과 미확인 항목은 [design.md 15장 "Unity CLI·에이전트 연동"](docs/design.md)을 따른다. `unity eval`은 임의 C#을 실행하므로 권한 허용 목록에는 필요한 명령만 넣는다.

## 문서 갱신

- 새로 확인한 사실(버전, 한도, 가격, 도구 동작)은 design.md에 출처와 확인 날짜, 미확인 부분을 함께 적는다.
- 결정이 확정되면 13장으로, 위임 항목을 정했으면 결과를 문서에 정리한다.
- 이 파일은 에이전트가 매번 읽는다. 상세는 문서로 보내고 여기에는 규칙과 명령만 남긴다.
