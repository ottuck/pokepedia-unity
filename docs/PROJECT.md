# Unity Pokepedia Battle — 프로젝트 기획 및 개발 인수인계 (v6)

**개정 기준: 2026년 10월 16일**

> 이 문서는 논의를 거쳐 **확정된 사항 위주**로 정리한 최신본이다. 이전 버전과 논의 기록은 남기지 않는다.
> **[확정]** 은 합의된 사항, **[위임]** 은 에이전트가 알아서 정하고 나중에 정리하기로 한 사항, **[권장안]** 은 제안, **[미정]** 은 결정 전 항목이다. 에이전트는 [미정]을 임의로 확정하지 않는다.
> **문서 구성:** 이 문서(기획·설계·연동 규약) · [ROADMAP.md](ROADMAP.md)(단계별 진행 기록, M0 결과) · [CLAUDE.md](../CLAUDE.md)(에이전트 안내).

## 요약

- Pokepedia의 **퀴즈(Quiz) → 띠부띠부씰 컬렉션(Collection) → Unity 배틀(Battle)** 루프를 소규모로 완성한다.
- 퀴즈로 모은 포켓몬 중 **배틀을 지원하는 6마리**로 NPC와 **1:1 턴제 배틀**을 한다. 151마리 지원은 필수가 아니다.
- 연출은 **Showcase**(피카츄·거북왕, 고급 연출)와 **Standard**(나머지 4마리, 공통 연출) 두 등급이며 **엔진은 하나**다.
- Unity 6.3 LTS, C#, uGUI, 순수 C# 엔진, 블랙·화이트 애니메이션 스프라이트, 현대 포켓몬 데이터(18타입·능력치 6종) 기반의 단순화 규칙(명중·급소 포함).
- 퀴즈를 Unity로 다시 만들지 않는다. 기존 Pokepedia DB는 변경하지 않는다.

---

## 1. 프로젝트 개요

### 목표

Pokepedia에서 모은 띠부띠부씰 포켓몬 중 배틀 지원 포켓몬으로 NPC와 싸우는 **Unity + C# 기반 2D 턴제 배틀 게임**을 만든다.

- 게임보이 감성의 레트로 UI를 유지한다.
- 웹(WebGL)으로 배포하고, Pokepedia에서 **iframe으로 접근**한다. 포트폴리오 허브에서는 단독으로도 플레이할 수 있다.
- 기존 Pokepedia의 퀴즈·도감·컬렉션·랭킹은 그대로 유지한다.
- **모바일 앱은 출시하지 않지만, 모바일 브라우저에서 화면이 완벽하게 보이는 것이 목표다** [확정].
- 대규모 포켓몬 게임이 아니라 **제대로 설계하고 연출한 소규모 Unity 게임 포트폴리오**다.

### 개발 목적

- C#을 실전 프로젝트로 학습 (Java 경험 활용)
- BattleEngine, DamageCalculator, 타입 상성, NPC AI, 상태 설계 등 **도메인 설계 경험**
- Unity 2D, uGUI, Animator, Particle System, Shader, 오디오, WebGL 배포 경험
- 규칙(엔진)과 콘텐츠(데이터·에셋·텍스트)의 분리 설계
- 대규모 서비스·사용자 확보·상업화가 목적이 아니다. 학습과 완성도 우선.

### 프로젝트 완료 기준 [확정]

다음 네 가지를 달성하면 성공이다. (포켓몬 수는 기준이 아니다.)

1. 순수 C#으로 게임 규칙을 작성하고 자동화 테스트할 수 있다.
2. Unity에서 UI·애니메이션·사운드·이펙트를 직접 구현해 봤다.
3. 브라우저에서 플레이 가능한 완성형 미니게임을 공개했다.
4. 다른 개발자에게 게임 아키텍처와 설계 의도를 설명할 수 있다.

---

## 2. 기존 Pokepedia 현황 (2026-10-16 직접 확인)

- 사이트: https://pokepedia.dev/ko · 레포: https://github.com/ottuck/pokepedia
- 소개: https://www.portfolio-hub.dev/ko/projects/pokepedia · 설계 문서: 레포의 `docs/system_design.md`

**중요:** Pokepedia의 게임은 **실루엣 이름 맞히기 퀴즈**다. 턴제 배틀, 기술 데이터, 전투 엔진은 **없다.** 따라서 이 프로젝트는 업그레이드가 아니라 **후속·확장(신규 게임)** 이다.

| 항목 | 내용 |
|---|---|
| 스택 | Next.js 16, React 19, TypeScript, Tailwind v4, Zustand, next-intl, Supabase(Postgres·Auth·Storage), Vercel, Vitest, Playwright |
| 라우트 | `/[locale]`(게임), `/pokedex`, `/pokemon/[id]`, `/leaderboard`, `/collection`, `/me` |
| 계정 | 게스트(익명)로 시작, Google 연결·병합 지원 |
| 컬렉션 | `user_sticker (user_id, pokemon_id, variant normal|shiny, quantity)` — **본인 SELECT** RLS |
| 포켓몬 데이터 | `pokemon` 테이블 (public read): 한/영/일 이름, `type_1`/`type_2`, 능력치 6종(hp, attack, defense, special_attack, special_defense, speed), `is_legendary`, `is_mythical`, 아트워크 경로 등. **현재 공식 기준**(18타입, 특공/특방 분리) |
| 데이터 흐름 | PokeAPI → `scripts/sync-pokemon`(수동) → Supabase. 런타임 외부 API 호출 없음 |
| 퀴즈 출제 | `features/quiz/rules.ts`의 `pickNextPokemon(pool, seen, random)`가 이번 run에서 나오지 않은 포켓몬 중 **균등 확률**로 고름 (코드 일부 확인) |
| 에셋 | 레포에 이미지 없음. 아트워크·실루엣은 Supabase Storage. 트레이너 도트·타이틀·효과음은 SVG·CSS·Web Audio로 직접 제작 |
| 환경 | Supabase remote 프로젝트 하나를 **Prod와 Preview가 공유**. migration은 main 머지 시 자동 적용 |
| 정책 | 구조 변경은 `docs/system_design.md`를 먼저 수정. 머지 정책은 pokepedia 저장소의 `CLAUDE.md` |

---

## 3. 최종 게임 루프 [확정]

```text
Pokepedia 퀴즈 (React, 기존)
   ↓ 이름 맞히기 정답
띠부띠부씰 컬렉션 (기존, Supabase user_sticker)
   ↓ 보유 포켓몬 목록을 한 번 전달 (React → Unity)
Unity Battle (신규)
   보유 포켓몬 ∩ 배틀 지원 포켓몬 선택 → NPC와 1:1 전투 → 결과 표시
```

- 퀴즈는 **스티커를 모으는 콘텐츠**, 배틀은 **모은 포켓몬 중 일부를 쓰는 추가 콘텐츠**다.
- 연결은 **단방향**(React → Unity). 배틀 결과로 스티커·랭킹을 갱신하지 않는다.
- 퀴즈에서 **배틀 지원 포켓몬이 처음에 등장**하도록 수정한다 (9.7, 게임 완성 후).
- 진입점: Pokepedia **별도 "배틀" 메뉴**(내비게이션). 퀴즈 보상 화면의 배틀 안내는 게임 완성 후 결정한다.

---

## 4. 기술 스택

| 구분 | 기술 | 상태 |
|---|---|---|
| Engine | **Unity 6.3 LTS (6000.3.x)** — 설치 시 Unity Hub의 최신 패치 | 확정 |
| Language | C# | 확정 |
| UI | **uGUI** | 확정 |
| 애니메이션 | Animator + 코드 트위닝 (외부 라이브러리는 근거 제시 후 도입) | |
| 이펙트 | Particle System, Shader Graph 등 (타입별 공통 이펙트 프리팹) | |
| 데이터 | JSON (ContentLoader가 Domain 모델로 변환) | 확정 |
| 규칙 로직 | Pure C# | 확정 |
| 테스트 | Unity Test Framework / NUnit | 확정 |
| 폰트 | Galmuri 계열 + TextMesh Pro **Static** 글리프 아틀라스 (7.7) | 권장안, M0 검증 |
| 사운드 | AI 활용 8비트 + 라이선스가 명확한 무료(CC0) 소스 우선 (8.5) | 방향 확정 |
| 웹 | Unity Web(WebGL), 정적 호스팅 (10절) | |
| 서버·DB | **없음.** Unity는 인증·DB를 다루지 않는다 | 확정 |

- ASP.NET Core, 별도 REST 서버, WebSocket, 별도 DB는 사용하지 않는다. 공개 웹 빌드에 외부 AI API 키를 포함하지 않는다.
- **Unity 6.3 LTS 선택 근거:** 공식 릴리스 페이지 기준 2027년 12월까지 지원된다. 6.0 LTS는 2026년 10월에 지원이 끝나므로 선택하지 않는다 (사용자가 제공한 검색 결과 화면과 웹 검색으로 교차 확인). 최신 패치 번호는 확인하지 못했으므로 설치 시점의 최신을 쓴다.
- 텍스트 입력이 없으므로 IME, 정답 정규화 같은 퀴즈 이식 항목은 필요 없다. 한·영·일 **폰트 글리프 문제는 UI와 전투 메시지 때문에 남는다** (7.7).

---

## 5. 게임 범위

### 5.1 지원 포켓몬 로스터 [확정]

Pokepedia가 151마리를 지원한다고 해서 Unity 배틀도 151마리를 지원할 이유는 없다. 지원 범위를 제한해 완성도를 높인다.

| 등급 | 포켓몬 | 타입 | 연출 |
|---|---|---|---|
| **Showcase** | 피카츄 #025 | 전기 | 전기 파티클, 공격 준비·발동, 전용 피격 모션 |
| | 거북왕 #009 | 물 | 물대포 이펙트, 충격·넉백, 묵직한 움직임 |
| **Standard** | 리자몽 | 불꽃/비행 | 공통 애니메이션 + 타입별 간단한 이펙트 |
| | 이상해꽃 | 풀/독 | 〃 |
| | 팬텀 | 고스트/독 | 〃 |
| | 뮤츠 | 에스퍼 | 〃 (전설, 드물게 등장, 6.5) |

