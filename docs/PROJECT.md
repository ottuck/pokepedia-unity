# Pokepedia Battle — PROJECT (기획·설계·연동 규약)

> 지금의 사양과 결정, 그리고 이후 작업할 때 알아야 할 것만 적는다. 진행 기록은 남기지 않는다(현재 상태는 [ROADMAP.md](ROADMAP.md), 에이전트 규칙은 [CLAUDE.md](../CLAUDE.md)).
> **[확정]** 합의된 사항 · **[위임]** 에이전트가 정하고 결과를 정리하는 사항 · **[미정]** 결정 전(14장, 에이전트가 임의로 확정하지 않는다).
> 절 번호는 코드 주석이 참조하므로 바꾸지 않는다.

## 요약

- Pokepedia의 **퀴즈 → 띠부띠부씰 컬렉션 → 배틀** 루프 중 배틀을 맡는 Unity 2D 턴제 게임. WebGL 빌드를 `battle.pokepedia.dev`(Cloudflare Pages)에 올리고 Pokepedia가 iframe으로 띄운다.
- 지금 공개: **피카츄(플레이어) vs 거북왕(상대)** 1:1. 로스터 데이터에는 6종이 있고 나머지 4종은 보관 중이다.
- 규칙은 순수 C# 엔진(현대 데이터 기반 단순화 규칙, 명중·급소 포함), 화면은 Unity(2.5D 도트 전투). 시작·VS 화면은 웹 템플릿(HTML)이 그린다.
- 서버·DB 없음. Pokepedia DB를 바꾸지 않는다.

---

## 1. 프로젝트 개요

### 목표

- Pokepedia에서 모은 띠부띠부씰 포켓몬으로 NPC와 싸우는 **Unity + C# 2D 턴제 배틀**. 대규모 포켓몬 게임이 아니라 **제대로 설계하고 연출한 소규모 Unity 포트폴리오**다.
- 웹(WebGL)으로 배포하고 Pokepedia에서 iframe으로 들어온다. 단독(Demo)으로도 플레이할 수 있다.
- 모바일 앱은 내지 않지만 **모바일 브라우저에서 완벽하게 보이는 것**이 목표다 [확정].

### 개발 목적

- C# 실전 학습(Java 경험 활용), 도메인 설계(엔진·데미지·상성·AI), 규칙과 콘텐츠의 분리.
- **Unity를 폭넓게 경험:** 월드/UI 렌더링 분리, Light2D, 후처리, 셰이더, VFX, 카메라 연출. 이 목적은 용량·작업량보다 우선한다 [확정].

### 프로젝트 완료 기준 [확정]

1. 순수 C#으로 게임 규칙을 작성하고 자동화 테스트할 수 있다.
2. Unity에서 UI·애니메이션·사운드·이펙트를 직접 구현했다.
3. 브라우저에서 플레이 가능한 완성형 미니게임을 공개했다.
4. 다른 개발자에게 아키텍처와 설계 의도를 설명할 수 있다.

(포켓몬 수는 기준이 아니다. 네 가지 모두 달성했다.)

---

## 2. 기존 Pokepedia 현황

- 사이트 https://pokepedia.dev · 저장소 https://github.com/ottuck/pokepedia · 설계 문서는 그 저장소의 `docs/system_design.md`, 머지 정책은 그 저장소의 `CLAUDE.md`.
- Pokepedia의 게임은 **실루엣 이름 맞히기 퀴즈**다. 배틀·기술 데이터는 이 저장소가 새로 만든 것이다.

| 항목 | 내용 |
|---|---|
| 스택 | Next.js 16, React 19, TypeScript, Tailwind v4, Zustand, next-intl, Supabase(Postgres·Auth·Storage), Vercel, Vitest |
| 계정 | 게스트(익명)로 시작, Google 연결·병합 |
| 컬렉션 | `user_sticker (user_id, pokemon_id, variant normal\|shiny, quantity)` — 본인 SELECT RLS |
| 포켓몬 데이터 | `pokemon` 테이블(public read): 한/영/일 이름, 타입, 능력치 6종, 아트워크 경로. PokeAPI → 동기화 스크립트 → Supabase |
| 아트워크 | Supabase Storage `pokemon-artwork/normal/{도감번호 3자리}.webp` (배틀 시작·VS 화면도 이것을 쓴다) |
| 환경 | Supabase 프로젝트 하나를 Prod와 Preview가 공유, migration은 main 머지 때 자동 적용 |

---

## 3. 최종 게임 루프 [확정]

```text
Pokepedia 퀴즈 (React)
   ↓ 피카츄 띠부씰이 없는 사용자의 새 판 첫 문제 = 피카츄 (9.7)
띠부씰 획득 화면 → [내 피카츄로 배틀] (첫 피카츄면 금빛 단독 버튼)
   ↓ /[locale]/battle (iframe)
Unity 배틀: 보유 ∩ 플레이어 가능 → NPC와 1:1 → 승리 장면에서 끝
```

- 배틀로 들어오는 길은 **배틀 체험(Demo)** 과 **피카츄 띠부씰**(Collection) 두 가지뿐이다 [확정]. 내비게이션의 "배틀" 메뉴도 같은 페이지다.
- 연결은 **단방향**(React → Unity). 배틀 결과로 스티커·랭킹을 바꾸지 않는다.

---

## 4. 기술 스택

