# Pokepedia ↔ Unity 메시지 규약 (v1)

이 문서가 규약의 **단일 출처**다. Pokepedia(React) 쪽 구현은 이 문서를 따른다.

## 구성

```text
Pokepedia 페이지 (/[locale]/battle)
  └─ <iframe src="https://battle.pokepedia.dev/?host=pokepedia" allow="autoplay">
        Unity Web 빌드 (index.html 템플릿 + HostBridge.jslib + HostBridge.cs)
```

- 방향은 **단방향**이다. React가 보유 목록을 한 번 넘기고, Unity는 결과를 저장하지 않는다.
- 쿠키·인증을 쓰지 않으므로 다른 도메인에 호스팅해도 `postMessage`로 충분하다.

## 모드 판별

| iframe 주소 | 동작 |
|---|---|
| `?host=pokepedia` 없음 | 즉시 **Demo** 모드(피카츄 vs 거북왕). 대기하지 않는다 |
| `?host=pokepedia` 있음 | START 후 `ready`를 보내고 `init`을 **5초** 기다린다 |

- 5초 안에 `init`이 오지 않으면 "연결하지 못했습니다" + **[다시 시도] / [데모 체험]**을 보여 준다. 자동으로 Demo로 바꾸지 않는다.
- [다시 시도]를 누르면 `ready`를 다시 보낸다.
- iframe 여부(`window.parent !== window`)로는 판별하지 않는다. 포트폴리오 허브도 iframe으로 임베드할 수 있기 때문이다.

## 메시지

모든 메시지는 `postMessage`로 보내는 **객체**이며 `type`과 프로토콜 버전 `v`를 가진다.

### Unity → React: `ready`

```json
{ "type": "ready", "v": 1 }
```

- Unity 로딩과 START가 끝났다는 뜻이다. **React는 이 메시지를 받은 뒤에만 `init`을 보낸다.**
- Unity가 재로딩되면 `ready`가 다시 온다. 그때 React는 `init`을 다시 보낸다.

### React → Unity: `init`

```json
{ "type": "init", "v": 1, "locale": "ko", "owned": [ { "id": 25, "shiny": false }, { "id": 9, "shiny": true } ] }
```

| 필드 | 설명 |
|---|---|
| `locale` | `ko` / `en` / `ja`. 그 밖의 값은 `ko`로 처리 |
| `owned` | 사용자가 가진 **스티커 전체**. 배틀 지원 여부는 Unity가 로스터와 교집합으로 판단한다 |

- `v`가 1이 아니면 Unity는 무시한다.
- 보유 목록은 조작될 수 있지만 보상·랭킹에 쓰이지 않으므로 검증하지 않는다.

### Unity → React: `battleEnded` (예정, M6)

```json
{ "type": "battleEnded", "v": 1, "result": "win" }
```

표시용이다. 저장하지 않는다.

## 보안

| 위치 | 규칙 |
|---|---|
| Unity 템플릿 `message` 리스너 | `event.origin`이 허용 목록(`window.pokeHost.allowedOrigins`)에 있을 때만 `init`을 Unity로 전달 |
| Unity → React 전송(jslib) | `targetOrigin`을 `'*'`로 쓰지 않고 허용 목록의 각 origin으로 보낸다(일치하는 곳에만 도착) |
| React 쪽 | `event.origin`이 Unity 호스트(`NEXT_PUBLIC_BATTLE_URL`의 origin)일 때만 `ready`를 처리 |
| Unity 호스트 응답 헤더 | `Content-Security-Policy: frame-ancestors`에 Pokepedia·포트폴리오 허브만 허용 |

허용 목록은 `Assets/WebGLTemplates/Pokepedia/index.html`에 있다. 현재 값: `https://pokepedia.dev`, `https://www.pokepedia.dev`, `http://localhost:3000`(Pokepedia 개발 서버), `http://localhost:8000`(로컬 테스트 페이지). Vercel Preview 도메인은 M6에서 정한다.

## 입력

- iframe 안에서는 방향키·Space의 기본 동작(부모 스크롤)을 막는다.
- 클릭·터치 시 캔버스에 포커스를 준다.
- React 쪽 iframe에는 `allow="autoplay"`를 준다. 첫 소리는 Unity 안의 START 버튼에서 낸다.

## 로컬 테스트

```bash
python -I Tools/web/serve.py --dir Builds/Web --port 8001 --ancestors http://localhost:8000
python -I Tools/web/serve.py --dir Tools/web/host-test --port 8000
```

`http://localhost:8000/`를 열면 모의 Pokepedia 페이지가 게임을 iframe으로 띄운다. 이 페이지에서 `ready` → `init` 왕복, 타임아웃(init 보내지 않기 체크), 스크롤 간섭을 확인할 수 있다.