- 총 6마리. 출시 순서는 Showcase → Standard이며, 첫 릴리스는 2~4마리만 있어도 충분하다.
- 필요한 이펙트·기술의 종류는 로스터의 타입으로 결정된다. 타입이 겹치게 골라 작업량을 통제했다.
- 이후 추가는 **로스터에 항목을 늘리는 것**(데이터 + 스프라이트 + 필요 시 연출 오버라이드)으로 한다. **엔진은 지원 포켓몬 수를 몰라야 한다.**
- 18타입 상성 시스템은 C#으로 전부 구현하되 **실제 기술·이펙트는 로스터가 쓰는 타입만** 만든다.
- 포트폴리오·README에는 **전체 151마리 중 일부만 배틀을 지원한다는 사실을 명확히 표시**한다.

### 5.2 품질 등급: Showcase / Standard [확정]

엔진은 하나이고 **시각적 표현만 다르게** 적용한다. Showcase도 가짜 로직이 아니라 **실제 엔진을 쓰는 플레이 가능한 콘텐츠**여야 한다.

| 연출 요소 | Standard | Showcase |
|---|---|---|
| 등장 | 페이드 인 | 포켓몬 전용 등장 연출 |
| 공격 | 짧게 앞으로 이동 (접촉형/발사형/효과형 공통 타임라인) | 준비 동작 + 공격 애니메이션 |
| 기술 효과 | 타입별 공통 이펙트 | 커스텀 파티클·셰이더 |
| 피격 | 흔들림 + 깜빡임 | 충격·넉백·카메라 효과 |
| 급소 / 빗나감 | 공통 문구 + 효과음 | 전용 연출 (화면 번쩍임, 회피 모션) |
| 승리 | 결과 메시지 | 전용 승리 연출 |
| 사운드 | 공통 효과음 | 타이밍에 맞춘 사운드 디자인 |

- **별도의 두 시스템을 만들지 않는다.** 하나의 연출 디렉터와 기본 타임라인을 두고 종족·기술 단위 **오버라이드 데이터**(AnimationProfile)만 얹는다. 피카츄가 컬렉션 모드에 나와도 자동으로 고급 연출이 적용된다.
- 포켓몬별 애니메이션을 따로 만들지 않는다. 움직임의 강도·속도·기준점 등을 **데이터로 조정**한다.
- 타입별 이펙트는 **공통 프리팹 하나 + 타입별 색·모양 파라미터**로 데이터화한다.
- 모든 로스터 포켓몬은 블랙·화이트 애니메이션 스프라이트를 기본 대기·등장 모션으로 가진다 (8.2).
- Showcase 완료 정의의 세부 항목과 **마일스톤별 시간 상한은 정하지 않는다** [확정].

### 5.3 전투 흐름과 화면 구성 [확정: 에이전트 설계, 세부는 M3에서 조정]

대중적인 포켓몬 전투 흐름을 따른다. **트레이너는 등장하지 않는다**(지우 인트로 없음 [확정]). 야생 포켓몬과 마주친 것처럼 진행한다.

```text
타이틀 (START → 오디오 시작)
   ↓
[Collection] 포켓몬 선택 (보유 ∩ 지원, 미보유는 잠금)   [Demo] 피카츄 vs 거북왕 고정
   ↓
"야생의 ○○이(가) 나타났다! 가라! ○○!"
   ↓
행동 선택: 싸우다 / 도망
   ├ 싸우다 → 기술 선택 → 턴 처리 → (반복)
   └ 도망   → "무사히 도망쳤다!" → 상대 포켓몬이 바뀌어 새 배틀 시작
   ↓
기절 "○○은(는) 쓰러졌다!"
   ├ 승리 → [다음 상대와 싸운다] / [처음으로]
   └ 패배 → [다시 도전] / [처음으로]
```

- **난이도는 하나** [확정]. NPC AI는 예상 데미지(위력·명중률·자속·상성 반영)가 가장 큰 기술을 고른다.
- **도망 [확정]:** Collection 모드에서는 도망치면 "무사히 도망쳤다!" 후 상대가 다른 포켓몬으로 교체되어 새 배틀이 시작되고, 플레이어 포켓몬은 HP가 가득 찬 상태로 시작한다. Demo 모드는 매치업이 고정이므로 도망을 비활성화하고 "도망칠 수 없다!" 메시지를 보여 준다.
- 현재는 1:1이므로 포켓몬 교체·가방 메뉴가 없다. 행동 메뉴는 **싸우다 / 도망** 두 가지다. **교체·가방 메뉴는 향후 확장으로 추가한다** (3:3 배틀과 함께, 5.4).
- **기술 선택:** 2×2 격자, 기술은 기본기 + 타입 기술 1~2개(최대 3~4개). 선택 중인 기술의 타입·위력·명중률을 보여 준다 (PP 없음).
- **턴 처리 메시지:** "○○의 ○○!" → 빗나가면 "공격은 빗나갔다!" → 급소면 "급소에 맞았다!" → 상성에 따라 "효과가 굉장했다!" / "효과가 별로인 듯하다…" / "효과가 없는 것 같다…" → HP 바 감소 애니메이션 → 기절 문구. 메시지는 타자기식으로 출력한다 (Pokepedia 퀴즈에도 타자기 훅이 있음).
- **화면:** 게임보이식 레이아웃. NPC는 오른쪽 위(앞모습), 플레이어는 왼쪽 아래(뒷모습), 이름·레벨(`:L50`)·HP 바·HP 숫자 표시, 하단에 메시지 창과 메뉴.
- **입력:** 방향키 + Z/X, 마우스 클릭, 터치. 모바일 브라우저(세로·가로)에서도 완벽하게 보이도록 반응형으로 만든다.
- 승리해도 보상·점수는 없다 (연속으로 다음 상대와 싸울 수 있다).

### 5.4 개발 순서와 범위

**Vertical Slice 우선** [확정]: 시각적 완성도가 가장 큰 리스크이므로 콘텐츠 확장보다 먼저 **피카츄 vs 거북왕**을 최종 품질에 가깝게 완성한다.

**Phase 0 — PoC (가장 먼저).** 먼저 **검증용 최소 Unity 프로젝트**를 만든다 (정식 저장소·구조는 M1). 결과를 확인한 뒤 폐기하거나 필요한 부분만 이관한다.

1. **한·영·일 폰트**: Galmuri11/14 + TMP Static 아틀라스로 글리프 커버리지, 빌드 용량 (7.7)
2. **WebGL 빌드**: 용량, 로딩 시간
3. **iframe + postMessage 왕복**: Unity `ready` → React `init` (9.2)
4. iframe 안 키보드(방향키, Z, X) 처리와 페이지 스크롤 간섭, 터치 동작
5. **호스팅 검증** (10절): 압축 방식별 파일 크기, `Content-Encoding`·wasm MIME 헤더, 스트리밍 컴파일, 캐시 갱신
6. **공격 연출 PoC**: 피카츄 전기 공격 1개를 Animator + Particle System으로 구현
7. **애니메이션 스프라이트 변환 PoC**: 블랙·화이트 GIF → 프레임·스프라이트 시트(+프레임 지연 데이터) → Unity 재생, 용량·프레임 수·투명도·루프 확인 (8.2)
8. **모바일 브라우저 레이아웃**: 휴대폰 크기(세로·가로)에서 비율, 터치, 안전 영역
9. **오디오 재생**: START 버튼 이후 효과음·BGM, WebGL 오디오 제약, PokeAPI 울음소리 6마리(latest/legacy) 청취 비교 (8.5)

**완료 기준:** 위 9가지에 대해 가능/불가/우회책이 결론 난다.

**M0 성능 목표 [추정 기준 — 공식 표준 아님, 프로젝트 성패 기준 아님]**

> 아래 수치는 업계 가이드(FRVR의 초기 다운로드 약 8 MB 권장, CrazyGames의 빈 프로젝트 사례 12.5~14.7 MB)와 일반적인 웹 로딩 감각을 바탕으로 한 **추측성 측정 기준**이다. 달성하지 못해도 프로젝트 실패로 판단하지 않으며, M0에서 실측한 뒤 조정한다. 이 지표는 서버가 아니라 **사용자 브라우저(클라이언트)** 에서 실행되는 성능이므로 일반 노트북·중급 휴대폰에서도 확인한다. 서버는 정적 파일만 전달하므로 서버 메모리·CPU 부담은 없고 영향은 전송량과 응답 속도뿐이다.

| 항목 | 목표 | 허용 | 재검토 |
|---|---|---|---|
| 전송 크기 (압축 후, 첫 로딩) | ≤ 8 MB | ≤ 15 MB | 15 MB 초과 |
| 첫 로딩 시간 (캐시 없음, 데스크톱 Wi-Fi, 메뉴 조작 가능까지) | ≤ 5초 | ≤ 10초 | 10초 초과 |
| Fast 4G 스로틀링 로딩 | ≤ 10초 | ≤ 15초 | 15초 초과 |
| 재방문 (캐시 있음) | ≤ 3초 | ≤ 5초 | 5초 초과 |
| 프레임 | 데스크톱 60fps 안정 | 모바일 30fps 이상 | 그 미만 |
| 메모리 | 여러 판 반복해도 계속 늘지 않음 | 탭 약 300 MB 이하 (추정) | 판마다 증가(누수 의심) |

- 측정: Chrome 개발자 도구 Network 탭(캐시 비활성화, Fast 4G 스로틀링), Chrome 작업 관리자 또는 Unity Profiler.
- Unity 6 문서의 WebGL 힙 기본값은 초기 32 MB, 최대 2048 MB, 증가 단위 상한 기본 96 MB다. 이 게임의 실제 메모리는 아직 측정하지 않았다.
- 기준을 넘으면 (1) 최적화(스트리핑 강화, `Disk Size with LTO`, 불필요한 패키지 제거, 텍스처 압축) → (2) 지연 로딩·범위 축소 → (3) 플랫폼 우선순위 재검토 순으로 대응한다. Cloudflare의 단일 파일 25 MiB 한도(10.2)도 함께 확인한다.
- **폰트와 이펙트 에셋이 스프라이트보다 더 큰 변수**일 가능성이 높다 (로스터 6마리의 GIF 원본은 합계 약 0.8 MB).