| 구분 | 기술 |
|---|---|
| Engine | Unity 6.3 LTS (6000.3.26f1) — 2027년 12월까지 지원. 6.0 LTS는 2026년 10월 종료라 쓰지 않는다 |
| 언어 | C# 9 고정 (`Tests.Dotnet`이 최신 문법을 먼저 잡는다) |
| 렌더 | **URP 2D**(Light2D·Sprite-Lit·Volume 후처리) |
| UI | uGUI + TextMesh Pro (Galmuri11 Static 아틀라스, 7.7) |
| 데이터 | JSON → `ContentLoader`(순수 C#) |
| 테스트 | NUnit (Unity Test Framework + `dotnet test`) |
| 웹 | Unity Web(WebGL), 사전 압축 Brotli, Cloudflare Pages |
| 서버·DB | 없음. Unity는 인증·DB를 다루지 않는다 |

**렌더·빌드 결정 (M0 PoC 근거)**
- **URP 2D 유지:** Built-in RP가 약 1.2MB 작지만 Unity가 6.5부터 deprecated로 지정했고 2D Light·Sprite-Lit·Volume 후처리·2D Shader Graph를 잃는다.
- 스플래시 로고 텍스처(2.7MB)는 `SplashScreen.show=false`만으로 빠지지 않고 `showUnityLogo=false`까지 꺼야 빠진다(Unity Issue UUM-63958, Won't Fix). `ProjectSetup`이 둘 다 끈다.
- **Data Caching 끔:** Safari는 iframe 안에서 IndexedDB를 지원하지 않고, 같은 origin을 두 탭에서 동시에 로딩하면 IndexedDB 캐시가 깨진 적이 있다. 재방문 캐시는 해시 파일명 + `immutable` HTTP 캐시에 맡긴다.
- 빌드: Brotli, 관리 코드 스트리핑 Minimal, IL2CPP 크기 우선. Release는 Runtime Speed(LTO 없음), Dev는 빌드 속도 우선. 스트리핑 High·Disk Size LTO는 빌드가 7~10분 걸리고 문제 가능성이 있어 쓰지 않는다.
- Unity에서 `record`의 `init`을 쓰려면 `IsExternalInit`을 직접 정의해야 한다(`Assets/Scripts/Domain/IsExternalInit.cs`).

---

## 5. 게임 범위

### 5.1 지원 포켓몬 로스터 [확정]

| 등급 | 포켓몬 | 지금 |
|---|---|---|
| Showcase | 피카츄 #025 (전기) | **공개, 유일한 플레이어** |
| Showcase | 거북왕 #009 (물) | **공개, 상대로만** |
| Standard | 리자몽(불꽃/비행) · 이상해꽃(풀/독) · 팬텀(고스트/독) · 뮤츠(에스퍼, 전설) | 보관: 데이터만 있고 그림은 빌드에서 뺐다(`npcWeight` 0, 10.3) |

- 지원 목록과 역할은 `roster.json`이 정한다(`playable`, `npcWeight`). **엔진은 지원 포켓몬 수를 모르고, 엔진·화면 코드에 포켓몬 이름을 쓰지 않는다.**
- 추가는 로스터 항목을 늘리는 것(데이터 + 스프라이트 + 필요하면 연출 오버라이드)이다. 18타입 상성은 모두 구현돼 있고 기술·이펙트는 로스터가 쓰는 타입만 만든다.
- README·포트폴리오에는 151종 중 일부만 지원한다는 사실을 정확히 적는다.

### 5.2 품질 등급: Showcase / Standard [확정]

- 엔진은 하나이고 **시각적 표현만 다르다.** Showcase도 실제 엔진으로 플레이된다.
- 별도 시스템을 만들지 않는다. 연출 디렉터의 기본 타임라인(접촉 / 발사 / 효과) 위에 종족·기술 단위 **오버라이드 데이터**만 얹는다: `Assets/Content/Data/animation.json` → `AnimationCatalog`.
  - 종 프로필(`animProfile` 키): `windupSec`(준비 시간), `lungePx`(돌진), `hitShakePx`(피격 흔들림), `knockbackPx`(넉백), `mouth`(앞모습에서 입 위치, 물 기술 발사점), `moveFx`(기술 id → 전용 연출 id). 빠진 값은 `defaults`.
  - 기술 전용 공격음 `moveSfx`(맨 위, 기술 id → 소리 id, 8.3). 없는 기술은 타입 공격음.
  - 18타입마다 이펙트 색(primary·secondary).
- Standard는 공통 타임라인 + 타입 색, Showcase는 전용 연출(5.5)과 타이밍에 맞춘 소리.

### 5.3 전투 흐름과 화면 구성 [확정]

트레이너(사람)는 화면에 나오지 않는다. 야생 포켓몬과 마주친 것처럼 진행한다.

```text
로딩 → 시작 화면 (HTML, [▶ 배틀 시작])
   ↓  ?host=pokepedia 면 ready → init 대기 (9.2)
VS 장면 (HTML, 약 2초, 확인 입력으로 건너뜀)
   ↓
"야생의 ○○이(가) 나타났다! 가라! ○○!"
   ↓
행동: 싸우다 / 도망 ── 싸우다 → 기술 → 턴 처리 → (반복)
   ↓
플레이어가 쓰러지면 [○○을(를) 응원한다 / 트레이너가 처리한다]
상대가 쓰러지면 승리 → (상대 후보 1마리면) 승리 장면에서 끝
```

- **시작 화면:** 웹 템플릿이 그린다(5.5). 상태는 로딩 / 시작 / 연결 중 / 연결 실패([다시 시도]·[데모 체험]) / 띠부씰 없음([데모 체험]). 키보드 Z·Enter·Space, 실험실은 L.
- **도망:** 상대 후보가 여럿이면 "무사히 도망쳤다!" 후 상대가 바뀌어 새 배틀(HP 가득). 상대 후보가 1마리(Demo, 지금의 Collection)면 첫 번째는 "도망칠 수 없다!", **두 번째부터 "정말 도망칠까?" [그만둔다 / 계속 싸운다]**(기본 커서는 계속 싸운다, 판마다 다시 센다) — 갇힌 느낌을 막는다.
- **쓰러지면 선택 [확정]:** 데모·첫 Collection 전투에서 져서 다시 도전할 이유는 없으므로, 플레이어가 **쓰러질 때마다** "트레이너, 어떻게 할까?"를 고른다(취소 없음, `BattleSession.faintChoice`).
  - **응원한다:** HP 가득으로 다시 일어나고 이후 주는 피해 ×1.5(`CheerPowerBoost`, 겹치지 않음). 엔진 `Revive`·`Revived`. 이름은 원작 인물 대신 "트레이너의 응원".
  - **트레이너가 처리한다:** 몬스터볼이 상대 머리에 맞아 상대가 쓰러지고 **플레이어 승리**(Collection이면 `battleEnded` 승리). 엔진 `KnockOut`(방금 이긴 쪽을 쓰러뜨려 승패를 뒤집음)·`TrainerThrewBall`. 바로 끝내고 싶은 사용자를 위한 위트 있는 마무리다.
  - 그래서 이 배틀들에서는 패배가 나오지 않는다. 패배([다시 도전])는 `faintChoice`가 꺼진 세션에만 있다.
- **승리:** 상대 후보가 1마리면 결과 메뉴 없이 승리 장면("승부에서 이겼다!", 노을 하늘)에서 끝난다. 나가기는 Pokepedia 메뉴, 단독 주소에서는 새로고침. 상대 후보가 여럿이면 [다음 상대와 싸운다] / [처음으로]. 보상·점수는 없다.
- **NPC AI(난이도 하나):** 기대 데미지(위력·명중·자속·상성) 순위를 매기고 **턴마다 섞는다**: 턴 1·3은 1순위, 2는 2순위, 4는 3순위, 이후 4턴마다 반복(난수 없음, `NpcBattleAi`). 같은 기술만 쓰면 단조로워서다.
- **기술 메뉴:** 큰 타입 색 버튼 3개(`MoveCards`) — 이름·위력·명중이 버튼 안에 있고, 누르면 2px 눌렸다 튀어 오르는 반응 뒤 바로 사용한다. 평소 세 버튼은 같은 모양이고 키보드 방향키로 고를 때만 지금 버튼을 띄운다. 버튼 바깥을 누르면 뒤로(휴대폰엔 X·Esc가 없다). 긴 이름은 글자 간격만 좁히고, 영어 명중은 "Acc", 일본어는 "威力·命中". PP 없음.
- **턴 메시지:** "○○의 ○○!" → 빗나감 / 급소 / 상성 문구 → HP 바 감소 → 기절. 타자기식 출력. 상대는 "야생의 ○○"처럼 별도 키를 쓴다(원작이 구분하고, 영어는 대소문자 때문에 접두어 방식이 안 된다). 키 목록은 `BattleTextKeys`.
- **화면:** 게임보이식 배치(상대 오른쪽 위 앞모습, 플레이어 왼쪽 아래 뒷모습, 이름·`:L50`·HP 바·숫자, 아래 메시지 창과 메뉴).
- **기준 해상도 240×216**(10:9, 160×144의 1.5배 — BW 스프라이트가 최대 112px라서). **정수 배율로만** 키운다(`PixelCanvasScaler`·`PixelCamera`). 문구는 Galmuri11 12px.
- **입력:** 방향키 + Z/X, 클릭, 터치.

### 5.4 개발 방침과 범위

- **Vertical Slice 우선** [확정]: 콘텐츠를 늘리기 전에 피카츄 vs 거북왕을 최종 품질로 완성한다.
- **용량은 최하위 고려** [확정]. 화면 품질에 도움이 되면 용량을 쓴다(후처리용 `PostProcessData` 연결). 호스팅 단일 파일 25 MiB 한도(10.2)만 실제 제약이다.
- **측정값:** 전송 약 13MB, 첫 로딩은 데스크톱 약 3.5초·사용자 휴대폰(4G) 체감 문제없음. 프레임은 데스크톱 75 FPS(모니터 상한), 아이폰 Safari 50~60 FPS → 후처리·조명을 줄이지 않는다. 저사양 안드로이드는 재지 않았다.
- **범위 제외:** 151종 지원, 포켓몬별 고유 애니메이션, 트레이너 등장·인트로 씬, 난이도 선택, 승리 보상·랭킹, 온라인 PvP, 게임 서버, 퀴즈의 Unity 재구현, PP·상태이상, 3D 모델·카메라·조명.
- **향후 확장(보관):** 3:3 배틀과 교체·가방 메뉴(엔진은 파티 구조로 설계돼 있다, 7.2), 로스터 공개·추가. 목록은 ROADMAP "보관".

### 5.5 화면 연출

**두 층:** 시작·VS 화면은 **웹 템플릿(HTML)**, 전투는 **Unity 도트(2.5D)**. 240×216 도트로는 시작 화면의 글자·로고가 촌스러워서, 레트로는 전투 화면부터만 쓴다 [확정].

**HTML 시작·VS 화면** (`Assets/WebGLTemplates/Pokepedia/index.html`)
- 게임 안 VS와 같은 **대각선 대결 구도**: 왼쪽 붉은 번개 폭풍(플레이어) vs 오른쪽 깊은 바다(상대), 빛나는 대각선(위 60% → 아래 40%), 천천히 도는 빛줄기, 불꽃·거품 입자, 크게 마주 선 공식 아트워크(못 받으면 뺀다).
- 엠블럼: 빨간 POKEPEDIA 리본 + **Bangers** BATTLE(진한 입체 그림자·흰 안쪽 테두리·노랑→주황 그라데이션·가끔 쓸고 지나가는 빛, 등장 때 찍히듯 들어오고 속도선). 버튼은 기울어진 두툼한 게임 버튼, 로딩은 줄무늬가 흐르는 기울어진 막대 + TIP, 안내는 노란 테두리 대화 상자.
- 글꼴: 로고 Bangers, 리본·버튼·이름 띠 Black Han Sans, 본문 Noto Sans KR(가나는 Noto Sans JP) — Google Fonts, 필요한 글자 조각만 받고 막히면 시스템 글꼴.
- **VS(view = vs):** 로고·버튼이 사라지고 두 포켓몬이 양쪽에서 밀고 들어오며, 0.62초에 Bangers "VS"가 대각선 위에 찍히고(흰 번쩍임·흔들림) 이름 띠(상대 오른쪽 위 파랑, 플레이어 왼쪽 아래 노랑)가 들어온다. 1.75초부터 0.45초 동안 옅어지며 도트 전투가 드러난다.
- 크기는 모두 게임 폭 기준(cqw)이라 데스크톱·휴대폰에서 같은 짜임이다. 겹침 상자는 `overflow: clip`(hidden은 스크롤 상자라 화면이 바뀔 때 저절로 밀려 있었다).
- **Unity ↔ HTML:** Unity가 상태·문구(로케일 파일)·도감 번호를 보내고(`HostBridge.ShowTitle` → `window.pokeTitle.show`, view = loading|idle|connecting|failed|no_partner|vs|hidden), 버튼은 템플릿이 `SendMessage("App", "OnTitleAction", start|retry|demo)`로 돌려준다 — 클릭 처리 안에서 바로 불러 첫 소리(오디오 잠금 해제)가 막히지 않는다. VS의 소리(등장음·급소 타격음)·시간·건너뛰기(0.3초 뒤 확인 입력)는 Unity(`BattleEventPlayer.PlayHtmlVs`)가 맡는다.
- 에디터에는 HTML이 없어 시작은 단순한 Unity 패널, VS는 도트 `VsIntro`(같은 대각선 구도)로 대신한다.

**Unity 전투 화면: 2.5D + 월드/HUD 분리 [확정]**
- 포켓몬은 2D 스프라이트 그대로, 배경·그림자·카메라·조명·후처리로 입체감을 낸다. 3D는 쓰지 않는다.
- **월드**(`BattleWorld`: `SpriteRenderer`·`PixelCamera`, Light2D·후처리·파티클)와 **HUD**(uGUI Overlay 캔버스: HP·메뉴·메시지)를 나눈다. HUD는 후처리를 받지 않아 글자가 선명하다. 화면 전체 번쩍임만 HUD 맨 위.
- **도트를 깨뜨리지 않는다:** Point 필터 + 정수 배율. 원근은 배치·고정 크기로, 패럴랙스·이동은 정수 픽셀로. 확대는 움직이는 짧은 순간에만(멈춰 있을 땐 항상 1배), 회전은 쓰지 않는다. 정수 배율로 남는 여백은 화면 테두리 색 `#262a35`(카메라 배경·가림 막대·웹 템플릿).
- **배경 "비 오는 저녁"**(`BattleBackdrop`, 코드로 그린 디더링 도트): 하늘·구름·산 2겹·침엽수 2줄·풀밭, 레이어별 패럴랙스. 안개 2겹, 비, 바닥 빗방울 물결, 가끔 번개 번쩍임, 비네트(스프라이트). 포켓몬 아래 디더링 타원 그림자(발판 없음).
- **조명·후처리:** 전역 조명(차가운 푸른빛) + 타입 색 점 조명 `BattleLighting.Pulse`(더하기 블렌드라 밝은 부분만 Bloom). `BattlePostFx`: Bloom(threshold 1.0)과 약한 채도·대비. 배경·포켓몬은 Sprite-Lit, 이펙트·그림자·안개·비·비네트는 Sprite-Unlit.
- **공격 카메라:** 준비 동작 동안 공격자 쪽으로 1.12배, 발사 순간(`ReleaseSec`, 공격음과 같은 시각) 상대 쪽으로 넘어가 1.08배, 맞는 순간 +4%(급소 +8%), 끝나면 가운데·1배. 월드의 Pan 노드를 움직이고 카메라 배율은 고정한다. 확대해도 배경 밖이 안 보이게 중심 이동을 제한한다.
- **기술 연출**(`AnimationDirector`, `moveFx`로 배정):

| id | 기술 | 모습 |
|---|---|---|
| `thunder_arc` | 10만볼트 | 공격자 몸 바깥에서 상대 몸 **표면**까지 뻗는 번개 |
| `hydro_beam` | 하이드로펌프 | 거북왕 **입 바로 앞**(`mouth`, 쏘는 동안 첫 프레임 고정)에서 곧게 뻗는 3겹 물줄기, 표면에서 물보라, 상대가 밀려남 |
| `hydro_geyser` | 물의맹세 | 발밑이 울리며 거품이 끓고, 둘레에서 작은 물기둥 4개 → 발밑에서 큰 물기둥이 솟아 상대를 띄움 |
| `contact` | 몸통박치기(공통 접촉) | 도움닫기 → 잔상과 함께 돌진 → 표면에 도트 별 타격 불꽃·파편·흙먼지 → 잠깐 멈춤 → 복귀 |
| `lightning_strike` | 번개 | 하늘에서 떨어지는 낙뢰 |
| (예비) `thunder_fork`, `hydro_burst` | — | 실험실에서만. 기술에 배정하면 쓸 수 있다 |

  발사·효과형의 공통 타임라인은 구체(projectile)·고리(effect). 끝점은 몸 **표면**이다(가운데로 하면 몸을 뚫은 것처럼 보인다).
- **피격:** `Pokepedia/Sprite Flash` 셰이더 번쩍임 + 흔들림 + 넉백, 급소는 화면 번쩍임·흔들림, 빗나감은 회피 모션.
- **응원 부활:** 카메라가 다가가고 하늘에서 금빛 빛기둥(`VfxLayer.LightShaft`) → 빛 속에서 떠오르며 다시 나타남 → 금빛 화면 번쩍임 → HP가 찬다. (흩날리는 깃털은 240×216에서 작아 나뭇잎처럼 보여 쓰지 않는다.)
- **트레이너가 처리한다:** 화면 왼쪽 아래에서 몬스터볼(코드로 그린 11×11 도트)이 돌며 포물선으로 날아가 상대 머리에 맞고 튕겨 나감 → 번쩍·움찔 → 머리 위 별이 돌며 비틀 → HP가 0으로 → 기절(`AnimationDirector.TrainerBall`).
- **승리:** 1.4초에 비·안개·구름이 걷히고 노을 하늘 레이어가 나타나며 전역 조명이 따뜻해진다. 이긴 쪽이 빛 속에서 세 번 뛰고 카메라가 다가간다(쓰러진 쪽이 이긴 경우엔 날씨만). 다음 판에서 날씨가 돌아온다.
- **HP UI:** 둥근 모서리 두 겹 테두리 상자, 포켓몬 쪽 받침선, 도트 "HP:" 표, 숫자 "126/ 126".
- **연출 실험실:** 주소에 `?lab`(또는 시작 화면에서 L). 전용 연출(10만볼트 3종·하이드로펌프·물의맹세·몸통박치기 양쪽), 몬스터볼(Ball), 응원 부활(Revive), VS, 승리를 반복 재생하고 결과 버튼으로 명중 → 빗나감 → 급소를 돌린다. 소리도 실제 배틀과 같이 난다(Tackle = 몸통박치기 전용음, WP = 물기둥 솟구침). HP는 바뀌지 않는다. `?fps`는 FPS와 가장 느린 프레임을 보여 준다(`?lab&fps` 가능). 고른 결과는 `animation.json`의 `moveFx`에 적는다.

---

## 6. 전투 규칙과 데이터 설계

### 6.1 원칙 [확정]

- 원작 재현이 목표가 아니다. 엔진은 **하나**, 포켓몬은 **데이터**로만 표현한다(포켓몬별 클래스·스크립트 없음).
- 데이터는 **현대 기준**(18타입, 능력치 6종, 특공/특방 분리), 규칙은 직접 정한 단순화 규칙. 게임보이 감성은 화면·소리로 낸다.
- 기술 이름은 장식이고 규칙은 데이터(타입·위력·명중·분류)가 정한다.

### 6.2 포켓몬 데이터

- `Assets/Content/Data/species.json`, `moves.json`, `roster.json`, `animation.json`. 로더 `ContentLoader`(순수 C#, 엄격한 스키마: 누락·모르는 필드·잘못된 값은 항목 이름을 담은 `FormatException`).
- 종족값·타입은 **PokeAPI** `/pokemon/{id}`(Pokepedia DB와 같은 출처)로 확인해 적었다. 런타임 DB 조회는 없다.
- 이름은 엔진에 없다. `nameKey`(`species.025`, `move.thunderbolt`)로 로케일(`Assets/Content/Locale/{ko,en,ja}.json`)에서 찾는다. 이름은 PokeAPI ko / en / ja-Hrkt 값.
- `roster.json`: `id`, `tier`(showcase|standard), `npcWeight`(상대로 나올 가중치, 0이면 안 나옴), `playable`(플레이어로 쓸 수 있음).

```json
// species.json
{ "id": 25, "types": ["electric"],
  "baseStats": { "hp": 35, "attack": 55, "defense": 40, "spAttack": 50, "spDefense": 50, "speed": 90 },
  "sprites": { "front": "front_25_sheet", "back": "back_25_sheet", "frontShiny": "front_25_shiny_sheet", "backShiny": "back_25_shiny_sheet" },
  "animProfile": "pikachu", "nameKey": "species.025", "legendary": false }
// roster.json (지금)
[ { "id": 25, "tier": "showcase", "npcWeight": 10, "playable": true }, { "id": 9, "tier": "showcase", "npcWeight": 10 },
  { "id": 6, "tier": "standard", "npcWeight": 0 }, ... ]
```

### 6.3 기술 데이터

- 직접 정의한다(Pokepedia에 기술 데이터 없음). **노말 기본기 1개 + 로스터 타입별 대표 기술 + 단일 타입 종의 보조 기술**, 지금 12개.
- 각 포켓몬은 **기본기 + 속성 기술 2개 = 3개** [확정], 타입에서 자동 배정: 기본기 → 종의 타입 순서대로 대표 기술 → (단일 타입이면) 그 타입의 보조 기술. 예: 피카츄 = 몸통박치기·10만볼트·번개, 리자몽 = 몸통박치기·화염방사·날개치기.
- 필드: `id, type, category(physical|special), power, accuracy, nameKey, animKind(contact|projectile|effect), role(basic|signature|secondary)`. 기본기는 정확히 1개, 대표·보조는 타입마다 최대 1개.
- 보조 기술은 대표 기술보다 기대 데미지(위력×명중)가 낮게 잡는다 — "세지만 빗나가는 기술"(번개)과 "약하지만 반드시 맞는 기술"(물의맹세·염동력) 같은 선택지.
- 수치는 원작보다 낮춰 전투를 4~6턴으로 맞춘다(6.5). 원작 기술 이름을 빌려도 고정 피해·2턴 기술처럼 단순 위력 계산과 맞지 않는 기술은 쓰지 않는다.

| id | 이름(ko) | 타입 | 분류 | 위력 | 명중 | animKind | role |
|---|---|---|---|---|---|---|---|
| tackle | 몸통박치기 | 노말 | 물리 | 16 | 100 | contact | basic |
| thunderbolt | 10만볼트 | 전기 | 특수 | 35 | 100 | projectile | signature |
| hydro_pump | 하이드로펌프 | 물 | 특수 | 45 | 85 | projectile | signature |
| flamethrower | 화염방사 | 불꽃 | 특수 | 35 | 95 | projectile | signature |
| wing_attack | 날개치기 | 비행 | 물리 | 35 | 95 | contact | signature |
| razor_leaf | 잎날가르기 | 풀 | 물리 | 35 | 95 | projectile | signature |
| sludge_bomb | 오물폭탄 | 독 | 특수 | 35 | 95 | projectile | signature |
| shadow_ball | 섀도볼 | 고스트 | 특수 | 35 | 95 | effect | signature |
| psychic | 사이코키네시스 | 에스퍼 | 특수 | 35 | 95 | effect | signature |
| thunder | 번개 | 전기 | 특수 | 45 | 70 | effect | secondary |
| water_pledge | 물의맹세 | 물 | 특수 | 30 | 100 | effect | secondary |
| confusion | 염동력 | 에스퍼 | 특수 | 28 | 100 | effect | secondary |

(타입·분류는 PokeAPI `/move/{name}`과 일치. 물의맹세의 원작 "맹세" 합체 효과는 넣지 않는다.)

### 6.4 전투 규칙 [확정]

공식은 현대 포켓몬(능력치 3세대 이후, 데미지 6세대 이후) 기준. 출처: Pokémon Showdown 소스(`sim/battle.ts` statModify·modify, `sim/battle-actions.ts` getDamage·modifyDamage), 상성은 PokeAPI `type` damage_relations와 Pokémon Database 표(324칸 일치). 코드 주석에 URL이 있다.

- **능력치:** 레벨 50, 개체값 31, 노력치 0, 성격 보정 없음. 종족값은 먼저 압축(6.5).
  - HP = ⌊(2×종족값+31)×레벨/100⌋ + 레벨 + 10 / 그 외 = ⌊(2×종족값+31)×레벨/100⌋ + 5
  - 실제 값(k=0.65): 피카츄 126 / 84 / 74 / 81 / 81 / 107, 거북왕 154 / 102 / 113 / 103 / 116 / 99 (`StatCalculatorTests`).
- **물리/특수:** 기술의 `category`. physical은 공격÷방어, special은 특공÷특방.
- **턴 순서:** 스피드가 높은 쪽 먼저, 같으면 무작위.
- **행동:** 명중 판정(기술 명중률, 회피·명중 단계 없음) → 빗나가면 `MoveMissed` → 급소 판정 → 데미지.
- **급소:** 1/16, ×1.5.
- **데미지:** 단계마다 정수 처리(`DamageCalculator`, 순서가 결과에 영향):
  1. 기본 = ⌊⌊⌊⌊2L/5+2⌋ × 위력 × A⌋ / D⌋ / 50⌋ + 2
  2. 급소 ⌊×1.5⌋ → 3. 난수 ⌊×R/100⌋(R 85~100) → 4. 자속 ×1.5(4096 고정소수점, 0.5는 내림) → 5. 상성 ⌊×배수⌋(이중 타입은 곱)
  6. 상성 0이면 0, 그 외 최소 1. 응원 부활 뒤에는 상성 다음에 ×`PowerBoost`.
- **난수 소비 순서**(테스트로 고정): 스피드 동률일 때만 `Next(2)` → 행동마다 명중률 < 100이면 `Next(100)` → 명중하면 급소 `Next(16)` → 데미지 `Next(16)`(85 + 값). 모두 `IRandom` 주입.
- 상성 0이면 급소가 떠도 급소로 표시하지 않는다. 피해 이벤트는 실제로 깎인 HP다. HP 0이면 기절.
- **제외:** PP, 상태이상, 우선도, 능력치 단계, 날씨.
- **NPC AI:** 5.3.

### 6.5 밸런스와 NPC 선택

- **전투 길이** 약 4~6턴. HP 배율 규칙 없이 **기술 위력(데이터)** 으로 맞춘다. 모든 모드가 같은 규칙·데이터를 쓴다.
- **종족값 압축:** `stat' = 80 + (종족값 − 80) × k`, 사사오입, 최소 1, **k = 0.65**. 종족값 합 격차(피카츄 320 ~ 뮤츠 680)를 줄인다.
- **Demo 목표 [확정]:** 피카츄 승률 **85~93%**, 평균 4~6턴(`BalanceTests`, 5,000판 시드 고정, 피카츄는 가장 센 기술만 / NPC는 실제처럼 기술을 섞음). 지금 90.9%·4.95턴. 60~70%는 한 번 빗나가면 지는 아슬아슬한 판이라 올렸다.
- 지금 값의 이유: 10만볼트 명중 100·하이드로펌프 명중 85(믿음직한 기술 vs 세지만 빗나가는 기술), NPC가 약한 기술을 섞는 만큼 하이드로펌프 위력 45·물의맹세 30.
- **수치가 민감하다:** 확정 n타가 한 단계 바뀌면 승률이 크게 뛴다. 조정할 때는 한 점이 아니라 주변까지 목표 범위가 유지되는 구간의 중심을 고른다. 조정용 표 출력(k × 위력)은 커밋 `cd45839`의 `BalanceTests.cs`에서 꺼내 임시로 쓴다.
- **상대 선택(Collection):** `npcWeight` 비례 가중 추첨(`OpponentPicker`), 플레이어와 같은 종 제외. 전설은 가중치를 낮게(다시 열 때 임시값 뮤츠 2 / 나머지 10). 상대 후보가 1마리면 도망을 끈다.
- 중복 스티커는 능력치에 영향 없음, shiny는 외형만.

---

## 7. 아키텍처

### 7.1 설계 원칙 [확정]

1. 엔진(규칙)과 콘텐츠(데이터·에셋·텍스트)를 분리한다. 엔진은 ID와 숫자만 안다.
2. Domain·Content·Application은 `UnityEngine`을 참조하지 않는다(asmdef와 `Tests.Dotnet`이 강제).
3. 난수는 `IRandom`으로 주입한다.
4. 규칙을 일반화하지 않는다(`IRuleSet` 등 금지). 분리 대상은 콘텐츠다.
5. 모든 클래스에 인터페이스를 만들지 않는다. DI 프레임워크·Repository·이벤트 버스·ECS를 쓰지 않는다.
6. **엔진은 지원 포켓몬 수를 모른다.**
7. 우선순위: 배틀 로직의 독립성, 테스트 용이성, 화면과 도메인의 책임 분리.

### 7.2 계층

```text
Presentation (Unity)  ← 시작 화면 연결, 배틀 화면 흐름·이벤트 재생·연출·오디오 (규칙 판단 금지)
       ↓
Application (C#)      ← BattleSession(흐름: NPC 행동, 다음 상대·다시 도전·도망·쓰러지면 선택), NpcBattleAi, 메시지 키, 호스트 연결
       ↓
Domain (Pure C#)      ← BattleEngine, DamageCalculator, TypeChart, BattleState
       ↑ 읽기 전용 데이터
Content               ← species·moves·roster·animation.json, locale, 스프라이트
Bridge (Platform)     ← IHostBridge 구현: WebGL(jslib ↔ postMessage·시작 화면) / 에디터(로그만)
```

- Domain: 입력 `BattleState` + 행동, 출력 `BattleEvent` 목록. 파티(여러 마리) 구조이지만 교체는 3:3 확장 때 넣는다.
- `Pokepedia.Application` 어셈블리의 네임스페이스는 `Pokepedia.App`이다(`UnityEngine.Application` 가림 방지).

### 7.3 엔진 ↔ 화면: 이벤트 로그와 연출 디렉터

정의는 `Assets/Scripts/Domain/Battle/BattleEvents.cs`. Side 0 = 플레이어, 1 = NPC.

```csharp
PokemonSentOut(int Side, int SpeciesId, int Hp, int MaxHp)   // NPC 먼저, 다음 플레이어
TurnStarted(int Turn)
MoveUsed(int Side, string MoveId)
MoveMissed(int Side, string MoveId)
DamageDealt(int TargetSide, int Amount, int HpAfter, double Effectiveness, bool Critical)
Fainted(int Side)
BattleEnded(int WinnerSide)
Revived(int Side, int Hp, int MaxHp)        // 응원 부활
TrainerThrewBall(int TargetSide)            // 트레이너가 처리한다 (뒤에 Fainted·BattleEnded)
```

엔진 API: `new BattleEngine(state, rules, random)` → `Start()` → `PlayTurn(playerAction, npcAction)`(한쪽이 쓰러지면 그 자리에서 끝), 그리고 `Revive(side, boost)`·`KnockOut(side)`. 행동은 `UseMove(moveIndex)`뿐이고 도망은 Application이 처리한다.

```text
BattleEngine ← BattleSession
     │ BattleEvent 목록
     ▼
BattleScreen (흐름·표시 상태) ── BattleHud (HUD·월드·메뉴) / BattleLab (실험실)
     ▼
BattleEventPlayer (메시지 + 연출 재생, VS)
     ▼
AnimationDirector ── 기본 타임라인 + AnimationProfile 오버라이드 → VfxLayer, Light, Camera, Audio
```

- `BattleScreen`은 흐름과 **표시 상태 `BattleViewState`**(`Hidden → Entering → Command → Animating → Result`, 그리고 `ConfirmQuit`·`ChooseFaint`·`Lab`)만 다룬다. 입력 처리기는 맞는 상태에서만 동작한다. 판 데이터(카탈로그·로케일·이름·세션·브리지)는 `BattleContext` 하나에 모은다.
- 호스트 연결(`init` 대기)과 플레이어 포켓몬 정하기는 배틀 화면 전에 `TitleScreen`이 맡는다. 플레이어 후보가 2마리 이상이 되면 선택 상태를 `Entering` 앞에 더한다.
- 연출을 바꿔도 엔진은 바뀌지 않는다. 테스트는 이벤트 목록만 단언하고, 메시지는 이벤트 → 로케일 키 → 문자열 순으로 만든다(`BattleMessages`).

### 7.4 Java 개발자를 위한 C# 대응표

| Java | C# |
|---|---|
| `record` | `record` (C# 9+) |
| `sealed interface` + 패턴 매칭 | `abstract record` + `switch` |
| `Optional<T>` | nullable (`T?`) |
| 패키지 | namespace + Assembly Definition(.asmdef) |
| JUnit | NUnit |
| Spring Bean 주입 | 생성자 주입 직접 구현 |
| 스레드/Future | 코루틴, `async/await` (Unity 메인 스레드 제약) |

### 7.5 디렉터리 구조

실제 구조와 어셈블리 의존 방향은 [README.md](../README.md)의 "구조"가 단일 출처다. asmdef는 5개 이내로 유지한다.

### 7.6 지원 포켓몬 목록의 출처

단일 출처는 Unity `roster.json`이다. Pokepedia는 플레이어 가능 ID를 코드 상수 `BATTLE_PLAYABLE_IDS`(지금 `[25]`, `src/features/battle/host.ts`)로 두고 출처를 주석으로 적는다. Pokepedia 서버 경로에서 Unity 호스트를 런타임에 부르지 않는다. 플레이어 가능 포켓몬을 바꾸면 두 곳을 함께 고친다.

### 7.7 폰트 [확정: Galmuri11 + TMP Static]

- 게임 안 글자는 **Galmuri11**(한글·가나·JIS 한자, OFL-1.1, 고지 `Assets/Content/Fonts/Galmuri-OFL.txt`). 11 Bold는 가나·한자·♀♂가, 11 Condensed는 한자가 없어 쓰지 않는다.
- 출력 문자열이 정해져 있으므로 **필요한 글리프만** 담은 TMP **Static** 아틀라스(12pt, `RASTER_HINTED`, 512×512)를 쓴다. Dynamic은 원본 TTF(5.4MB)가 빌드에 들어가 쓰지 않는다.
- **로케일 문구에 새 글자가 생기면 아틀라스를 다시 굽는다:** `Pokepedia.EditorTools.FontAtlasBuilder.Build`(로케일 JSON + ASCII + 고정 기호). 빠진 글자는 □로 깨진다.
- 시작·VS 화면(HTML)은 웹 글꼴을 쓴다(5.5).

### 7.8 테스트 전략

- **꼭 필요한 최소한만** 둔다: 깨지면 실제 버그가 되는 규칙·계약(데미지·상성·능력치, 전투 흐름, 데이터 검증, 연동 규약, 어셈블리 경계). 같은 규칙은 대표 사례 몇 개로 묶는다.
- Domain 테스트는 코드로 만든 가짜 종·기술(`TestMon`, `TestMove`)만 쓴다. 실제 JSON은 `ContentDataFilesTests`·`BalanceTests`만 읽는다.
- `IRandom` 스텁으로 결정적 테스트를 만든다.
- 화면·연출은 자동화하지 않는다(Actions 한도). 실험실과 브라우저에서 눈으로 확인하고, 배포 전 Release 빌드로 한 판을 해 본다.

---

## 8. 아트·에셋 전략

### 8.1 방침

- 포켓몬 도트는 **PokeAPI 스프라이트**, 시작·VS 화면은 Pokepedia Storage의 공식 아트워크. 권리는 Pokepedia와 같은 수준(비공식·비영리 팬 프로젝트 고지)으로 하고 별도 검토는 하지 않는다. 다른 원작 에셋은 쓰지 않는다.
- **저장소에 스프라이트를 커밋하지 않는다** [확정]: `Assets/Resources/Sprites/`는 `.gitignore`. `Tools/sprites`의 스크립트와 **PokeAPI sprites 커밋 SHA로 고정한 매니페스트**만 커밋하고, clone 후 스크립트로 재현한다(README).
- 트레이너 그림은 쓰지 않는다. PokeAPI는 트레이너 그림을 주지 않는다(sprites 저장소에는 badges·items·pokemon·types뿐).

### 8.2 스프라이트 스타일 [확정: 5세대 블랙·화이트 애니메이션]

- 소스: PokeAPI sprites `versions/generation-v/black-white/animated/`(앞, `back/`, `shiny/`, `back/shiny/`).
- `Tools/sprites`가 GIF를 프레임 분해 → 시트 PNG(`--dedupe --indexed`) + 프레임 지연 JSON으로 만든다. 런타임에 이름으로 `Resources.Load` 해서 `SpriteAnimator`가 재생한다(프레임 지연이 불균일하고 프레임이 많아 Animator 클립은 쓰지 않는다). 임포트 설정(Point·무압축·밉맵 없음)은 `SpriteSheetImporter`가 강제한다.
- 매니페스트에는 지금 피카츄·거북왕만 있다. Standard 4종을 다시 쓰려면 `fetch_sprites.py --update-manifest --ids 25,9,6,3,94,150` 뒤 `build_sprites.py`(같은 PokeAPI 커밋).
- 발 위치 pivot, 정수 배율, Point 필터. 다른 스타일 후보 비교는 [`sprite-style-compare.html`](sprite-style-compare.html).

### 8.3 사운드 전략 [확정]

- **모든 소리를 이 저장소의 코드로 직접 합성한다**(외부·AI·원작 음원 없음). 목록·레시피는 `Assets/Content/Audio/manifest.md`.
- **속성 공격음:** 기술을 쓸 때 나는 소리(전기 = 낙뢰, 물 = 물대포 등, 시드 고정). 포켓몬 울음소리가 아니다. 고르지 않은 안은 두 번째 기술용으로 `TypeAttackSounds.Alternates`에 보관.
- **기술 전용 공격음:** 같은 타입의 두 기술이 같은 소리를 내지 않게 `animation.json`의 `moveSfx`로 기술마다 소리를 지정할 수 있다. 쓸 수 있는 소리는 새로 만든 것(`TypeAttackSounds.MoveOnlyIds`)과 보관한 안(`AlternateIds`)이고, 없는 id면 굽기(`AudioBaker`)가 멈춘다. 지금: 몸통박치기 = 전용 "슈욱 → 퍽·쿵 → 흙먼지"(충돌이 돌진 0.09초에 맞춤), 물의맹세 = 전용 "촤아"×4 + 큰 "촤아아"(물기둥이 솟을 때마다 한 번, 연출 시간표 그대로). 물소리는 거품 공명(짧게 위로 미끄러지는 사인) + 출렁이는 대역 노이즈로 만들고, 소리마다 45ms 에 걸쳐 곡선으로 차오르게 한다(10ms 안팎에 터졌다 줄어드는 노이즈·쿵 저음은 총소리처럼 들린다). 하이드로펌프(물대포)·10만볼트·번개(둘 다 낙뢰)는 타입 공격음.
- **울음소리(등장 소리)는 넣지 않는다.** PokeAPI 원본은 어색했다. 넣을지는 14장.
- **배틀 BGM:** 직접 작곡한 8비트 루프 1곡(`BattleMusic`). 판마다 처음부터, 승패가 나면 줄이고 징글. WebGL은 소리를 AAC로 바꾸며 앞부분을 건드려 루프 이음새가 튈 수 있어 앞에 무음 2048 샘플 + WAV smpl 루프 구간을 둔다.
- **WebGL에서는 실행 중 합성이 화면을 멈추므로** 에디터가 미리 굽는다(`AudioBaker` → `Assets/Resources/Audio/`, 커밋 안 함). 짧은 소리는 iOS 디코드 문제 때문에 최소 0.25초로 채운다.
- **타이밍이 핵심:** 공격음은 발사 순간(`ReleaseSec`)보다 소리마다 정한 만큼 먼저 틀어 터지는 순간을 맞춘다. 준비가 길면 그 앞을 기 모으기 소리로 채운다.
- 브라우저는 사용자 입력 전 오디오를 막으므로 첫 소리는 시작 버튼 처리 안에서 낸다(5.5).

---

## 9. Pokepedia(React) 연동

### 9.1 방식: iframe + postMessage [확정]

- 페이지 **`/[locale]/battle`** 에 iframe으로 넣는다. 쿠키·인증을 쓰지 않으므로 다른 도메인이어도 `postMessage`로 충분하다. origin 검증 필수.

### 9.2 메시지 규약 (v1) [확정]

이 절이 Pokepedia ↔ Unity 규약의 **단일 출처**다.

#### 구성

```text
Pokepedia 페이지 (/[locale]/battle, BattleFrame)
  └─ <iframe src="https://battle.pokepedia.dev/?host=pokepedia&lang=ko" allow="autoplay">
        Unity Web 빌드 (index.html 템플릿 + HostBridge.jslib + HostBridge.cs)
```

- **단방향:** React가 보유 목록을 한 번 넘기고 Unity는 결과를 저장하지 않는다.
- 컬렉션 조회는 인증된 React(서버/RLS)가 한다. **보유 ∩ 지원 계산은 Unity가 한다**(React는 보유 전체를 보낸다 — 로스터가 늘어도 규약이 바뀌지 않는다).
- 전달된 보유 목록은 조작될 수 있지만 보상·랭킹에 쓰이지 않아 검증하지 않는다.

#### 모드 판별

| iframe 주소 | 동작 |
|---|---|
| `?host=pokepedia` 없음 | 시작하면 바로 **Demo**(피카츄 vs 거북왕) |
| `?host=pokepedia` 있음 | 시작 후 `ready`를 보내고 `init`을 **5초** 기다린다 |

- 5초 안에 `init`이 없으면 "연결하지 못했습니다" + [다시 시도]([`ready` 재전송]) / [데모 체험]. **자동으로 Demo로 바꾸지 않는다.**
- iframe 여부로는 판별하지 않는다(포트폴리오 허브도 iframe으로 띄울 수 있다). 쿼리는 jslib로 `location.search`를 읽는다.
- `lang`(`ko`/`en`/`ja`)은 로딩·시작 화면의 언어다. 없으면 브라우저 언어(모르면 한국어). 연결 뒤 게임 안 언어는 `init.locale`.

| 모드 | 플레이어 | 상대 |
|---|---|---|
| **Demo** | 피카츄 고정 | 거북왕 고정 |
| **Collection** | 보유 ∩ `playable` (지금 피카츄뿐) | `npcWeight` 추첨 (지금 거북왕뿐) |

- 보유 ∩ 플레이어 가능이 **1마리면 선택 없이 바로 VS**, **0마리면** "퀴즈에서 피카츄 띠부씰을 얻으면 내 포켓몬으로 배틀할 수 있어요" + [데모 체험]. 2마리 이상의 선택 화면은 플레이어 포켓몬이 늘 때 만든다.
- 보유한 피카츄가 shiny 스티커면 배틀에서도 shiny 도트(외형만).
- 엔진은 같고 포켓몬 선택만 다르다.

#### 메시지

모든 메시지는 `type`과 프로토콜 버전 `v`를 가진 객체다. 요청 ID는 없다.

| 방향 | 메시지 | 뜻 |
|---|---|---|
| Unity → React | `{ "type": "ready", "v": 1 }` | 로딩과 시작이 끝났다. **React는 이것을 받은 뒤에만 `init`을 보낸다.** Unity가 다시 로딩되면 또 온다 |
| React → Unity | `{ "type": "init", "v": 1, "locale": "ko", "owned": [ { "id": 25, "shiny": false } ] }` | `locale` ko/en/ja(그 밖은 ko), `owned`는 스티커 전체. `v`가 1이 아니면 무시 |
| Unity → React | `{ "type": "battleEnded", "v": 1, "result": "win" }` | Collection에서 승패가 날 때마다(`win`/`lose`, 도망은 안 보냄). 표시용, 저장 안 함 |

#### 보안

| 위치 | 규칙 |
|---|---|
| 템플릿 `message` 리스너 | `event.origin`이 허용 목록(`window.pokeHost.allowedOrigins`)에 있을 때만 `init` 전달 |
| Unity → React(jslib) | `targetOrigin`을 `'*'`로 쓰지 않고 허용 목록의 각 origin으로 보낸다 |
| React | `event.origin`이 Unity 호스트(`BATTLE_ORIGIN`)일 때만 `ready` 처리 |
| Unity 호스트 헤더 | `frame-ancestors`에 Pokepedia·포트폴리오 허브·Vercel Preview·`localhost:3000`만 |

- 허용 목록(템플릿): `https://pokepedia.dev`, `https://www.pokepedia.dev`, `http://localhost:3000`(Pokepedia 개발 서버), `http://localhost:8000`(로컬 모의 페이지), 그리고 **이 계정의 Pokepedia Preview 정규식** `^https://pokepedia-[a-z0-9-]+-vison901-gmailcoms-projects\.vercel\.app$`(PR마다 주소가 바뀐다). `frame-ancestors`는 한 단계 와일드카드만 되므로 `https://*.vercel.app`을 넣는다 — 다른 Vercel 사이트도 띄울 수는 있지만 공개 게임이라 영향이 없고 `init`은 위 정규식에서만 받는다.
- Pokepedia에는 CSP 헤더가 없어 iframe을 막지 않는다. 다른 포트(예: 3100)의 개발 서버는 `frame-ancestors`에 없어 게임이 뜨지 않는다.

#### 입력과 크기

- iframe 안에서 방향키·Space의 기본 동작(부모 스크롤)을 막고, 클릭·터치 때 캔버스에 포커스를 준다. iframe에는 `allow="autoplay"`.
- **크기는 Pokepedia가 정한다** (`BattleFrame`, `fitBattleScreen`): 데스크톱은 빨간 도감 기기 프레임 안에서 iframe을 화면 배율(DPR)에 맞는 **정수 배율**로 딱 맞게 잡는다(최대 720 CSS px, 창 높이 안). 더 크게 키우면 240×216 그림의 글자·디더링이 깨져 보인다. 휴대폰은 가장자리까지 채운다. 피카츄·거북왕만 고해상도로 바꾸는 방법은 도트 크기가 섞이고 문제가 화면 전체라 쓰지 않는다 — 크게 보이면서 매끄러우려면 게임 해상도를 올려야 한다(보관).

#### 로컬 테스트

```bash
python -I Tools/web/serve.py --dir Builds/Web --port 8001 --ancestors http://localhost:8000
python -I Tools/web/serve.py --dir Tools/web/host-test --port 8000
```

`http://localhost:8000/`의 모의 Pokepedia 페이지에서 `ready` → `init` 왕복, 타임아웃(init 보내지 않기), 스크롤 간섭, 보유 상황(피카츄 / shiny / 없음)별 진입, `battleEnded` 수신을 확인한다.

### 9.3 DB 정책 [확정]

**Pokepedia DB는 바꾸지 않는다.** 새 테이블·RPC·컬럼이 없다. 읽기만 한다. 꼭 필요해지면 additive만(Prod·Preview가 같은 DB, migration 자동 적용).

### 9.4 Pokepedia 쪽 구현 (그 저장소)

- 내비게이션 "배틀", 페이지 `/[locale]/battle`(`BattleFrame`: 메시지 처리·크기 계산), `src/features/battle/host.ts`(`BATTLE_ORIGIN`, `BATTLE_PLAYABLE_IDS`, `initFor`, `fitBattleScreen`).
- 퀴즈·컬렉션 연동은 9.6·9.7.
- 문구는 `messages/ko·en·ja.json` 세 곳에 모두(키 일치 테스트가 있다). 구조 변경은 그 저장소의 `docs/system_design.md`를 먼저 고치고 그 저장소 `CLAUDE.md`의 머지 정책을 따른다.

### 9.5 조작 가능성

규칙이 클라이언트에 있어 결과를 검증할 수 없지만 **보상·랭킹에 쓰이지 않으므로** 조작은 본인 경험에만 영향을 준다. 서버 검증은 하지 않는다. 보상·공식 랭킹을 추가하면 다시 본다.

### 9.6 컬렉션의 배틀 가능 표시

- 배틀 가능 포켓몬(피카츄) 카드는 **금색 테두리 + "배틀" 배지**로 보여 주고 배틀로 들어가는 입구가 된다. shiny 스티커의 노란(`volt`) 테두리와 구분한다.
- `전체 | 배틀 가능` 필터는 선택 사항으로 남겨 두었다(14장).

### 9.7 첫 퀴즈 피카츄 [확정]

- **피카츄 띠부씰이 없는 사용자**의 새 판 **첫 문제만** 피카츄 실루엣으로 낸다. 서버(`startOrResume`)가 이미 있는 `user_sticker`를 읽어 판단한다 — DB 변경이 없고 조작할 수 없고 기기가 바뀌어도 같다. (브라우저 저장소·DB 컬럼 안은 이 이유로 쓰지 않았다.)
- 피카츄로 정한 이유: Demo(피카츄 vs 거북왕)와 그대로 맞아 매치업·밸런스·연출을 바꿀 필요가 없다. 첫 문제 하나·미보유 사용자만이라 퀴즈 반복·편중 부작용이 거의 없다.
- 맞히면 띠부씰 획득 화면에 **[내 피카츄로 배틀]** (첫 피카츄면 금빛 단독 버튼). 진행 중인 퀴즈는 남아 돌아오면 이어서 한다.

---

## 10. 웹 배포 인프라

### 10.1 개요

```text
브라우저
 ├─ Pokepedia (Vercel) ── iframe ──▶ Unity 정적 호스팅 (Cloudflare Pages `pokepedia-battle`, battle.pokepedia.dev)
 └─ Supabase ◀── React만 접근. Unity는 접근하지 않음 (시작·VS 화면이 Storage 아트워크를 공개 URL로 읽는 것만 예외)
```

- 새 서버·DB·게임 서버는 없다. 배틀 상태는 브라우저 탭 메모리에만 있다(새로고침하면 처음부터).

### 10.2 호스팅: Cloudflare Pages [확정]

- `pokepedia.dev` DNS는 Vercel이 관리한다(네임서버 `ns1/ns2.vercel-dns.com`, 등록 기관 Name.com). Workers 커스텀 도메인은 DNS가 Cloudflare에 있어야 하지만 **Pages는 외부 DNS에 CNAME 하나로 붙는다** — 운영 중인 DNS를 옮기지 않으려고 Pages를 쓴다. 정적 요청 무료·무제한, **파일당 25 MiB**, `_headers` 지원.
- **주의:** wrangler 4.x의 `wrangler pages project create`는 기본으로 Worker로 바꿔 만든다. Pages로 만들려면 처음 한 번 `--force`. 이미 Pages 프로젝트가 있으면 이후 `pages deploy`는 Pages로 간다.

### 10.3 호스팅 설정

- `Tools/web/_headers`: `Build/*.wasm.br`·`*.js.br`·`*.data.br`에 `Content-Encoding: br`과 안쪽 확장자의 Content-Type(`.wasm`은 `application/wasm` — 스트리밍 컴파일). Cloudflare가 다시 압축하지 않는다(`Content-Length`가 원본 `.br`와 같다).
- `Build/*`는 해시 파일명이라 `Cache-Control: public, max-age=31536000, immutable`, 나머지는 Pages 기본(매번 재검증). Pages는 같은 헤더가 여러 규칙에 걸리면 값을 쉼표로 이어 붙이므로 **한 헤더는 한 규칙에서만** 정한다. `frame-ancestors`는 `/*`에 한 번.
- 로컬 `serve.py`가 같은 규칙을 흉내 낸다(`--mbps 9 --latency-ms 60`으로 Fast 4G 근사).
- **용량:** 전송 약 13MB(코드 `wasm.br` 약 7.7MB + 데이터 `data.br` 약 5.2MB). 최적화는 하지 않기로 했다(사용자 휴대폰 4G에서 문제 없음). 도트 시트는 단색 면이 많아 압축이 잘 되므로 그림을 빼도 전송량은 크게 줄지 않는다(Standard 4종 그림을 뺐을 때 텍스처 원본은 27.9 → 8.3MB였지만 전송은 0.4MB만 줄었다).
- 배포 직후 1분 안쪽은 이전 파일이 보일 수 있다(전파). 해시 비교는 잠시 뒤 다시 한다.

### 10.4 도메인과 환경

- **`battle.pokepedia.dev`** → CNAME `pokepedia-battle.pages.dev` (Vercel DNS의 레코드, 루트·와일드카드 ALIAS보다 우선). CAA가 Google·Let's Encrypt를 허용한다.
- 연결 순서(반대로 하면 522): ① Cloudflare 대시보드 Pages `pokepedia-battle` → Custom domains → `battle.pokepedia.dev` → **My DNS provider**. ② `npx vercel dns add pokepedia.dev battle CNAME pokepedia-battle.pages.dev`(Vercel MCP는 이 도메인 레코드 조회가 403이라 CLI). ③ Cloudflare가 확인·인증서 발급.
- Pokepedia의 iframe 주소는 코드 상수 `BATTLE_ORIGIN`(`src/features/battle/host.ts`).

### 10.5 빌드·배포·CI

- **로컬 Unity Release 빌드 → `python -I Tools/web/deploy.py`** [확정]: `Builds/Web`을 `Builds/deploy`로 복사하고 `_headers`를 넣어 `wrangler pages deploy`(`--branch`가 main이 아니면 미리보기). 배포 뒤 `Build/*.br`의 sha256을 실제 응답과 비교해 확인한다.
- Unity 빌드·테스트는 CI에 없다(라이선스·시간). **CI는 순수 C# 테스트만**(`dotnet test Tests.Dotnet`, PR에서만, 경로 필터) — 규칙은 [CLAUDE.md](../CLAUDE.md)의 CI 절.
- 빌드 산출물(`Builds/`)은 커밋하지 않는다. batchmode 빌드를 강제로 끊으면 `bee_backend`가 남아 다음 빌드가 실패하고 `ProjectSettings.asset`에 빌드 중 임시 값(`preloadedAssets`)이 남는다 — 프로세스를 끄고 파일을 되돌린다.

### 10.6 모니터링·보안·비용

Cloudflare Web Analytics 정도면 충분하다. 비밀값은 없고 `init`의 origin 대조와 `frame-ancestors`로 임베드를 제한한다. 비용은 사실상 0원.

### 10.7 서버가 필요해지는 시점

승리 보상, 공식 랭킹, 클라우드 세이브, 온라인 PvP를 넣을 때. 그때도 새 게임 서버보다 기존 Pokepedia 백엔드를 확장하는 것을 기본으로 한다.

### 10.8 Pokepedia 호스팅 이전(Vercel → Cloudflare) [범위 밖]

Unity가 Cloudflare에 있다고 Pokepedia를 옮길 필요는 없다(별도 origin + postMessage). 옮기면 Workers로 바꾸기 쉽다.

---

## 11. GitHub 저장소 전략

```text
ottuck/pokepedia          # Pokepedia(Next.js). 연동에 필요한 최소 변경만, 그 저장소 PR·정책으로
ottuck/pokepedia-unity    # 이 저장소
```

- 분리 이유: 구조·도구·CI·배포가 전혀 다르고, 합치면 Pokepedia CI가 Unity 변경에 영향을 받는다.
- 접점은 iframe 주소, `postMessage` 규약(9.2), 플레이어 가능 ID(7.6)뿐이다. 규약은 이 문서가 단일 출처다.
- 포트폴리오 서사: Quiz → Collection → Battle 루프, TypeScript/React(퀴즈)와 C#/Unity(배틀)의 역할 분담, 순수 C# 엔진과 NUnit, 이벤트 기반 연출, 브라우저에서 실행되는 Unity, Next.js와의 iframe 연동. 실제 지원 수를 정확히 적는다.

---

## 12. 로드맵

현재 상태와 마일스톤은 [ROADMAP.md](ROADMAP.md).

---

## 13. 확정된 사항

| 항목 | 내용 |
|---|---|
| 프로젝트 | Quiz → Collection → Battle 루프, 소규모 Unity 배틀 포트폴리오. 퀴즈는 Pokepedia 유지 |
| 공개 범위 | 피카츄(플레이어) vs 거북왕(상대). Standard 4종은 데이터만 보관 (5.1) |
| 품질 등급 | Showcase / Standard, 엔진 하나, 연출은 오버라이드 데이터 (5.2) |
| 전투 흐름 | 트레이너 없음, 행동 싸우다/도망, 난이도 하나(NPC는 턴마다 기술을 섞음), 쓰러지면 [응원한다 / 트레이너가 처리한다], 상대 1마리면 도망 확인·승리 장면에서 끝 (5.3) |
| 화면 | 시작·VS는 HTML(대각선 대결 구도, Bangers 엠블럼), 전투는 240×216 정수 배율 도트 2.5D(월드/HUD 분리, 3D 없음) (5.5) |
| 기술 | 포켓몬마다 기본기 + 속성 기술 2개, 12종 (6.3) |
| 규칙 | 1:1, 현대 데이터, L50·개체값 31·노력치 0, 물리/특수, 명중·급소, PP·상태이상 없음 (6.4) |
| 밸런스 | 4~6턴, 종족값 압축 k 0.65, Demo 피카츄 승률 85~93% (6.5) |
| 기술 스택 | Unity 6.3 LTS, C# 9, URP 2D, uGUI, 순수 C# Domain, NUnit, JSON (4) |
| 폰트 | 게임 안 Galmuri11 TMP Static, 시작·VS는 웹 글꼴 (7.7) |
| 스프라이트 | PokeAPI BW 애니메이션, 저장소에 커밋하지 않음 (8.2) |
| 사운드 | 모두 코드로 합성, 속성 공격음, 창작 BGM, 울음소리 없음 (8.3) |
| 연동 | iframe + postMessage 단방향, 보유 ∩ 지원은 Unity가 계산, 입구는 Demo와 피카츄 띠부씰 (9.2) |
| Pokepedia 쪽 | 첫 퀴즈 피카츄(피카츄 미보유), 보상 화면 배틀 버튼, 컬렉션 금색 테두리, 정수 배율 배틀 프레임 (9.4·9.6·9.7) |
| DB·보상 | Pokepedia DB 변경 없음, 승리 보상·랭킹 없음 |
| 인프라 | 서버 없음, Cloudflare Pages + `battle.pokepedia.dev`(DNS는 Vercel에 두고 CNAME) (10) |
| 모바일 | 앱 없음, 모바일 브라우저에서 완벽하게 |

## 14. 아직 결정하지 않은 사항 (에이전트가 임의로 확정하지 말 것)

**사용자 결정 필요**
- 포켓몬 울음소리(등장 소리)를 넣을지, 넣는다면 어떤 방식인지 (8.3).
- 컬렉션 `전체 | 배틀 가능` 필터를 넣을지 (9.6).
- 거북왕 등 다른 포켓몬을 플레이어로 열 시점과 밸런스(참고: 거북왕 vs 피카츄 승률 약 11%, 시드 고정 5,000판).
- Standard 4종 공개, 3:3 배틀·교체·가방, 로스터 추가, 게임 해상도 올리기의 시점 (ROADMAP "보관").

**에이전트에 위임 (할 때 결과를 이 문서에 정리)**
- 로스터를 열 때의 전설 가중치, 매치업별 상성 편향·이상치 보정.
- Standard용 공통 배경 테마, 저사양 안드로이드에서 느리면 후처리·조명 축소.

**별도 프로젝트**
- Pokepedia 호스팅 이전(Vercel → Cloudflare) (10.8).

---

## 15. 개발 에이전트 지침

이 프로젝트의 핵심은 AI가 코드를 생성하는 것이 아니라 **C#과 Unity를 이해하며 만드는 것**이다.

1. 주요 기술 선택에는 이유와 대안을 간단히 설명한다. 새 C#/Unity 개념은 Java와 비교한다(7.4).
2. Unity 생명주기와 순수 C# 도메인 로직을 구분한다. 규칙은 **테스트를 먼저** 쓴다.
3. 불필요한 프레임워크·인프라·추상화를 더하지 않는다. 웹 빌드 가능성을 늘 유지한다.
4. **Pokepedia 코드·DB는 함부로 고치지 않는다.** 고치기 전에 해당 소스, `docs/system_design.md`, 그 저장소 `CLAUDE.md`를 확인한다.
5. 엔진 코드에 포켓몬 이름·스프라이트·문자열을 넣지 않는다. 포켓몬별 코드를 만들지 않는다. **엔진은 로스터 크기를 모른다.**
6. Showcase도 실제 엔진을 쓴다. 가짜 전투나 미리 만든 연출만 재생하지 않는다.
7. **사실을 추측으로 채우지 않는다.** Pokepedia·PokeAPI·Unity는 코드·문서·API로 확인하고, 외부 제안(GPT 등)의 기술 이름·규칙·라이선스는 그대로 믿지 않는다. 버전·한도·가격 같은 사실은 웹에서 확인해 출처와 미확인 부분을 함께 적는다.
8. 사용자는 포켓몬 도메인 지식이 약하므로 규칙·용어를 먼저 설명하고 추천안과 이유를 제시한 뒤 정한다.
9. 사운드·에셋은 출처와 라이선스를 매니페스트에 기록한다.

### Unity CLI·에이전트 연동

출처: [Unity CLI as the replacement for the in-Editor MCP server](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli).

- 에디터 안 AI Assistant의 MCP 서버는 deprecated이고 Unity CLI의 `unity mcp`가 대체한다. 셸을 쓸 수 있으면 **MCP보다 `unity command`·`unity eval` 직접 호출**이 빠르고 토큰도 적다(공식 안내) → 이 저장소의 기본.
- 에디터 없이 헤드리스 빌드·테스트도 된다(`unity test . --mode EditMode`, 배치 빌드는 README). 로컬 CLI `1.0.0-beta.13`. `unity pipeline install`은 아직 안 했다(하면 패키지가 추가되므로 PR로 확인).
- 확인하지 못한 것: `winget install Unity.CLI` 패키지명, `unity skill install`의 에이전트 이름, `unity command`·`unity eval`이 실행 중인 에디터를 꼭 필요로 하는지.
- `unity eval`은 에디터에서 임의 C#을 실행한다. 권한 허용 목록에는 필요한 명령만 넣는다. 에디터 단일 점유와 병렬 작업 규칙은 [CLAUDE.md](../CLAUDE.md).
