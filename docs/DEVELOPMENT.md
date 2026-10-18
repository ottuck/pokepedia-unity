# 개발 메모

디렉터리 구조, 처음 설정, 테스트, 웹 빌드·로컬 실행, 배포 명령. 소개와 전체 구조도는 [README.md](../README.md).

## 디렉터리

```text
Assets/
  Scripts/
    Domain/        Pokepedia.Domain        순수 C#, UnityEngine 참조 금지 (모델, Battle/ 엔진·이벤트, Rules/ 상성·능력치·데미지, IRandom)
    Content/       Pokepedia.Content       순수 C# (JSON 파서, 데이터 로더·카탈로그, 로케일)
    Application/   Pokepedia.Application   순수 C#, 네임스페이스 Pokepedia.App (NPC AI, 시뮬레이터, 연결 흐름, IHostBridge)
    Bridge/        Pokepedia.Bridge        IHostBridge 의 Unity/WebGL 구현 + HostBridge.jslib
    Presentation/  Pokepedia.Presentation  화면·입력·연출 (규칙 판단 금지)
  Editor/          ProjectSetup, FontAtlasBuilder, WebBuild
  Tests/EditMode/  NUnit 테스트 (UnityEngine 미사용 → dotnet 에서도 실행)
  Content/         Data(species·moves·roster·animation.json), Locale(ko/en/ja), Fonts(Galmuri11 + Static 아틀라스), Audio(사운드 매니페스트), VFX
  Resources/       Sprites·Audio 생성물 (커밋 안 함)
  WebGLTemplates/Pokepedia/  웹 템플릿 (HTML 시작·VS 화면, 10:9 화면, 키 입력 차단, origin 검증)
Tests.Dotnet/      Unity 없이 순수 C# 테스트를 돌리는 프로젝트 (소스 링크)
Tools/             sprites(스프라이트 재현), web(로컬 서버·모의 호스트·배포), audio(속성 공격음 시제품·렌더 도구), unity
docs/              PROJECT.md(기획·설계·연동 규약), ROADMAP.md(현재 상태·마일스톤·보관), 이 문서, 스프라이트 스타일 비교 페이지
```

의존 방향: `Presentation → Application → Content → Domain`, `Presentation → Bridge → Application`(인터페이스 구현). asmdef가 강제한다.

`Pokepedia.Application` 어셈블리의 네임스페이스를 `Pokepedia.App`으로 둔 이유: `Pokepedia.Application` 네임스페이스가 있으면 `Pokepedia.*` 안의 모든 코드에서 `UnityEngine.Application`이 가려져 컴파일 오류가 난다.

## 처음 설정

1. Unity Hub에서 6000.3.26f1 + **Web Build Support** 모듈 설치
2. 스프라이트 생성 (이미지는 저장소에 없음, PokeAPI/sprites 커밋 SHA로 고정) → [Tools/sprites/README.md](../Tools/sprites/README.md)
   ```sh
   py -3 -m venv Tools/sprites/.venv
   Tools/sprites/.venv/Scripts/python -I -m pip install -r Tools/sprites/requirements.txt
   Tools/sprites/.venv/Scripts/python -I Tools/sprites/build_sprites.py
   ```
3. Unity로 프로젝트 열기. 처음 한 번 메뉴 **Pokepedia → Setup Project** 실행(플레이어 설정, 폰트 아틀라스, 진입 씬, 합성 사운드 굽기)

## 테스트

```sh
dotnet test Tests.Dotnet          # Unity 없이 (CI 와 동일)
unity test . --mode EditMode      # Unity Test Runner
```

`Tests.Dotnet/`은 `Assets/Scripts/{Domain,Content,Application}`과 `Assets/Tests/EditMode`의 소스를 복사 없이 링크해 `dotnet test`로 돌린다. UnityEngine을 참조하지 않으므로 순수 C# 영역이나 테스트에서 실수로 `using UnityEngine;`을 쓰면 dotnet 빌드가 바로 깨진다. 언어 버전도 C# 9로 고정해 Unity에서 컴파일되지 않는 최신 문법을 먼저 잡는다. CI(`.github/workflows/domain-tests.yml`)의 실행 조건과 테스트 기준은 [CLAUDE.md](../CLAUDE.md).

## 웹 빌드와 로컬 실행

```sh
# Release (Brotli, Runtime Speed, LTO 없음). Dev 는 WebBuild.Dev
"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod Pokepedia.EditorTools.WebBuild.Release -logFile Logs/build-web.log
# 사전 압축 파일(.br)에 맞는 헤더로 서빙
python -I Tools/web/serve.py --dir Builds/Web --port 8001 --ancestors http://localhost:8000
# 모의 Pokepedia (iframe + ready/init 왕복 테스트)
python -I Tools/web/serve.py --dir Tools/web/host-test --port 8000
```

- 단독 실행: `http://localhost:8001/` → Demo 모드
- iframe 실행: `http://localhost:8000/` → `?host=pokepedia`로 열려 `ready` → `init` 왕복
- `serve.py --mbps 9 --latency-ms 60`으로 Fast 4G 근사 속도에서 로딩을 잰다.
- 같은 와이파이의 휴대폰으로 열려면 `--host 0.0.0.0`을 붙이고(Windows 방화벽 허용 필요) 휴대폰에서 `http://<PC의 IP>:<포트>/?fps`를 연다. `?fps`는 왼쪽 위에 FPS와 가장 느린 프레임(ms)을 보여 준다.

## 배포 (Cloudflare Pages)

```sh
# Release 빌드 뒤. wrangler 로그인 필요(npx wrangler login)
python -I Tools/web/deploy.py                 # 본 배포 → https://pokepedia-battle.pages.dev
python -I Tools/web/deploy.py --branch test   # 미리보기 → https://test.pokepedia-battle.pages.dev
```

헤더 규칙은 `Tools/web/_headers`(PROJECT.md 10.3). 로컬 `serve.py`도 같은 규칙을 흉내 낸다.