**Phase 1 — Showcase 데모 (Vertical Slice).** 피카츄 vs 거북왕 1:1을 실제 엔진으로 플레이 가능한 한 판으로 완성: 순수 C# 엔진과 NUnit 테스트, 5.3의 전투 흐름과 게임보이식 화면, 공격·피격·급소·빗나감 연출, 사운드, 반응형 레이아웃, 웹 빌드와 **Demo Mode** 단독 배포.

**Phase 2 — 로스터 확장 + Standard 연출.** 4마리 확장(리자몽, 이상해꽃, 팬텀, 뮤츠), 공통 배틀 연출과 타입별 이펙트 프리팹·AnimationProfile 검증, 여러 체형의 스프라이트 대응, 밸런스 조정.

**Phase 3 — Pokepedia 연동 (Collection Mode).** 보유 ∩ 지원 선택, iframe 연동 (9절). 퀴즈의 지원 포켓몬 우선 출제와 컬렉션 "배틀 가능" 필터 등 Pokepedia 쪽 세부는 **게임 완성 후** 정한다 (9.6, 9.7).

**이후 / 향후 확장.** 1순위는 1:1 배틀의 완성도이며, 그 이후에 다음을 확장한다.
- **3:3 배틀**, **포켓몬 교체 메뉴와 가방 메뉴** (3:3과 함께 추가) [확정: 향후 확장 계획, 시점·규칙은 미정]. 엔진은 처음부터 여러 포켓몬(파티)을 지원하는 구조로 설계해 둔다 (7.2).
- 로스터 추가, 기술 다양화, BGM, 포켓몬별 고유 모션, 퀴즈 보상 화면의 배틀 안내(게임 완성 후 결정).

**범위 제외.** 151마리 전체 지원, 포켓몬별 고유 애니메이션 151개, 트레이너·인트로 씬, 난이도 선택, 배틀 승리 보상·랭킹 갱신, 온라인 PvP·실시간 멀티플레이, 자체 게임 서버, 퀴즈의 Unity 재구현, LLM 기반 NPC, PP·상태이상.

---

## 6. 전투 규칙과 데이터 설계

### 6.1 원칙 [확정]

- 원작 규칙 재현이 목표가 아니다. 엔진은 **하나**만 작성하고 포켓몬은 **데이터**로만 표현한다. 포켓몬별 클래스·스크립트를 만들지 않는다.
- **데이터 기준: 현대 기준.** 등장 포켓몬은 1세대 151종 중 선별한 일부, **타입·능력치·상성은 현대 기준**(18타입, 능력치 6종), 전투 규칙은 직접 정의한 단순화 규칙이다. 게임보이 감성은 스프라이트·UI·폰트·색감·애니메이션·사운드로 유지한다. (로스터 6마리는 원작 1세대와 현대 기준에서 타입 구성이 같다: 전기, 물, 불꽃/비행, 풀/독, 고스트/독, 에스퍼. 원작 1세대는 15타입이며 능력치의 특수가 하나였다 — 기억 기반.)
- 기술 이름·구성은 원작과 일치할 필요가 없다. 이름은 장식이고 규칙은 데이터(타입·위력·명중률·분류)가 결정한다.

### 6.2 포켓몬 데이터

- 출처: Pokepedia `pokemon` 데이터 → **로스터 포켓몬만 빌드 시점 스냅샷 JSON**으로 export해 Unity에 포함한다 [권장안]. 런타임 DB 조회는 없다.
- 사용 필드: id, 한/영/일 이름, `type_1`/`type_2`(이중 타입), 능력치 6종, `is_legendary`, `is_mythical`.
- 이름 문자열은 엔진에 없다. `nameKey`로 로케일 파일에서 찾는다.
- **지원 포켓몬 목록은 `roster.json`** (id와 등급 `showcase`|`standard`)으로 관리한다. 엔진은 이 파일을 모른다.

```json
// species.json (예시)
{ "id": 25, "types": ["electric"],
  "baseStats": { "hp": 35, "attack": 55, "defense": 40, "spAttack": 50, "spDefense": 50, "speed": 90 },
  "moves": ["tackle", "thunder_spark"],
  "sprites": { "front": "front_025", "back": "back_025", "frontShiny": "front_025_s", "backShiny": "back_025_s" },
  "animProfile": "pikachu", "nameKey": "species.025", "legendary": false }

// roster.json (예시)
[ { "id": 25, "tier": "showcase" }, { "id": 9, "tier": "showcase" },
  { "id": 6, "tier": "standard" }, { "id": 3, "tier": "standard" },
  { "id": 94, "tier": "standard" }, { "id": 150, "tier": "standard" } ]
```

### 6.3 기술 데이터 (새로 작성)

- Pokepedia에는 기술 데이터가 없으므로 **직접 정의**한다. **노말 기본기 1개 + 로스터가 쓰는 타입별 대표 기술 1개씩**이며 기본 로스터(전기·물·불꽃·비행·풀·독·고스트·에스퍼)면 **약 9개**다. 새 타입을 추가할 때 해당 기술·이펙트를 추가한다.
- 각 포켓몬은 "기본기 1 + 자신의 타입별 기술 1~2"를 받는다. 이 매핑은 타입에서 **자동 생성**한다.
- 기술 필드: `id, type, category(physical|special), power, accuracy, nameKey, animKind`. **명중률과 급소를 포함한다** [확정].
- 초기 수치 [권장안, 시뮬레이션으로 조정]: 기본기 위력 16·명중 100, 타입별 대표 기술 위력 36·명중 95. 전투를 4~6라운드로 맞추려고 원작의 위력 값(40/90 정도)보다 낮게 잡는다 (6.5).
- 원작 기술 이름을 쓰더라도 위력은 단순화한다 (고정 피해 기술, 2턴 기술, 후속 세대 기술은 단순 위력 계산과 맞지 않으므로 이름만 빌려 쓰지 않는다).
- 공격 연출은 **접촉형 / 발사형 / 효과형** 세 가지 공통 타임라인 중 `animKind`로 선택한다 (몸통박치기 = 접촉, 불꽃·물대포 = 발사, 에스퍼·고스트 = 효과) [위임].

### 6.4 전투 규칙 [확정]

공식은 현대 포켓몬 게임(3세대 이후) 기준이며 에이전트의 기억에 기반하므로 구현 전에 한 번 더 검증한다.

- **능력치 환산:** 레벨 50 고정, 개체값 31, 노력치 0, 성격 보정 없음.
  - HP = ⌊(2×종족값+31)×레벨/100⌋ + 레벨 + 10
  - 그 외 = ⌊(2×종족값+31)×레벨/100⌋ + 5
  - 예: 피카츄 L50 = HP 110 / 공격 75 / 방어 60 / 특공 70 / 특방 70 / 스피드 110, 거북왕 L50 = HP 154 / 103 / 120 / 105 / 125 / 98.
- **물리/특수:** 기술별 `category`를 데이터에 지정한다. physical은 공격÷방어, special은 특공÷특방.
- **턴 순서:** 스피드가 높은 쪽이 먼저 행동하고 동률이면 무작위.
- **행동 처리:** 명중 판정(기술 명중률%, 회피·명중 단계 없음) → 빗나가면 `MoveMissed` → 명중하면 급소 판정 → 데미지 계산.
- **급소:** 확률 1/16, 데미지 ×1.5 [권장안: 현대 6세대 방식, 값 조정 가능].
- **데미지 공식:** ((2L/5+2) × 위력 × 공격/방어 ÷ 50 + 2) × 급소(×1.5) × 난수(0.85~1.0) × 자속 보정(×1.5) × 타입 상성. L은 레벨, 상성은 ×2 / ×0.5 / ×0이며 이중 타입은 곱한다.
- 명중·급소·데미지 난수는 모두 `IRandom`으로 주입해 테스트에서 고정한다. HP 0이면 패배.
- **타입 상성:** 18×18 표를 직접 작성하고 단위 테스트로 검증한다.
- **제외:** PP, 상태이상, 우선도, 능력치 단계 변화, 날씨.
- **NPC AI:** 하나의 수준. 예상 데미지(위력·명중률·자속·상성 반영)가 큰 기술을 고른다.

### 6.5 밸런스와 NPC 선택 [확정 방침 / 세부는 에이전트가 조정]

- **전투 길이:** 쇼케이스는 약 **4~6라운드**. 별도의 HP 배율 규칙은 **두지 않고 기술 위력 값(데이터)** 으로 맞춘다 (위력 ×0.4가 HP ×2.5와 같은 결과를 냈다). 모든 모드가 같은 규칙과 기술 데이터를 쓰므로 Demo만의 값은 없다.
- **균형:** 종족값을 중간값 쪽으로 당기는 **압축** `stat' = 80 + (종족값 − 80) × k` (k 초기 0.6 [권장안])을 쓴다.
- **NPC 선택 (Collection):** 로스터에서 가중 추첨한다. **뮤츠 같은 전설 포켓몬은 드물게만 등장**하도록 가중치를 낮춘다 [확정]. 압축 계수, 위력·명중 값, 상성 편향, 이상치 보정 등 **밸런스 세부는 에이전트가 시뮬레이션으로 조정하고 완료 후 이 문서에 정리한다** [위임].
- 중복 스티커(quantity)는 능력치에 영향 없음, shiny는 외형만 변경.
- **Demo Mode:** 피카츄(플레이어) vs 거북왕(NPC) 고정. **피카츄 승률 60~70%** 를 시드 고정 시뮬레이션 테스트로 검증·조정한다 [확정].
- 신규 사용자(스티커 없음)와 단독 실행은 Demo Mode로 처리한다. Collection에서 보유 ∩ 지원이 0마리면 자동 전환하지 않고 안내와 [데모 체험] 선택지를 보여 준다 (9.2).

**참고: 종족값 합** (Pokepedia 현재 기준) 피카츄 320, 팬텀 500, 이상해꽃 525, 거북왕 530, 리자몽 534, 뮤츠 680. 피카츄가 가장 약하고 뮤츠가 가장 강한 이상치이며 나머지는 비슷하다.

**참고: 시뮬레이션 요약** (에이전트의 단순 모델, 레벨 50, 기본기 40·대표 기술 90/명중 95 기준, 급소 1/16×1.5, AI는 예상 데미지 최대 기술 선택): 보정 없이는 평균 약 2라운드이고 피카츄는 거북왕에게 17%, 나머지에게 거의 0%. 압축 k=0.6과 위력 ×0.4를 쓰면 평균 약 4.6라운드, 피카츄 vs 거북왕 약 77%. 피카츄 능력치를 추가로 올리면 100%가 되어 과하다. 같은 위력의 기술끼리는 **타입 상성이 승패를 거의 결정**한다(물 > 불꽃 > 풀 > 물 구조). 뮤츠는 압축 후에도 거의 전승이라 전설 가중치·보정이 필요하다. 실제 값은 M2에서 엔진으로 확정한다.

---

## 7. 아키텍처

### 7.1 설계 원칙 [확정]

1. **엔진(규칙)과 콘텐츠(데이터·에셋·텍스트)를 처음부터 분리**한다. 엔진은 ID와 숫자만 안다.
2. **Domain은 UnityEngine을 참조하지 않는다.** Assembly Definition으로 강제한다.
3. 난수는 `IRandom`으로 주입해 테스트에서 고정한다.
4. 규칙 자체를 일반화하지 않는다 (`IRuleSet` 등 금지). 분리 대상은 규칙이 아니라 콘텐츠다.
5. 모든 클래스에 인터페이스를 만들지 않는다. DI 프레임워크, Repository 패턴, 이벤트 버스 라이브러리, ECS는 도입하지 않는다.
6. **엔진은 지원 포켓몬의 수를 모른다.** 로스터가 늘어도 엔진은 수정하지 않는다.
7. 우선순위: **배틀 로직의 독립성, 테스트 용이성, 화면과 도메인의 책임 분리.** Java/Spring의 DDD는 참고만 한다.

### 7.2 계층

```text
Presentation (Unity)   ← uGUI, 이벤트 재생, 연출(AnimationDirector), 오디오
        ↓
Application (C#)       ← BattleController, NpcBattleAi, 배틀 흐름
        ↓
Domain (Pure C#)       ← BattleEngine, DamageCalculator, TypeChart, BattleState
        ↑ 읽기 전용 데이터 주입
Content (Data)         ← species.json, roster.json, moves.json, typechart, locale, 스프라이트, AnimationProfile

Bridge (Platform)      ← IHostBridge: WebGL(jslib ↔ postMessage) / Editor(Null 구현)
```

- **Domain:** 입력 `BattleState` + `BattleAction`, 출력 `BattleEvent` 목록. 엔진은 문자열·스프라이트를 모른다. 여러 포켓몬(파티)을 지원하는 구조로 설계한다.
- **Application:** 플레이어 행동 접수, NPC 행동 결정, 엔진 호출, 이벤트 전달, 배틀 종료·재시작·상대 교체 처리.
- **Presentation:** `BattleEvent`를 받아 애니메이션·파티클·셰이더·사운드·HP 바로 **재생만** 한다. 규칙 판단 금지.
- **Bridge:** React와의 통신을 한 곳에 격리한다. 에디터에서는 Null 구현.

### 7.3 엔진 ↔ 화면: 이벤트 로그와 연출 디렉터

```csharp
public abstract record BattleEvent;
public sealed record BattleStarted(...) : BattleEvent;
public sealed record MoveUsed(int Side, string MoveId) : BattleEvent;
public sealed record MoveMissed(int Side, string MoveId) : BattleEvent;
public sealed record DamageDealt(int Side, int Amount, int HpAfter, float Effectiveness, bool Critical) : BattleEvent;
public sealed record Fainted(int Side) : BattleEvent;
public sealed record BattleEnded(int WinnerSide) : BattleEvent;
```

```text
BattleEngine (Pure C#)
      │ BattleEvent 목록
      ▼
BattleController
      │ 이벤트 실행
      ▼
AnimationDirector (Presentation)
      │ 기본 타임라인(접촉/발사/효과) + (종족/기술) AnimationProfile 오버라이드
      ├── Animator   ├── Particle System   ├── Shader   ├── Audio   └── Camera Effects
```

엔진은 데미지와 남은 HP만 반환하고 연출은 Presentation이 이벤트와 AnimationProfile로 결정한다. 규칙과 시각적 연출이 완전히 분리되므로 연출을 바꿔도 엔진은 변경하지 않는다. 테스트는 이벤트 리스트만 단언하고, 메시지는 이벤트 → 로케일 키 → 문자열 순으로 Presentation에서 조립한다.

### 7.4 Java 개발자를 위한 C# 대응표

| Java | C# |
|---|---|
| `record` | `record` (C# 9+) |
| `sealed interface` + 패턴 매칭 | `abstract record` + `switch` 식 |
| `Optional<T>` | nullable reference types (`T?`) |
| 패키지 | namespace + Assembly Definition(.asmdef) |
| JUnit | NUnit |
| Spring Bean 주입 | 생성자 주입 직접 구현 |
| 스레드/Future | 코루틴, `async/await` (Unity 메인 스레드 제약 주의) |

### 7.5 디렉터리 구조 (초기 예시)

```text
pokepedia-unity/
├── Assets/
│   ├── Scripts/
│   │   ├── Domain/            # .asmdef, UnityEngine 참조 없음
│   │   │   ├── Battle/        # BattleEngine, BattleState, BattleAction, BattleEvent
│   │   │   └── Rules/         # DamageCalculator, TypeChart, TurnOrder
│   │   ├── Application/       # BattleController, NpcBattleAi
│   │   ├── Content/           # ContentLoader, Dto, RosterProvider
│   │   ├── Bridge/            # IHostBridge, WebGlHostBridge, NullHostBridge, *.jslib
│   │   └── Presentation/      # 화면, SpriteLibrary
│   │       └── Animation/     # AnimationDirector, 타임라인, AnimationProfile 적용
│   ├── Content/
│   │   ├── Data/              # species.json, roster.json, moves.json, typechart.json
│   │   ├── Animation/         # AnimationProfile 데이터 (종족/기술 오버라이드)
│   │   ├── Audio/             # 사운드 + 매니페스트
│   │   ├── Locale/            # ko/en/ja
│   │   ├── Sprites/           # PokeAPI 스프라이트 (스크립트로 생성, 커밋 안 함)
│   │   └── Placeholder/
│   ├── VFX/                   # 타입별 공통 이펙트 프리팹, Showcase 전용 이펙트
│   ├── Tests/EditMode/        # Domain 테스트 (가짜 종족·기술 사용)
│   └── Scenes/ Animations/ Prefabs/ Shaders/
├── Tools/                     # 데이터 export, 스프라이트 다운로드·변환, 글리프 추출 스크립트
├── WebTemplates/              # WebGL 템플릿
└── README.md
```

의존 방향: `Presentation → Application → Domain`, `Presentation/Application → Content → Domain`. Bridge는 Application이 인터페이스로만 사용한다. asmdef는 5개 이내.

### 7.6 지원 포켓몬 매니페스트 [권장안]

지원 포켓몬 ID 목록의 **단일 출처는 Unity 저장소의 `roster.json`** 이다. React와 Unity가 각자 목록을 관리하지 않는다. 빌드와 함께 `roster.json`(또는 ID 매니페스트)을 정적 파일로 배포하고 Pokepedia가 필요할 때 읽는다 (CORS 헤더 필요). 전달 방식은 [위임].

### 7.7 폰트 (한글·일본어·영어) [권장안, M0에서 검증]

- TMP 폰트 에셋에 글리프가 없으면 □로 깨진다. 이 게임은 **사용자 텍스트 입력이 없고 출력 문자열이 정해져 있으므로 필요한 글리프만** 포함하면 된다 (한글 11,172자·일본어 한자 수천 자 불필요).
- 배포 빌드는 **TMP Static 폰트 에셋(미리 만든 글리프 아틀라스)**. Dynamic은 개발·예비 용도(원본 폰트를 빌드에 포함해 용량↑). (Unity 문서의 권장 취지는 외부 제안 인용이며 직접 확인하지 않음)
- 글리프 추출: `ko/ja/en.json` + 포켓몬·기술 이름 + 기호 → 중복 제거 → Static 아틀라스. `Tools/` 스크립트로 자동화.
- **후보 폰트: Galmuri** (한글·가나·영문 비트맵 스타일). 변형은 Galmuri14, 11, 11 Bold, 11 Condensed, 9, 7. 저장소에 라이선스 **OFL-1.1**이 표기됨(원문 직접 확인). 한글 완성형은 14/11/11 Bold/11 Condensed/9에서 지원되고 7은 4,358자만 지원하며, 한자는 JIS X 0208의 6,355자까지만 지원한다. **어느 변형이 히라가나·가타카나를 지원하는지, 11 Bold의 일본어 지원 여부는 확인하지 못했다** → M0에서 Galmuri11·14의 실제 글리프를 검사한다. 보조 폰트로 Noto Sans CJK 계열(OFL)은 필요할 때만 검토.
- 검사 항목: 일본어 한자, 한글 받침, 기술 이름, 특수문자(♀ ♂ × ! 등), 글자 크기, 빌드 용량. 아틀라스 크기는 설정에 따라 달라 **용량을 단정할 수 없으므로 M0에서 실측**한다.
- 자동 검증: 모든 로케일 문자열의 글리프 포함 여부를 EditMode 테스트로 확인 (TMP의 글리프 확인 API 시그니처는 확인 필요).

### 7.8 테스트 전략

- Domain 테스트는 코드로 만든 가짜 종족·기술(`TestMon`, `TestMove`)만 쓴다. 실제 JSON에 의존하지 않는다.
- `ContentLoader`는 "실제 JSON이 스키마에 맞는지", 로스터의 모든 포켓몬이 필요한 스프라이트·기술·로케일 키를 가졌는지 자동 검증한다.
- `IRandom` 스텁으로 결정적 테스트를 만든다 (명중·급소·데미지 난수 모두 주입).
- 순서: 타입 상성(18×18 표본 + 이중 타입) → 데미지 → 명중·급소 → 턴 순서 → 승패 판정 → NPC AI. Demo 매치업은 시드 고정 시뮬레이션으로 승률·라운드 수를 검증한다.
- 연출은 자동화가 어려우므로 체형별 대표 포켓몬을 골라 눈으로 확인한다 (스프라이트 위치·잘림).

---

## 8. 아트·에셋 전략

### 8.1 방침

- **포켓몬 이미지는 PokeAPI 스프라이트를 사용한다.** 권리 문제는 사용자 결정에 따라 Pokepedia와 같은 수준(비공식·비영리 팬 프로젝트 고지)으로 진행하며 별도 검토는 하지 않는다. 다른 원작 에셋은 쓰지 않고 UI·효과음은 직접 제작하거나 라이선스가 명확한 것을 쓴다.
- 퀴즈의 정면 아트워크는 배틀에서 쓰지 않는다. 배틀에는 플레이어 쪽 **뒷모습**이 필요하다.
- **로스터 포켓몬(소수)에만 필요한 에셋을 준비한다.**
- **저장소에 스프라이트를 커밋하지 않는다** [확정]: `Assets/Content/Sprites/`는 `.gitignore`에 넣고, `Tools/`의 다운로드·변환 스크립트와 **PokeAPI sprites 커밋 SHA로 고정한 파일 목록 매니페스트**만 커밋한다. clone 후 스크립트를 한 번 실행하면 재현된다 (README에 명시). Pokepedia와 같은 정책(레포에 이미지 없음)이다.

### 8.2 스프라이트 스타일 [확정: 5세대 블랙·화이트 애니메이션]

- **사용 소스** (PokeAPI sprites, 1~151번 전부 존재 확인, 2026-10-16): 앞 `versions/generation-v/black-white/animated/{id}.gif`, 뒤 `animated/back/{id}.gif`, 색이 다른 포켓몬 `animated/shiny/`·`animated/back/shiny/`. 정적 PNG(`black-white/{id}.png`, `back/`, `shiny/`)도 있어 폴백·목록 아이콘에 쓴다. (확인한 것은 파일 존재와 형식뿐이며 이미지 내용·품질은 확인하지 않았다.)
- **로스터 6마리 GIF 크기**(앞/뒤, KB): 피카츄 23/21, 거북왕 119/120, 리자몽 81/77, 이상해꽃 122/102, 팬텀 24/32, 뮤츠 45/47 (합계 약 0.8 MB).
- **Unity 처리:** GIF를 직접 임포트하지 못하므로 `Tools/` 스크립트로 **프레임 분해 → 스프라이트 시트 + 프레임 지연 데이터**로 변환하고 Animator 클립 또는 프레임 플레이어로 재생한다. 최대한 원본을 참고해 옮기며 필요하면 일부를 직접 보정한다. 변환 후 용량·프레임 수·투명도·루프는 M0 PoC에서 확인한다.
- **유의:** 스프라이트는 5세대 도트이고 UI는 게임보이풍이므로 UI·배경·팔레트로 조화시킨다. 포켓몬마다 크기가 다르므로 기준점(발 위치) 보정과 정수 배율·Point 필터가 필요하다 (`SpriteLibrary`).
- 다른 후보(채택하지 않음: 1세대 레드·블루, 옐로 GBC, 2세대 크리스탈, 기본 96×96)는 비교 페이지 [`sprite-style-compare.html`](sprite-style-compare.html)(같은 `docs` 폴더)에서 다시 볼 수 있다.

### 8.3 폰트

7.7 참고.

### 8.4 인트로·트레이너

트레이너·인트로 씬은 만들지 않는다 [확정].

### 8.5 사운드 전략 [방향 확정 / 세부는 권장안]

- **방향 [확정]:** **AI를 활용한 8비트 사운드**를 기본으로 하고, **에이전트가 구할 수 있는 라이선스가 명확한 무료 소스를 우선** 사용한다. 모든 포켓몬마다 소리를 따로 만들 필요는 없다.
- **소스 후보** (외부 제안의 조사 + 일부 직접 확인). 사용 전 각 파일의 라이선스를 반드시 확인한다.

| 소스 | 내용 | 라이선스·비고 |
|---|---|---|
| Kenney UI Audio / Digital Audio / Impact Sounds | UI·전기/특수·타격 효과음 | CC0로 알려짐 (직접 확인하지 않음) |
| OpenGameArt "8-Bit Battle Loop" (Wolfgang_) | 반복 가능한 8비트 배틀 BGM (OGG, FamiTracker 파일 포함) | CC0 (검색 결과 확인) |
| OpenGameArt "8-Bit Sound Effect Pack Vol. 001" (Deva) | WAV 46개 | CC0 표기 + 크레딧 요청(@Shades). **같은 이름의 다른 팩(OwlishMedia, 76개)은 번들 License.txt가 "재판매 제외 모든 용도"라는 보고가 있어 CC0 표기와 불일치** → 팩을 특정하고 zip의 라이선스 파일 확인 |
| ChipTone / Jfxr / BeepBox | 8비트 효과음·칩튠 BGM 생성 도구 (브라우저) | 생성물의 상업적 이용 가능으로 알려짐 (직접 확인하지 않음). 도구 약관 확인 |
| ElevenLabs Sound Effects (AI) | 자연어로 효과음 생성 | 무료 플랜 생성물의 상업적 이용 제한, 유료 플랜은 조건부로 알려짐 (직접 확인하지 않음). 생성 시점의 요금제·약관을 기록 |

- **포켓몬 울음소리 [방침 확정]:** PokeAPI `cries` 저장소(`cries/pokemon/latest|legacy/{id}.ogg`, README는 1~9세대 전체라고 설명, 151마리 존재 여부는 직접 세지 못함)를 참고한다. **품질이 좋으면 사용 1순위, 나쁘면 비슷한 소리를 새로 제작**한다. M0에서 로스터 6마리의 `latest`와 `legacy`를 사용자가 직접 듣고 판단한다. 음원은 Showdown·Veekun·The Sounds Resource에서 가져온 원작 음원이며 저장소 라이선스는 자동 인식되지 않는다(NOASSERTION). 권리는 8.1의 정책과 같은 수준으로 취급한다.
- **필요한 사운드** (피카츄 vs 거북왕 기준 약 14개): UI(커서 이동·선택·취소 3), 배틀(등장·공통 공격·피격 3), 전기(충전·방전·명중 3), 물(물대포 발사·충돌 2), 결과(승리·패배 2), BGM 1. 같은 소리를 피치·재생 속도로 변형해 개수를 줄인다.
- **Unity 구현:** 처음에는 `AudioSource.PlayOneShot()`으로 시작해 연출 이벤트와 동기화한다 (준비 → 충전음, 발사 → 방전음, 피격 → 충돌음). **타이밍 동기화가 핵심**이다.
- **웹 주의:** 브라우저는 사용자 상호작용 전 자동 오디오 재생을 제한하므로 **START 버튼을 누를 때 오디오를 시작**한다. WebGL에서는 AudioMixer 일부 기능에 제약이 있다(외부 제안이 인용한 Unity 문서 내용, 직접 확인하지 않음). BGM은 압축 형식, 짧은 효과음은 압축 해제 방식 등 임포트 설정으로 용량을 관리한다.
- **역할 분담:** 에이전트 — 라이선스 확인 → 소스 수집 → WAV/OGG 정리 → AudioClip 등록 → 애니메이션 이벤트 연결. AI — 기존 소스로 부족한 전기·물 공격과 승리 연출의 고유 효과음 생성. 사용자 — 최종 소리 선택, 볼륨 밸런스, 피격감, BGM 조화.
- **사운드 매니페스트:** 모든 사운드의 출처·라이선스·파일명·수집일(AI 생성이면 도구와 요금제)을 `Content/Audio/`에 기록한다. 사운드는 애니메이션 타이밍이 잡힌 뒤 제작한다 (M3 안에서).

---

## 9. Pokepedia(React) 연동 — 가벼운 연동

### 9.1 방식: iframe + postMessage [권장안]

- 기존 사이트의 헤더·푸터·i18n을 유지하면서 Unity 게임을 한 페이지에 넣는다. 임베드 페이지 경로는 **`/[locale]/battle`** [확정].
- Origin 검증 필수. Pokepedia CSP(`frame-src`)와 Unity 호스트의 `frame-ancestors`를 확인한다.
- 별도 도메인에 호스팅해도 `postMessage`로 충분하다 (쿠키를 쓰지 않음).

### 9.2 메시지 규약 (v1) [확정]

이 절이 Pokepedia ↔ Unity 규약의 **단일 출처**다. Pokepedia(React) 쪽 구현은 이 절을 따른다.

#### 구성

```text
Pokepedia 페이지 (/[locale]/battle)
  └─ <iframe src="https://battle.pokepedia.dev/?host=pokepedia" allow="autoplay">
        Unity Web 빌드 (index.html 템플릿 + HostBridge.jslib + HostBridge.cs)
```

- 방향은 **단방향**이다. React가 보유 목록을 한 번 넘기고, Unity는 결과를 저장하지 않는다.
- 쿠키·인증을 쓰지 않으므로 다른 도메인에 호스팅해도 `postMessage`로 충분하다.
- 컬렉션 조회는 인증된 React(서버/RLS)가 수행한다. Unity는 인증·DB를 다루지 않는다.
- **지원 포켓몬 필터는 Unity가 처리한다.** React는 보유 목록 전체를 보내고 Unity가 로스터와의 교집합(보유 ∩ 지원)을 계산한다. 로스터가 늘어도 규약이 바뀌지 않는다.
- Unity로 전달된 보유 목록은 조작될 수 있으나 보상·랭킹에 영향이 없으므로 검증하지 않는다.

#### 모드 판별 [확정]

| iframe 주소 | 동작 |
|---|---|
| `?host=pokepedia` 없음 | 즉시 **Demo** 모드(피카츄 vs 거북왕). 대기하지 않는다 |
| `?host=pokepedia` 있음 | START 후 `ready`를 보내고 `init`을 **5초** 기다린다 |

- 5초 안에 `init`이 오지 않으면 "연결하지 못했습니다" + **[다시 시도] / [데모 체험]**을 보여 준다. **자동으로 Demo로 바꾸지 않으며** Demo는 [데모 체험] 버튼으로 사용자가 명시적으로 선택할 때만 시작한다.
- [다시 시도]를 누르면 `ready`를 다시 보낸다.
- iframe 여부(`window.parent !== window`)로는 판별하지 않는다. 포트폴리오 허브도 iframe으로 임베드할 수 있기 때문이다.
- 쿼리는 jslib로 `location.search`를 읽어 판별한다(iframe에서 `Application.absoluteURL`의 의미가 불명확해 쓰지 않는다. M0 확인).

| 모드 | 포켓몬 선택 | 사용처 |
|---|---|---|
| **Demo Mode** | 로그인 없이 **피카츄 vs 거북왕 고정 매치업** (Showcase 품질, 선택 화면 없음) | 포트폴리오 허브 등 단독 실행 |
| **Collection Mode** | Pokepedia가 `init`으로 전달한 **보유 포켓몬 ∩ 지원 포켓몬** | Pokepedia iframe |

- 별도 엔진을 두 개 만드는 것이 아니라 포켓몬 선택(로스터) 데이터만 다르다.
- Collection 선택 화면: 지원 포켓몬 목록을 보여 주고 **보유한 포켓몬만 선택 가능, 미보유는 잠금**("퀴즈로 획득"). 보유했지만 지원하지 않는 포켓몬은 "배틀 지원 N종 준비 중" 같은 한 줄로 안내한다. 보유 ∩ 지원이 비어 있으면 안내와 [데모 체험] 버튼을 보여 준다.

#### 메시지

모든 메시지는 `postMessage`로 보내는 **객체**이며 `type`과 프로토콜 버전 `v`를 가진다. 요청 ID는 현재 필요하지 않다 [위임].

**로딩 순서:** Unity Web Build 로딩 → Unity → React `ready` → React → Unity `init` → Unity가 보유 ∩ 지원을 계산하고 게임 시작.

**Unity → React: `ready`**

```json
{ "type": "ready", "v": 1 }
```

- Unity 로딩과 START가 끝났다는 뜻이다. **React는 이 메시지를 받은 뒤에만 `init`을 보낸다.** React가 `init`을 먼저 보내면 Unity 로딩 전에 사라질 수 있다.
- Unity가 재로딩되면 `ready`가 다시 온다. 그때 React는 `init`을 다시 보낸다.

**React → Unity: `init`**

```json
{ "type": "init", "v": 1, "locale": "ko", "owned": [ { "id": 25, "shiny": false }, { "id": 9, "shiny": true } ] }
```

| 필드 | 설명 |
|---|---|
| `locale` | `ko` / `en` / `ja`. 그 밖의 값은 `ko`로 처리 |
| `owned` | 사용자가 가진 **스티커 전체**. 배틀 지원 여부는 Unity가 로스터와 교집합으로 판단한다 |

- `v`가 1이 아니면 Unity는 무시한다.

**Unity → React: `battleEnded` (예정, M6)**

```json
{ "type": "battleEnded", "v": 1, "result": "win" }
```

표시용이다. 저장하지 않는다.

**Unity → React: `resize` (필요 시)** — 높이 조정. 아직 정의하지 않았다.

#### 보안

| 위치 | 규칙 |
|---|---|
| Unity 템플릿 `message` 리스너 | `event.origin`이 허용 목록(`window.pokeHost.allowedOrigins`)에 있을 때만 `init`을 Unity로 전달 |
| Unity → React 전송(jslib) | `targetOrigin`을 `'*'`로 쓰지 않고 허용 목록의 각 origin으로 보낸다(일치하는 곳에만 도착) |
| React 쪽 | `event.origin`이 Unity 호스트(`NEXT_PUBLIC_BATTLE_URL`의 origin)일 때만 `ready`를 처리 |
| Unity 호스트 응답 헤더 | `Content-Security-Policy: frame-ancestors`에 Pokepedia·포트폴리오 허브만 허용 |

허용 목록은 `Assets/WebGLTemplates/Pokepedia/index.html`에 있다. 현재 값: `https://pokepedia.dev`, `https://www.pokepedia.dev`, `http://localhost:3000`(Pokepedia 개발 서버), `http://localhost:8000`(로컬 테스트 페이지). Vercel Preview 도메인은 M6에서 정한다. 또 Pokepedia CSP의 `frame-src`도 확인한다(9.1).

#### 입력

- iframe 안에서는 방향키·Space의 기본 동작(부모 스크롤)을 막는다.
- 클릭·터치 시 캔버스에 포커스를 준다.
- React 쪽 iframe에는 `allow="autoplay"`를 준다. 첫 소리는 Unity 안의 START 버튼에서 낸다.

#### 로컬 테스트

```bash
python -I Tools/web/serve.py --dir Builds/Web --port 8001 --ancestors http://localhost:8000
python -I Tools/web/serve.py --dir Tools/web/host-test --port 8000
```

`http://localhost:8000/`를 열면 모의 Pokepedia 페이지가 게임을 iframe으로 띄운다. 이 페이지에서 `ready` → `init` 왕복, 타임아웃(init 보내지 않기 체크), 스크롤 간섭을 확인할 수 있다.

### 9.3 DB 정책 [확정]

- **Pokepedia DB는 변경하지 않는다.** 새 테이블·RPC가 필요 없고 `user_sticker`에도 아무것도 추가하지 않는다. 읽기만 사용한다 (컬렉션은 React가 읽어 전달, 카탈로그는 스냅샷 JSON).
- (Prod와 Preview가 같은 DB를 공유하고 migration이 자동 적용되므로 변경이 필요해지면 additive만 허용한다.)

### 9.4 Pokepedia 쪽 변경 범위

- 내비게이션 "배틀" 항목, 임베드 페이지(`/[locale]/battle`), 메시지 핸들러.
- 문구는 `messages/ko·en·ja.json` 세 곳에 모두 추가 (언어별 키 일치 테스트가 있음).
- 새 라우트는 구조 변경이므로 `docs/system_design.md`를 먼저 수정하고 pokepedia 저장소 `CLAUDE.md`의 머지 정책을 따른다.
- 기존 퀴즈 코드는 삭제·수정하지 않는다. (예외: 9.7의 우선 출제 — `pickNextPokemon` 확장, 게임 완성 후 별도 PR)

### 9.5 조작 가능성

Unity는 규칙이 클라이언트에 있으므로 결과를 서버에서 검증할 수 없다. 그러나 **배틀 결과가 보상·랭킹에 쓰이지 않으므로** 조작은 본인 경험에만 영향을 준다. 서버 검증 구조는 도입하지 않는다. 승리 보상이나 공식 랭킹을 추가하게 되면 재검토한다.

### 9.6 컬렉션의 "배틀 가능" 필터 [선택, 게임 완성 후 결정]

컬렉션 화면에 `전체 컬렉션 | 배틀 가능` 필터를 추가하는 안이다 (획득한 스티커 ∩ 배틀 지원 포켓몬). 지원 ID 목록은 7.6의 `roster.json`을 단일 출처로 읽는다. Pokepedia 코드 변경이며(컬렉션 화면, 3개 국어 문구, `system_design.md` 선수정) DB 변경은 필요 없다.

### 9.7 퀴즈 우선 출제: 배틀 지원 포켓몬이 처음에 등장 [의도 확정, 세부는 게임 완성 후]

- **의도 [확정]:** 퀴즈에서 배틀 지원 포켓몬이 **무조건 처음에 등장**하도록 수정한다. 세부(적용 범위, 방식)는 게임 완성 후 정한다.
- **구현 방향 [권장안]:** `pickNextPokemon`에서 이번 run의 `seen`을 기준으로 **아직 나오지 않은 지원 포켓몬을 먼저** 뽑고, 모두 나온 뒤 일반 풀에서 고른다. DB 변경은 없다. 지원 ID는 Unity `roster.json`의 **빌드 시점 사본**(동기화 스크립트 포함)을 쓰고 퀴즈 서버 경로에서 다른 호스트를 런타임에 호출하지 않는다. Vitest를 추가하고 규칙 변경이므로 `docs/system_design.md` §5를 먼저 수정한 뒤 별도 PR로 진행한다.
- **부작용 (사용자와 확인한 사항):** 이렇게 하면 모든 run의 첫 6문제가 항상 같은 6마리(순서만 무작위)가 되어 퀴즈가 반복적이고 쉬워지며 해당 스티커가 편중될 수 있다. 세부 결정 때 대안을 검토한다: 사용자가 **아직 갖지 않은** 지원 포켓몬만 우선, 앞 K문제 중 일부만, 가중치 방식 등.

---

## 10. 웹 배포 인프라

### 10.1 개요 [권장안]

```text
브라우저
 ├─ Pokepedia (Vercel, 기존) ── iframe ──▶ Unity 정적 호스팅 (Cloudflare Workers 정적 자산 권장)
 └─ Supabase (기존) ◀── React만 접근. Unity는 접근하지 않음
```

- **새 서버·DB·게임 서버는 없다.** 필요한 것은 정적 파일 호스팅(CDN)뿐이다. 컬렉션은 기존 Pokepedia 백엔드가 React를 통해 읽어 전달한다.
- 배틀 상태(`BattleState`)는 **브라우저 탭 메모리**에만 존재한다 (클라이언트 측 stateful, 새로고침하면 초기화). 턴제라 서버 쪽 실시간 루프가 없다.
- 브라우저 로컬 저장소(PlayerPrefs)에는 언어·볼륨 같은 설정만 둘 수 있다.
- 화면 비율은 게임보이(약 10:9)를 기준으로 iframe 크기에 맞춰 스케일한다. 입력은 버튼·터치·키보드로 모두 처리 가능하다.

### 10.2 호스팅 선택지 (2026-10-16 문서 기준)

| 선택지 | 평가 | 근거·주의 |
|---|---|---|
| **Cloudflare Workers 정적 자산** | **권장** | 검색 결과 기준 Cloudflare는 신규 프로젝트를 Workers 정적 자산으로 시작하도록 안내하며 Pages는 계속 지원되고 deprecated가 아니다 (공식 문서 원문은 직접 확인하지 못함). 단일 파일 최대 **25 MiB**, 파일 수 Free 20,000개 / Paid 100,000개, 정적 자산 요청은 무료·무제한 |
| Cloudflare Pages | 대안 | 단일 파일 최대 25 MiB, 무료 플랜 파일 수 20,000개, 월 빌드 500회(Git 연동 빌드), `_headers` 규칙 100개까지 |
| Vercel (별도 프로젝트) | 가능, 대안 | CLI 배포 시 업로드 총량 Hobby **100 MB**, Hobby 월 Fast Data Transfer 첫 **100 GB**, 하루 배포 100회. **단일 파일 크기 한도는 문서에서 확인하지 못함.** **Hobby는 비상업적 개인 용도로만 사용 가능** (포트폴리오 용도가 해당하는지 애매하면 지원팀에 문의) |
| GitHub Pages | 비추천 | 커스텀 헤더 불가 → Decompression Fallback 필요(로더 증가, wasm 스트리밍 불가) |
| 개인 서버 | 비추천 | 공개 게임용으로는 가동·보안 운영 부담 |

최종 선택은 M0 검증 후 확정한다.

### 10.3 호스팅 설정

- Unity Web 빌드는 압축 형식에 맞는 `Content-Encoding`(`br` 또는 `gzip`)과 **`.wasm`의 `application/wasm` MIME**이 필요하다 (스트리밍 컴파일). Brotli는 Chrome·Firefox에서 HTTPS에서만 기본 지원한다. 압축을 끄면(Disabled) 파일이 25 MiB 한도를 넘을 수 있으므로 M0에서 크기를 확인한다.
- 서버에 이미 자동 압축이 있으면 이중 압축이 될 수 있으므로 실제 응답 헤더를 확인한다. **Vercel은 `wasm`·`javascript` MIME을 자동 압축**하므로(문서 기준) Unity 압축을 Disabled로 두는 방법이 있다 (`.data` 같은 `application/octet-stream`은 자동 압축 대상 아님). Cloudflare의 자동 압축 대상과 사전 압축(`.br`) 파일에 `Content-Encoding`을 직접 지정하는 방식은 확인하지 못했으므로 M0에서 검증한다.
- `Content-Security-Policy: frame-ancestors`로 임베드를 허용할 도메인(Pokepedia, 포트폴리오 허브)만 지정한다. `roster.json`을 Pokepedia가 읽는다면 CORS 헤더를 설정한다.
- 캐시: 로더·`index.html`은 재검증, 해시 파일명(`Name files as hashes`, 구현 시 확인)을 쓰면 빌드 산출물은 장기 캐시.

### 10.4 도메인과 환경

- 서브도메인 **`battle.pokepedia.dev`** [확정]. 주의: Workers 커스텀 도메인은 해당 도메인(zone)이 Cloudflare에 있어야 한다는 보고가 있다 (검색 결과, 미확인). `pokepedia.dev`의 DNS 위치를 확인하고, Cloudflare가 아니면 DNS 이전 또는 `*.workers.dev` 주소를 쓴다 [미정].
- Pokepedia에 `NEXT_PUBLIC_BATTLE_URL` 환경변수로 iframe 주소를 둔다 (Prod/Preview 분리 가능, 기존 Zod 환경변수 스키마에 등록).
- Pokepedia의 `next.config.ts`와 `vercel.json`에는 CSP 헤더 설정이 없다 (`src/proxy.ts`는 미확인). iframe 임베드는 추가 설정 없이 될 가능성이 높다.

### 10.5 빌드·배포·CI

- **초기: 로컬 Unity 빌드 → 스크립트로 업로드** (`wrangler deploy`, `assets.directory`에 빌드 폴더 지정). Unity CI(GameCI 등)는 라이선스·캐시 설정이 번거로워 이후 검토한다.
- **CI에서는 순수 C# 도메인 테스트만** 실행한다 (GitHub Actions + `dotnet test`). Domain이 UnityEngine을 참조하지 않으므로 별도 테스트 csproj가 Domain 소스를 링크해 Unity 라이선스 없이 매 푸시마다 검증할 수 있다.
- 주의: Unity에서 `record`의 `init` 접근자를 쓰려면 `System.Runtime.CompilerServices.IsExternalInit` 타입을 직접 정의해야 하는 것으로 알고 있다. M0/M1에서 확인한다.
- 빌드 산출물(`Builds/` 등)은 저장소에 커밋하지 않는다.

### 10.6 모니터링·보안·비용

모니터링은 Cloudflare Web Analytics 정도면 충분하다. 비밀값은 없고, `init`의 `event.origin` 대조와 `frame-ancestors`로 임베드를 제한한다. 비용은 사실상 0원이다.

### 10.7 서버가 필요해지는 시점

승리 보상, 공식 랭킹, 클라우드 세이브, 온라인 PvP를 추가할 때다. 이 경우에도 **새 게임 서버를 만들지 않고 기존 Pokepedia 백엔드를 확장**하는 것을 기본으로 한다. 현재 범위에서는 모두 제외한다.

### 10.8 Pokepedia 호스팅 이전(Vercel → Cloudflare) [범위 밖, 별도 프로젝트로 검토]

Unity를 Cloudflare에 올리는 것이 Pokepedia 이전을 요구하지 않는다 (별도 origin + postMessage). 이전하려면 검색 결과 기준으로 다음을 확인해야 한다: Cloudflare의 OpenNext 가이드(2026-08 갱신)는 신규 Next.js 앱에는 vinext를 권장하고 OpenNext는 기존 앱 유지용이라고 안내한다, **Node.js 미들웨어(15.2+)는 아직 미지원**으로 표시되며 Pokepedia는 `src/proxy.ts`를 쓰므로 호환 확인이 필요하다(런타임 종류는 미확인), 함수 지역을 도쿄(`hnd1`)에 고정해 줄인 DB 왕복 지연이 Workers에서 다시 늘 수 있다. 위험과 작업량이 크므로 Unity 완성 후 별도로 검토한다.

---

## 11. GitHub 저장소 전략

```text
ottuck/pokepedia          # 유지. 연동에 필요한 최소 변경만
ottuck/pokepedia-unity    # Unity / C# [확정]
```

- 두 프로젝트의 이력은 분리한다. 코드는 MIT, 콘텐츠(스프라이트)는 별도 고지. Unity 표준 `.gitignore`(`Library/`, `Temp/`, `Logs/` 등)를 초기에 설정하고 스프라이트는 8.1 방침에 따라 커밋하지 않는다.
- **분리 이유:** Unity 프로젝트는 구조·도구·CI가 Pokepedia(Next.js)와 전혀 다르고 배포도 따로 한다. 합치면 Pokepedia의 CI(`check`, `db`)가 Unity 변경에도 영향을 받는다.
- **접점은 세 가지뿐:** iframe 주소(`NEXT_PUBLIC_BATTLE_URL`), `postMessage` 규약(9.2), (선택) `roster.json` 매니페스트. Pokepedia 쪽 변경은 **pokepedia 저장소의 별도 PR**로 진행하고 그 저장소의 `CLAUDE.md` 정책을 따른다. 메시지 규약은 Unity 저장소 문서를 단일 출처로 한다.

### 포트폴리오 서사

- **Quiz → Collection → Battle** 루프로 기존 서비스에 새 활용처 부여, TypeScript/React(퀴즈)와 C#/Unity(배틀)의 역할 분담, 순수 C# 엔진과 Unity 화면의 분리 설계·이벤트 기반 연출, Next.js와 Unity 웹 게임의 iframe 연동.

| 기술 | 보여줄 결과물 |
|---|---|
| C# 도메인 설계 | 독립적인 턴제 배틀 엔진 |
| NUnit 테스트 | 타입 상성·데미지·명중·급소 검증 |
| Unity Animator | 포켓몬별 공격·피격 연출 |
| Particle System | 전기·물·불꽃 효과 |
| Shader | 피격·색상 변화·특수 효과 |
| Unity Web Build | 브라우저에서 실행되는 게임 |
| React 연동 | 기존 Pokepedia 컬렉션 활용 |

- 짧은 플레이 영상(GIF)과 아키텍처 설명을 함께 제시하고, 소개 문구에 실제 지원 수를 정확히 표기한다 (예: "N종을 공통 전투 시스템으로 지원하며 대표 데모에는 고급 연출을 적용했습니다").

---

## 12. 로드맵

마일스톤 표, 현재 상태, 진행 기록은 [ROADMAP.md](ROADMAP.md)가 단일 출처다. 개발 단계는 그 문서에 기록하며 진행한다. 범위와 단계 구분의 근거는 5.4를 따른다.

---

## 13. 확정된 사항

| 항목 | 내용 |
|---|---|
| 프로젝트 | Quiz → Collection → Battle 루프, 소규모 Unity 배틀 포트폴리오. 퀴즈는 Pokepedia(React) 유지 |
| 지원 범위 | **6마리**: Showcase 2(피카츄, 거북왕) + Standard 4(리자몽, 이상해꽃, 팬텀, 뮤츠). 151마리 지원은 필수 아님. 지원 목록은 `roster.json` |
| 품질 등급 | Showcase / Standard, 엔진 하나, 연출은 오버라이드 데이터 |
| 개발 순서 | Vertical Slice 우선. 마일스톤 시간 상한·Showcase 완료 정의 세부는 정하지 않음 |
| 완료 기준 | 1절의 네 가지 (포켓몬 수는 기준 아님) |
| 전투 흐름 | 트레이너·인트로 없음, 행동 메뉴 싸우다/도망, **난이도 하나**, **도망치면 상대 포켓몬이 바뀜**, 승리·패배 후 다음 상대/다시 도전, Demo는 도망 비활성 (5.3) |
| 향후 확장 | **3:3 배틀, 포켓몬 교체·가방 메뉴** (1:1 완성 이후, 시점·규칙 미정) |
| 전투 규칙 | 1:1, 현대 기준 데이터, 레벨 50·개체값 31·노력치 0, 기술별 물리/특수, **명중·급소 포함**, PP·상태이상 제외 (6.4) |
| 밸런스 | 전투 4~6라운드(기술 위력 값으로 조절, HP 배율 규칙 없음), 종족값 압축, **전설은 드물게 등장**, Demo 피카츄 승률 60~70%를 시드 고정 시뮬레이션으로 검증 (6.5) |
| 기술 | Unity 6.3 LTS, C#, uGUI, 순수 C# Domain, NUnit, JSON |
| 엔진/콘텐츠 | 처음부터 분리, 엔진은 지원 포켓몬 수를 모름 |
| 스프라이트 | PokeAPI **5세대 블랙·화이트 애니메이션**(앞/뒤/shiny GIF → Unity 변환), 저장소에 커밋하지 않음 (스크립트+매니페스트), 권리 검토는 제외 |
| 사운드 | AI 활용 8비트 + CC0 소스 우선, 매니페스트로 출처 관리, 울음소리는 품질이 좋으면 사용 1순위·아니면 비슷하게 제작 |
| 연동 | iframe + postMessage, **단방향**, 지원 필터는 Unity 내부 교집합, 경로 `/[locale]/battle` |
| 모드 | Demo(피카츄 vs 거북왕 고정) / Collection(보유 ∩ 지원). 판별은 `?host=pokepedia` + `init` 대기, 실패 시 [다시 시도], Demo는 명시적 선택 |
| DB | Pokepedia DB 변경 없음. Unity는 인증·DB 직접 사용 안 함 |
| 보상 | 배틀 승리 보상·랭킹 갱신 없음 |
| 인프라 | 자체 서버 없음, 정적 호스팅(Cloudflare Workers 정적 자산 권장), 서브도메인 `battle.pokepedia.dev` |
| 저장소 | `ottuck/pokepedia-unity` (Pokepedia와 분리) |
| 모바일 | 앱 출시 없음. **모바일 브라우저에서 화면이 완벽하게 보이는 것이 목표** |
| Pokepedia 쪽 | 퀴즈에서 지원 포켓몬이 처음에 등장하도록 수정(의도 확정, 세부는 게임 완성 후) |

## 14. 아직 결정하지 않은 사항 (에이전트가 임의로 확정하지 말 것)

**M0 측정·청취로 정해지는 것**
- 폰트 최종 변형 (Galmuri11/14, 글리프 검사)
- 울음소리 사용 여부 (청취 후)
- GIF의 Unity 변환·재생 방식
- 호스팅 최종 선택, `pokepedia.dev` DNS 위치에 따른 서브도메인 방식

**에이전트에 위임 (완료 후 문서에 정리)**
- 전투 수치 세부: 위력·명중률 값, 급소 확률(1/16 가정), 압축 계수 k, 전설 가중치, 상성 편향, 이상치 보정 (6.5)
- 전투 흐름 세부: 승리·패배 화면 구성 (5.3)
- 공격 연출 종류(`animKind`) 분류, 매니페스트 전달 방식, 프로토콜 요청 ID 필요 여부, 사운드 구체 소스·선곡

**게임 완성 후 결정**
- Pokepedia 쪽 연동 세부: 퀴즈 우선 출제의 적용 방식(9.7), 컬렉션 "배틀 가능" 필터(9.6), 퀴즈 보상 화면의 배틀 안내
- 3:3 배틀·포켓몬 교체·가방 메뉴의 시점과 규칙 (하기로 한 향후 확장, 1순위는 1:1 완성도), 이후 로스터 추가 시점

**별도 프로젝트**
- Pokepedia 호스팅 이전(Vercel → Cloudflare) 여부 (10.8)

---

## 15. 개발 에이전트 지침

이 프로젝트의 핵심은 AI가 코드를 생성하는 것이 아니라 **C#과 Unity를 이해하며 만드는 것**이다.

1. 주요 기술 선택에는 이유와 대안을 간단히 설명한다.
2. 새 C#/Unity 개념은 Java와 비교해 설명한다 (7.4 표 참고).
3. Unity 생명주기와 순수 C# 도메인 로직을 구분한다.
4. 규칙은 **테스트를 먼저** 작성한다.
5. 불필요한 프레임워크·인프라를 추가하지 않는다. 과한 추상화를 피한다.
6. 초기부터 웹 빌드 가능성을 유지하고 정기적으로 WebGL 빌드를 확인한다.
7. **Pokepedia 코드·DB는 함부로 수정하지 않는다.** 수정 전 해당 소스, `docs/system_design.md`, pokepedia 저장소 `CLAUDE.md`의 머지 정책을 확인한다. DB 변경이 꼭 필요하면 additive만 허용하고 Prod/Preview가 같은 DB임을 기억한다.
8. 엔진 코드에 포켓몬 이름·스프라이트·문자열을 넣지 않는다. 포켓몬별 코드를 만들지 않는다. **엔진은 로스터 크기를 알지 못한다.**
9. 기능 개발과 에셋 제작을 분리한다. 엔진은 에셋 없이 동작해야 한다.
10. **Showcase 데모도 실제 엔진을 사용해야 한다.** 가짜 전투 로직이나 미리 만든 연출만 재생하지 않는다. Standard와 Showcase를 별도 시스템으로 만들지 않는다.
11. **Vertical Slice 우선**: 콘텐츠를 늘리기 전에 피카츄 vs 거북왕을 먼저 완성한다. 로스터 확장은 데이터 추가 중심으로 한다.
12. **Pokepedia·PokeAPI에 대한 사실을 추측으로 채우지 않는다.** 코드·문서·API를 확인한 뒤 말한다. 외부 제안(GPT 등)의 사실 주장, 특히 기술 이름·규칙·라이선스는 그대로 믿지 않는다.
13. **단순한 사실(버전, 한도, 가격 등)은 에이전트가 웹 검색으로 직접 확인해 문서에 반영한다.** 출처와 확인하지 못한 부분을 함께 적는다.
14. 사용자는 포켓몬 게임 도메인 지식이 약하므로 **규칙·용어는 먼저 설명**하고 추천안과 이유를 제시한 뒤 결정한다. [위임] 항목은 에이전트가 정하되 결과를 문서에 정리한다.
15. 사운드·에셋은 출처와 라이선스를 매니페스트에 기록한다.

### Unity CLI·에이전트 연동 (2026-10-16 확인)

외부 제안(GPT)의 "Unity MCP 도입" 내용을 공식 문서로 검증한 결과다. 출처: [Unity CLI as the replacement for the in-Editor MCP server](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli).

**확인된 사실**
- 에디터 안 AI Assistant 패키지(`com.unity.ai.assistant`)에 들어 있던 MCP 서버는 deprecated이고, Unity CLI의 `unity mcp`가 대체한다. CLI 자체는 deprecated가 아니다.
- 에이전트가 셸 명령을 실행할 수 있으면 MCP보다 `unity command`·`unity eval`로 에디터를 직접 조작하는 쪽이 빠르고 토큰도 적게 든다고 공식 문서가 안내한다. 따라서 **Claude Code는 MCP가 아니라 CLI 직접 호출을 기본으로 한다.** MCP는 셸을 못 쓰는 환경용 대안이다.
- 연결 절차(공식): CLI 설치 → `unity pipeline install` → (MCP가 필요하면) `unity mcp configure <client>` → `unity skill install <agent-name>`.
- 실행 중인 에디터 조작은 Unity 6.0 LTS 이상 필요. 현재 6000.3.26f1은 해당된다. CLI는 무료이며 Unity AI 구독과 무관하다. 로컬 연결만 받는다(localhost).
- 에디터 없이 헤드리스 빌드·테스트 실행도 가능하다. 이 저장소는 이미 `unity test . --mode EditMode`를 쓰고 있고, 로컬에 CLI `1.0.0-beta.13`이 설치돼 있다.
- 아직 `unity pipeline install`은 하지 않았다(`Packages/manifest.json`에 파이프라인 패키지 없음). 설치하면 패키지가 추가되므로 PR로 변경을 확인한다.

**확인하지 못한 것 (그대로 믿지 말 것)**
- GPT가 적은 `winget install Unity.CLI` 패키지명과 `codex plugin marketplace add Unity-Technologies/unity-agent-plugin` 명령. 설치는 공식 Install Unity CLI 절차를 따른다.
- `unity skill install`에서 쓸 에이전트 이름(`--help`로 확인).
- `unity command`·`unity eval`이 실행 중인 에디터를 반드시 필요로 하는지. 문서의 "running Editor" 표현에서 추정한 것이다.
- GPT가 전한 CoreCLR 로드맵(Unity 7.0에서 CoreCLR 에디터 도입 등). 공식 확인 전이다. 어느 쪽이든 WebGL 빌드는 IL2CPP라 6.3 LTS 유지 결정에는 영향이 없다고 본다.

**병렬 에이전트 운영 규칙**
- Unity 에디터는 프로젝트 폴더당 하나만 열 수 있다(`Library/`와 `Temp/UnityLockfile` 점유). 에디터가 열린 폴더에서는 batchmode 실행(컴파일 확인·테스트·빌드)이 실패한다.
- 에디터 조작이 필요한 작업(씬 배치, 파티클, UI)은 에이전트 한 명이 순서대로 맡는다. 순수 C# 로직(`Domain`, `Content`, `Application`)과 `dotnet test`는 별도 worktree에서 병렬로 진행할 수 있다.
- IDE(Rider·VS Code)는 에디터와 별개라서 에이전트 작업 중에도 같이 열어도 된다.
- worktree를 새로 만들면 `Library/`를 다시 임포트하느라 첫 실행이 오래 걸린다.
- `unity eval`은 에디터에서 임의 C#을 실행할 수 있다. 권한 허용 목록에는 필요한 명령만 넣는다.

### 새 에이전트 세션의 첫 작업

이 문서를 **별도 세션(Opus 모델)** 에 전달해 작업을 시작한다.

1. **M0 PoC** 수행: 폰트(Galmuri11/14 + TMP Static), WebGL 빌드·호스팅 헤더, iframe 왕복(`ready` → `init`), 키 입력, 피카츄 공격 연출 1개, BW GIF 변환·재생, 모바일 브라우저 레이아웃, 오디오 재생(START 버튼)과 울음소리 청취 비교.
2. M1(정식 초기화) → M2(엔진, TDD, 시드 고정 시뮬레이션으로 수치 조정) → M3(피카츄 vs 거북왕 Vertical Slice) 순으로 진행한다.
