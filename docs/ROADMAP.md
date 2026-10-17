# 로드맵

개발 단계 현황과 진행 기록. **에이전트는 작업 전에 "현재 상태"를 확인하고, 단계가 바뀌거나 완료·주요 결정이 생기면 같은 PR에서 이 문서를 갱신한다.** 범위·설계는 [PROJECT.md](PROJECT.md), 에이전트 규칙은 [CLAUDE.md](../CLAUDE.md)에 있다. 마일스톤별 시간 상한은 두지 않는다 [확정].

## 현재 상태

- **다음 작업:** 없음 — 한 흐름(퀴즈 → 띠부씰 → 배틀) 완성. 확장은 보관(사용자 결정, 2026-10-17). 이후에는 흐름이 부드럽게 유지되는지(웹·모바일)만 본다
- 마지막 갱신: 2026-10-17

## 마일스톤

| Milestone | 내용 | 완료 기준 | 상태 |
|---|---|---|---|
| M0 | 검증용 최소 Unity 프로젝트 + PoC 9가지 (항목은 M0 결과 요약 참고) | 9가지 항목 결론 | 완료 → [M0 결과](#m0--poc-결과) |
| M1 | **정식** Unity 프로젝트·저장소 초기화 (M0 결과 반영), asmdef, 테스트 환경, 웹 빌드 설정, 진입 화면·브리지 | 정식 프로젝트가 웹에서 실행 | 완료 |
| M2 | 순수 C# 엔진 (포켓몬 2마리): 타입 상성(18×18), 데미지, 명중·급소, 턴 순서, 승패, NPC AI, 시드 고정 시뮬레이션으로 수치 조정 | UI 없이 1:1 전투를 테스트로 검증 | 완료 |
| M3 | **Vertical Slice — 피카츄 vs 거북왕**: 콘텐츠 로딩(로스터 2마리, 기술 12개), 5.3의 전투 흐름과 화면, 이벤트 재생, 공격·피격·급소·빗나감 연출, 사운드, 2.5D 화면 연출 (Showcase 품질, PROJECT.md 5.5) | 실제 엔진을 쓰는 한 판이 처음부터 끝까지 플레이됨 | 완료 (M3-1 흐름·화면, M3-2 연출, M3-3 사운드, M3-4 월드/HUD 분리, M3-5 2.5D 연출, 프레임 측정: 데스크톱 75·아이폰 Safari 50~60 FPS) |
| M4 | 웹 배포, 포트폴리오 소개 (**Demo Mode**, 최적화는 M7로 옮김). Cloudflare 실배포 헤더 검증 포함 | 외부 사용자가 로그인 없이 단독 플레이 가능 | 완료 (Cloudflare Pages 배포·`.br` 헤더 검증·`battle.pokepedia.dev` 연결·README 소개. 포트폴리오 허브 항목 추가는 다른 저장소라 별도) |
| M5 | **연출 완성 — 피카츄 vs 거북왕 Demo 그대로**: **VS 도입 장면**, 공격 카메라 연출(2.5D: 확대·이동·기울기, 3D 아님), 이펙트 다듬기, 남은 확인(번개 번쩍임 타이밍), **마지막으로 M6 준비: `BattleScreen` 분리(실험실·UI 구성·흐름/이벤트 재생을 나눔)와 표시 상태 enum(연결 → 선택 → 진입 → 명령 → 연출 → 결과)** — GPT 리뷰 P1·P2, Collection 모드가 붙기 전에 | Demo 한 판이 도입부터 승패까지 완성된 연출로 플레이됨 | 완료 (VS 도입 장면, 공격 카메라, 몸통박치기, 배경 번개 사용자 확인, M6 준비 리팩터링) |
| M6 | **Pokepedia 연동 (Collection Mode)**: iframe 진입, `ready` → `init`, **피카츄 띠부씰이 없는 사용자**의 첫 퀴즈는 **피카츄 실루엣** → 정답 뒤 띠부씰과 함께 "내 피카츄로 배틀" 권유, 컬렉션에서 배틀 가능 포켓몬(피카츄) 황금 테두리(9.6·9.7). 배틀 입구는 체험(Demo)과 피카츄 띠부씰뿐, 플레이어는 피카츄만(9.2). Pokepedia 쪽은 그 저장소의 별도 PR | Pokepedia 퀴즈에서 배틀까지 끊김 없이 이어짐 | 완료 (Unity Collection 모드·로딩 화면 배포, Pokepedia 배틀 페이지·첫 퀴즈 피카츄·컬렉션 금색 테두리, Production 흐름 확인, 사용자 점검 뒤 다듬기: 첫 화면 언어·휴대폰 기술 메뉴·결과 문구·모바일 배틀 화면 크기) |
| M7 | **최적화** → **용량 측정만 하고 끝냄**(사용자 결정, 2026-10-17: 사용자 휴대폰(4G)에서 데모 첫 로딩·플레이 모두 문제 없음). 로딩 화면은 M6에서 넣음 | 용량 내역 기록 | 완료 (전송 13.4MB = 코드 7.7 + 데이터 5.6, 데이터의 2/3가 미공개 Standard 4종 스프라이트 — PROJECT.md 10.3) |
| 이후 | **보관(사용자 결정, 2026-10-17 — 지금은 하지 않음)**: 로스터 4종(리자몽·이상해꽃·팬텀·뮤츠) Standard 연출·체형별 표시·밸런스·공개(그림은 빌드에서 뺐고 데이터만 보관, PROJECT.md 10.3), 3:3·교체·가방, 로스터 추가. ("보상 화면 연동" = 퀴즈 띠부씰 획득 화면에서 배틀로 안내는 M6에서 끝남) | | 보관 |

상태 표기: 예정 / 진행 중 / 완료. 마일스톤이 바뀌면 위 표와 "현재 상태"를 함께 고친다.

## 진행 기록

최신이 위. 한 줄에 날짜, 한 일, 근거(커밋·PR·문서 절)를 적는다.

- 2026-10-17 **사용자 피드백 반영: 승리 끝·흰 깃털·시작/로딩 화면·넓은 프레임.** ① 상대가 1마리인 배틀은 이기면 결과 메뉴 없이 승리 장면에서 끝(사용자 선택). ② 부활 깃털이 나뭇잎처럼 보여 금빛을 빼고 새하얀 깃털(깃 갈라짐, 깃대가 빠져나온 모양)로. ③ 시작 화면·로딩 화면을 Pokepedia 퀴즈 시작 화면 느낌으로(하늘·구름·반짝이·도트 테두리 로고·▶ PRESS START, 피카츄·거북왕 그림자). ④ Pokepedia(PR 68): 게임·배틀 프레임을 컬렉션 폭으로 넓힘 — 배틀은 빨간 도감 기기 프레임 안에서 10:9 게임이 창 높이만큼 커지고 옆은 어두운 화면 테두리(이 저장소 템플릿 `#262a35`), 휴대폰은 그대로 가장자리까지. PROJECT.md 5.3·5.5·9.2.

- 2026-10-17 **쓰러지면 선택 + 거북왕 기술 섞기**(사용자 요청 "한 번 부활하고 즐겼으면 바로 끝내고 싶은 유저도 있다", "1:1 두 마리뿐이니 확실히 멋진 전투로"). ① 플레이어가 쓰러질 때마다 [○○을(를) 응원한다(기존 부활) / 트레이너가 처리한다] — 후자는 트레이너가 던진 몬스터볼이 거북왕 머리에 "퍽" 맞고(별이 돌며 비틀) 거북왕이 쓰러져 승리. 기존 "판마다 1번 부활"은 선택으로 바뀜. ② NPC AI가 턴마다 기술을 섞음(1·3턴 최강, 2턴 2순위, 4턴 3순위) — 거북왕이 하이드로펌프·물의맹세·몸통박치기를 모두 씀. ③ 물대포 → **물의맹세**(PokeAPI 확인), 새 연출 `hydro_geyser`: 주변 땅이 울리고 거품이 끓다가 둘레에서 작은 물기둥 4개가 차례로 솟고, 마지막에 발밑에서 큰 물기둥이 솟아 상대를 띄움(여러 물기둥은 사용자 요청). ④ 밸런스 재조정: 하이드로펌프 위력 35 → 45, 물의맹세 30 — Demo 피카츄 승률 90.9%·평균 4.95턴(범위 85~93%). 실험실에 물기둥·거북왕 몸통박치기·몬스터볼 추가. 테스트 먼저(세션 선택 흐름·AI 순환·메시지 키), dotnet 108개. PROJECT.md 5.3·5.5·6.3·6.4·6.5·13.

- 2026-10-17 사용자 피드백: ① Pokepedia 퀴즈 화면 위 "정답을 맞히면 포켓몬 배틀" 안내 띠 제거(PR 67, 과해 보이고 지금은 피카츄만이라 정확하지도 않음 — 정답을 맞힌 사람만 획득 화면 버튼으로 배틀에 이어짐). ② 기술 버튼: 한 번에 바로 쓰므로 "고른 버튼" 흰 테두리 표시를 없애고, 누르는 순간 2px 눌렸다 튀어 오르는 반응(약 0.2초) 뒤 기술 사용. 키보드 방향키로 고를 때만 지금 버튼 표시. PROJECT.md 5.3.

- 2026-10-17 **사용자 피드백 반영·배포**(Unity PR 47 → battle.pokepedia.dev, Pokepedia PR 66 → pokepedia.dev). ① 부활 연출 강화: 카메라 다가감 + 하늘에서 금빛 빛기둥 + 화면 전체 성스러운 깃털 + 금빛 화면 번쩍임. ② 도망 확인: 도망칠 수 없는 배틀에서 두 번째 "도망"부터 "정말 도망칠까?" [그만둔다/계속 싸운다] — 갇힌 느낌 방지. ③ Standard 4종 그림을 빌드에서 뺌(매니페스트에서 제외, 데이터는 보관): 데이터를 푼 크기의 텍스처 27.9 → 8.3MB, 전송량은 13.4 → 13.0MB(도트 그림은 원래 압축이 잘 돼서 실제 전송은 0.4MB만 줄었다). ④ Pokepedia 퀴즈 화면 위 금빛 안내 띠 "정답을 맞히면 포켓몬 배틀! — 첫 문제를 맞히면 그 포켓몬으로 바로 배틀할 수 있어요"(피카츄를 가지면 배틀하기 링크, 첫 문제를 넘긴 판에서는 숨김 — Codex 지적 반영). 데스크톱·모바일 Preview와 Production 확인. 이후 단계는 보관(사용자 결정).

- 2026-10-17 **응원 부활**(사용자 요청): 데모 전투(와 피카츄 띠부씰로 들어오는 첫 Collection 전투)에서 처음 쓰러지면 "트레이너의 응원"으로 HP 가득·주는 피해 ×1.5로 다시 일어섬, 판마다 1번. Domain `BattleEngine.Revive`·`Revived` 이벤트(피해 배율 `Battler.PowerBoost`), Application `BattleSession` `cheerRevives`, 메시지 2줄(3개 언어), 금빛 빛·반짝임 속 재등장 + HP 회복 연출. 테스트 먼저(엔진 부활·세션 1번·메시지 키), dotnet 107개. Dev 빌드에서 몸통박치기만 써서 져 보고 흐름 확인. PROJECT.md 5.3·13.
- 2026-10-17 **M7 정리**: 최적화는 하지 않기로(사용자 휴대폰 4G에서 문제 없음), 용량만 측정해 기록 — 전송 13.4MB(코드 7.7MB·데이터 5.6MB), 데이터를 풀면 30.7MB 중 텍스처 91%, 그중 미공개 Standard 4종 스프라이트 19.7MB. PROJECT.md 10.3.

- 2026-10-17 **기술 메뉴를 큰 타입 색 버튼 3개로**(시안 C, 사용자 결정 — 시안 A 타입 판·B 막대·C를 실제 폰트로 그린 비교 페이지에서 고름). PR 44 → battle.pokepedia.dev 배포(해시 일치). `MoveCards`: 타입 색 그라데이션 버튼(코드로 그린 도트 텍스처)에 이름·위력·명중, 고른 버튼은 흰 테두리·그림자로 2px 떠오름, 버튼 전체가 눌림 영역, 바깥을 누르면 뒤로, 터치도 바로 사용(미리보기 탭 제거). 긴 이름은 글자 간격만 좁힘. 영어 명중 "Acc", 일본어 "威力·命中". ko·en·ja Dev 빌드 확인. PROJECT.md 5.3.

- 2026-10-17 **사용자 아이폰 피드백 반영·배포**(Unity PR 42 → battle.pokepedia.dev, Pokepedia PR 65 → pokepedia.dev). 데모 첫 로딩 약 4초로 충분(사용자 확인). ① 기술을 바꿀 때 "EncodingError: Decoding failed" 경고창: 커서·HP 틱 효과음(약 0.05초)을 iOS 사파리가 풀지 못함 → 구운 효과음 최소 0.25초(무음 꼬리), 템플릿 `errorHandler`로 소리 디코딩 오류는 콘솔에만. ② 하이드로펌프가 가끔 짧은 세로 조각: 끝에 꼬리를 쓸어 지우는 0.16초가 휴대폰에서 한두 프레임만 보임 → 전체가 가늘어지며 옅어지게. ③ 기술 메뉴 간격·타입 색 점·정보 상자 타입 띠(임시, 사용자가 더 세련된 안을 원해 시안 A 타입 판·B 막대·C 큰 타입 버튼을 보여 드림). ④ 첫 유저 경험(사용자 요청): 피카츄를 처음 얻으면 획득 화면에 금빛 "⚔ 내 피카츄로 배틀" 버튼 하나만. Codex 지적 반영: 배틀 링크도 `advance()`로 다음 문제를 준비(돌아오면 같은 획득 화면이 다시 뜨던 문제), 첫 배틀 판단은 첫 문제(seq 1)에서만(`isNew`가 변형 하나만 세서). Preview에서 첫 문제 피카츄 → 금빛 버튼 → 배틀 → 돌아오면 2번째 문제 확인.

- 2026-10-17 **M6 다듬기 완료·배포**(Unity PR 40 → battle.pokepedia.dev, Pokepedia PR 64 → pokepedia.dev). Unity: 게임 첫 화면 언어를 `?lang`(없으면 브라우저 언어)으로, 기술 메뉴는 바깥을 누르면 뒤로·터치는 첫 탭 미리보기 뒤 한 번 더 눌러 사용, 기술을 한 줄에 하나씩(최대 3개)·정보 상자 넓힘(영어 "Thunderbolt"·"Accuracy 100" 잘림 해결), 상대 1마리면 승리 뒤 "한 번 더 싸운다". Pokepedia: 휴대폰에서 배틀 iframe을 좌우 끝까지(정수 배율 한 단계↑, 375px·2배 화면에서 2배→3배), "배틀하기" 배지 85×28px. 확인: 로컬 Dev·Release 빌드(en·ja 첫 화면, 바깥 클릭 뒤로, 영어 기술 이름, "Battle again", 375px 직접 열기 캔버스 750×675 = 3배), Production(en 페이지 첫 화면 영어, 배지 크기). **미확인:** 실제 휴대폰에서 터치 미리보기(에뮬레이터는 클릭을 마우스로 보냄)와 iframe 안 3배 배율(에뮬레이터가 다른 도메인 iframe에 화면 배율을 넘기지 않아 2배로 보임) — 사용자 휴대폰에서 확인 필요.

- 2026-10-17 **사용자 관점 점검(Production, 모바일 375px·데스크톱, ko·en)** → M7 전에 "M6 다듬기"를 넣음. 찾은 것: ① en·ja 페이지에서도 게임 타이틀·시작 버튼이 한국어(언어가 `init` 뒤에야 바뀜), ② 모바일에서 기술 메뉴를 빠져나갈 방법이 없음(취소가 키보드 X·Esc뿐), ③ 메뉴 한 줄 높이 약 17px로 손가락에 작음, 터치는 누르는 즉시 선택돼 다른 기술의 타입·위력·명중을 미리 볼 수 없음, ④ 상대가 거북왕 하나인데 승리 뒤 버튼이 "다음 상대와 싸운다", ⑤ 정수 배율 때문에 일부 화면(DPR 2·375px 등)에서 게임이 프레임의 70% 정도로 작게 그려지고 테두리 여백이 큼, ⑥ 컬렉션 "배틀하기" 배지가 작음(높이 약 18px), ⑦ 첫 로딩이 길고(→ M7) 배틀 진입이 "내 피카츄로 배틀 → 로딩 → 시작" 두 번 누름(시작은 소리 재생 정책 때문에 필요). 정상 확인: 퀴즈 이어하기(배틀 다녀와도 점수·2번째 문제 유지), 피카츄 없음 안내, 360px 메뉴.

- 2026-10-17 **M6 완료** — Pokepedia PR 60(배틀 페이지)·61(첫 퀴즈 피카츄·획득 화면 배틀 버튼)·62(컬렉션 금색 테두리·배틀 배지) 병합(사용자 승인, 테두리 디자인 확정). PR 60의 Codex 지적(메뉴 5칸이 360px 일본어에서 넘침)은 모바일 탭 여백을 줄여 고치고 Preview에서 확인. **Production(pokepedia.dev)에서 새 게스트로 흐름 확인:** 첫 문제 피카츄 실루엣 → 정답 → "내 피카츄로 배틀" → `/battle`에서 VS 도입 → 피카츄 vs 거북왕, 컬렉션 피카츄 금색 테두리 + "배틀하기" 배지. 같은 날 사용자 지시로 **별문제 없는 작업은 묻지 않고 병합·배포**하도록 두 저장소 CLAUDE.md를 고침(이 저장소 PR 37, Pokepedia PR 63).

- 2026-10-17 M6 **Collection 모드를 battle.pokepedia.dev에 배포**(main `e921cd9`, Release 빌드). 서빙 파일 해시가 빌드와 일치, `.br` 헤더 정상, `frame-ancestors`에 `*.vercel.app`·`localhost:3000` 적용. **Pokepedia Preview(PR 62, P1~P3 포함)에서 전체 흐름 확인:** 띠부씰 없는 상태로 `/battle` → 안내 + [데모 체험], 퀴즈 첫 문제 피카츄 실루엣 → 정답 → 획득 화면 "내 피카츄로 배틀" → `/battle`에서 VS 도입 → 피카츄 vs 거북왕, 컬렉션 피카츄 금색 테두리 + "배틀하기" 배지. Pokepedia PR: 60(배틀 페이지)·61(첫 퀴즈 피카츄)은 그 저장소 정책상 구조 변경이라 사용자 승인 필요(`needs-approval`), 62(컬렉션 테두리)는 UI.

- 2026-10-17 M6 **U2 Unity Collection 모드**: `roster.json`에 `playable`(피카츄만), Standard 4종 `npcWeight` 0. 연결되면 보유 ∩ 플레이어 가능(`CollectionLineup`)으로 바로 VS 도입 → 피카츄 vs 거북왕, 색이 다른 피카츄는 shiny 스프라이트, 상대 후보가 1마리라 도망 불가, 승패마다 `battleEnded`(win/lose). 후보가 없으면 "퀴즈에서 피카츄 띠부씰을 얻으면…" 안내 + [데모 체험]. 웹 템플릿에 로딩 화면(진행 막대·`?lang` 안내), Vercel Preview origin 정규식 허용, `frame-ancestors`에 `*.vercel.app`·`localhost:3000`. 모의 호스트에 보유 상황 선택. dotnet 테스트 105개, WebGL Dev 빌드에서 shiny 진입·도망 불가·`battleEnded`·0마리 안내 확인. PROJECT.md 6.5·9.2.

- 2026-10-17 **M6 시작 — 설계와 결정**. Pokepedia 현황 확인: `/battle` 경로·내비 항목 없음, CSP 없음(iframe 막지 않음), 첫 문제는 `startOrResume`이 서버에서 무작위 선택, 컬렉션의 노란 테두리는 이미 색이 다른 스티커 표시. 사용자 결정: ① 첫 퀴즈 피카츄 조건은 **피카츄 띠부씰 미보유**(저장소 추가 없음, DB 변경 없음), ② **배틀 입구는 체험과 피카츄 띠부씰뿐, 플레이어는 피카츄만** → 거북왕 vs 피카츄 밸런스 문제는 해당 없음(이후 확장으로). Unity는 `roster.json`의 `playable`·`npcWeight`로 정하고 1마리면 선택 화면을 건너뜀. Vercel Preview origin은 정규식 허용. PROJECT.md 9.2·9.6·9.7·13·14.

- 2026-10-17 M5 결과를 **https://battle.pokepedia.dev 에 배포**(main `27316cb`, Release 빌드, `Tools/web/deploy.py`). 서빙 파일 해시가 빌드와 일치, `.br` 헤더 정상, 브라우저에서 VS 도입 확인.

- 2026-10-17 VS 도입 디자인 **A(대각선 띠)로 확정**(사용자 결정). 후보 B·C 코드와 실험실 순환을 지움. 다음은 M6. PROJECT.md 5.5·13.

- 2026-10-17 VS 도입 디자인 후보 B·C 추가(사용자 요청): `VsIntro`를 스타일 구조로 바꾸고 B 가로 띠 + 폭발 문장, C 스포트라이트 + 번개 균열을 더함. 실험실 VS 칸을 누를 때마다 A → B → C. 사용자 선택 대기. PROJECT.md 5.5.

- 2026-10-17 **M5 완료** — M6 준비 리팩터링(GPT 리뷰 P1·P2): `BattleScreen`(약 480줄)을 `BattleHud`(보이는 것), `BattleEventPlayer`(이벤트 재생·VS·승리 핸들), `BattleLab`(실험실), `BattleContext`(판 데이터)로 나누고, 흐름과 표시 상태 `BattleViewState`만 남김. 메뉴 입력은 맞는 상태에서만 받음. 동작은 바꾸지 않음(Dev 빌드로 일반 전투·실험실 회귀 확인). PROJECT.md 7.3.

- 2026-10-17 M5 몸통박치기 다듬기: 도움닫기 → 잔상 돌진(거리 30%) → 몸 표면 도트 별 타격 불꽃·파편·고리·흙먼지 → 잠깐 멈춤 → 복귀. 실험실에 "Tackle" 추가(사용자 요청). 배경 번개 번쩍임은 사용자가 보고 문제없다고 확인. WebGL Dev 빌드 시점별 캡처로 확인. PROJECT.md 5.5.

- 2026-10-17 M5 공격 카메라(2.5D): 준비 동작 동안 공격자 쪽 확대(1.12배) → 발사 순간 상대 쪽으로 휙(1.08배) → 맞는 순간 당김 → 가운데·1배 복귀, 승리 때 이긴 쪽 1.1배. 월드 Pan 노드를 옮기고 키우는 방식이라 HUD는 고정, 멈춰 있을 때는 항상 1배(도트 유지), 기울기는 도트가 깨져 뺌. ROADMAP M5에 마지막 단계로 "M6 준비: BattleScreen 분리·표시 상태 enum"(GPT 리뷰 P1·P2) 추가. WebGL Dev 빌드에서 10만볼트·하이드로펌프 시점별 캡처로 확인. PROJECT.md 5.5.

- 2026-10-17 M5 시작: **첫 로딩 기준값** 측정(전송 13.4MB, 이 PC 처음 방문 약 3.5초, Fast 4G 추정 13~15초 — M6 로딩 화면·M7 크기 줄이기 근거). **VS 도입 장면**(대각선 타입 색 띠, 실루엣 → "VS" 번쩍임과 함께 색, 약 2초, 건너뛰기, 실험실에서 반복 재생). 사용자가 고르지 않은 후보 연출 2개(쌍포·예전 물줄기)를 코드에서 지움. WebGL Dev 빌드에서 확인. PROJECT.md 5.3·5.5·10.3.

- 2026-10-17 로드맵 정정(사용자 결정): 첫 퀴즈를 **피카츄**로 해서 "내 피카츄로 배틀"이 지금 Demo(피카츄 vs 거북왕)와 그대로 맞게 함 → Demo 매치업 전환·밸런스 재조정·거북왕 뒷모습 연출은 필요 없어짐. 참고로 잰 값: 지금 규칙에서 거북왕(플레이어) vs 피카츄 승률 11.5%(5,000판), 피카츄만 원래 종족값이면 79.0% — Collection 모드(M6) 상대 선택 때 참고. PROJECT.md 6.5·9.6·9.7·13·14.

- 2026-10-17 **로드맵 재편(사용자 결정)**: 로스터 확장은 에셋·연출 반복 작업이 커서 맨 뒤 "확장"으로. 하나의 흐름 완성을 먼저 — M5 연출 완성(거북왕 vs 피카츄 전환, VS 도입, 카메라 연출), M6 Pokepedia 연동(첫 퀴즈 거북왕 실루엣 → 배틀 권유, 컬렉션 황금 테두리), M7 최적화(내용 확정 뒤 한 번에). 3D는 하지 않는다. PROJECT.md 5.1·9.6·9.7·13·14.

- 2026-10-17 **M4 완료**: README에 포트폴리오 소개(플레이 링크, 배포본에서 픽셀 그대로 뽑은 장면 4장 `docs/images/`, 지원 범위의 정확한 수 — 공개 Demo는 피카츄 vs 거북왕, 로스터 데이터 6종, 보여 줄 기술). 외부 사용자가 로그인 없이 https://battle.pokepedia.dev 에서 플레이 가능(완료 기준). 포트폴리오 허브(`portfolio-hub-nuxt`) 항목은 다른 저장소라 별도로 한다. PROJECT.md 11.

- 2026-10-17 `battle.pokepedia.dev` 연결: Cloudflare Pages 커스텀 도메인(My DNS provider → CNAME 방식, 사용자가 대시보드에서 등록) + Vercel DNS에 `battle CNAME pokepedia-battle.pages.dev`(Vercel CLI). 약 2분 뒤 HTTPS 200, 커스텀 도메인에서도 `.br` 헤더·`frame-ancestors` 정상(br을 받는 요청엔 그대로, 못 받는 요청엔 Cloudflare가 풀어서 보냄), 브라우저에서 전투 시작까지 확인. PROJECT.md 10.4.

- 2026-10-17 M4 시작: 호스팅을 **Cloudflare Pages**로 확정(사용자 결정. DNS가 Vercel이라 Workers 커스텀 도메인은 안 됨, 공식 문서 확인). 프로젝트 `pokepedia-battle` 생성, `Tools/web/_headers`·`deploy.py`로 첫 배포. `.br` 파일에 `Content-Encoding: br`이 그대로 나가고(재압축 없음) 브라우저에서 실행됨을 확인 — M0부터 남아 있던 Cloudflare 헤더 검증 완료. wrangler 4.151이 `pages project create`를 Worker로 바꿔 만들고 배포해 버려서 그 Worker는 지우고(사용자 허락) `--force`로 Pages를 다시 만들었다. PROJECT.md 10.2·10.3·10.5·13.

- 2026-10-17 **M3 완료**: 아이폰 Safari에서 `?fps`로 평균 50~60 FPS(사용자 측정, 화면 값 57~59 FPS·가장 느린 프레임 18~21ms). 60Hz 상한 근처라 후처리·조명은 그대로 둔다. 다음은 M4 웹 배포(호스팅·DNS 결정 필요). PROJECT.md 5.5.

- 2026-10-17 M3 마무리 준비: 실험실에 명중/빗나감/급소 결과 버튼(급소 연출과 문구 확인용), 실험실 연출이 보조 기술이 아니라 대표 기술로 재생되게 고침, `?fps` 프레임 표시(모바일 측정은 사용자). `BattleSession.NextBattle`/`Retry`는 배틀이 끝난 뒤에만 허용(진행 중이면 예외, 사용자 결정), `BattleSide` 출전 인덱스 범위 검사. 기존 테스트 하나가 진행 중 호출을 전제해서 도망으로 끝낸 뒤 부르도록 고쳤다. 하이드로펌프는 다시 직선(휘게 한 것은 수압이 안 느껴진다는 피드백으로 되돌림). GPT 리뷰 반영: 승리 메시지를 빨리 넘기면 승리 연출(뛰기·카메라)이 다음 배틀까지 돌던 것을 새 배틀 시작 때 멈추고, 조명 풀(6개)을 다시 쓸 때 이전 Pulse 가 새 조명을 끄지 않게 슬롯 토큰을 둠(전역 조명 페이드는 이미 토큰이 있어 안전했다). AnimationDirector 분리는 M5 로 미룸. Release 빌드에서 확인: 급소 연출과 문구, 결과 버튼 순환, 연타로 승리 메시지를 넘겨도 다음 판이 정상(콘솔 오류 없음), 데스크톱 75 FPS 유지. 개발 서버에 `--host`(휴대폰 측정용). PROJECT.md 5.5·14.

- 2026-10-17 하이드로펌프 다듬기(사용자 피드백): 입 위치가 첫 프레임 기준인데 머리가 프레임마다 움직이고 쏘는 동안 8px 밀려나 입과 물줄기가 떨어져 보였음 → 쏘는 동안 그 프레임에 고정, 반동 1px. 물줄기를 가로로 나가 휘어 떨어지는 곡선으로, 굵기는 17 → 11px(쌍포 수준). PROJECT.md 5.5.

- 2026-10-17 기술 3개(사용자 결정): 포켓몬마다 몸통박치기 + 속성 기술 2개. `role: secondary`(보조 기술)를 추가해 단일 타입 종이 받게 함 — 번개(위력 45·명중 70), 물대포(25·100), 염동력(28·100). 기대 데미지가 대표 기술보다 낮아 AI·Demo 밸런스는 그대로. 연출 선택 확정: 10만볼트 `thunder_arc`, 하이드로펌프 `hydro_beam`, 예비였던 `lightning_strike`·`hydro_burst`는 번개·물대포의 연출로 재사용. 10만볼트 시작점을 몸 바깥으로, 끝점을 상대 몸 표면으로 옮김. 하이드로펌프·물대포는 거북왕 입 바로 앞에서 발사(`animation.json`의 `mouth`, 종마다 앞모습 입 위치). 승리 연출에서 하늘이 실제로 바뀌게 함(맑은 노을 하늘 레이어, 1.4초). 메뉴 글자가 칸보다 길면 줄바꿈되어 아래 칸과 겹치던 것을 한 줄로 고정. WebGL Dev 빌드에서 확인. PROJECT.md 5.5·6.3·13.

- 2026-10-17 M3 피드백 반영(사용자 플레이): 발판 제거·그림자 강화, 배경 전체 다시 그림(하늘·구름·산 2겹·숲 2겹·풀밭, 디더링), 10만볼트를 몸에서 발사(`thunder_arc`), 하이드로펌프를 굵고 출렁이는 물줄기로 바꾸고 몸 표면에서 멈추게 함(`hydro_beam`, 이전에는 몸을 뚫은 것처럼 보였음), 후보 연출 7개를 고르는 연출 실험실(`?lab`, 타이틀에서 L), 승리 연출 강화(비가 그치고 하늘이 밝아짐), 원작 참고 HP UI(두 겹 테두리 상자, 그림자·받침선, "HP:" 표). WebGL Dev 빌드에서 화면·실험실·번개·물줄기·승리 연출을 확인했다(PROJECT.md 5.5). 프레임 성능은 여전히 못 쟀다.

- 2026-10-17 밸런스 조정(사용자 피드백): 피카츄가 10만볼트만 써야 HP 2쯤 남기고 겨우 이기고 한 번 빗나가면 진다 → 10만볼트 명중 95 → 100, 하이드로펌프 95 → 85. 시뮬레이션 승률 68% → 88.5%(평균 4.93턴), 이긴 판의 남은 HP 15% → 약 25%. Demo 목표를 85~93%로 바꾸고 `BalanceTests` 갱신(PROJECT.md 6.5).

- 2026-10-17 M3-5(2.5D 연출): 비 오는 저녁 배경(하늘·구름·언덕·나무·원근 바닥 5레이어 + 패럴랙스), 안개 2겹·비·바닥 빗방울 물결·번개 번쩍임·비네트, 포켓몬 그림자, 전역 Light2D + 타입 색 점 조명 `Pulse`(기 모으기·낙뢰·물대포·접촉·급소·승리), 월드 후처리 Bloom, 공격·승리 때 카메라 이동, 승리 연출(빛·반짝임·뛰기)을 구현했다(`BattleBackdrop`·`BattleLighting`·`BattlePostFx`, 상세는 PROJECT.md 5.5). 모든 그림은 코드로 그린 텍스처라 에셋 파일이 없다. URP 2D의 `Light2D`가 `Unity.RenderPipelines.Universal.2D.Runtime` 어셈블리에 따로 있어 Presentation asmdef에 참조를 추가했다. WebGL Dev 빌드를 브라우저에서 열어 확인했다(콘솔 오류 없음, 홀수 크기 창 정렬 정상, 낙뢰 볼트·피격 번쩍임·기절·패배 후 다시 도전까지 정상). 승리 연출과 급소 연출은 직접 보지 못했다. **프레임 성능은 못 쟀다**(브라우저 패널이 숨겨져 rAF가 제한됨) — 사용자 환경에서 확인 필요. 이전 포트 8011에 다른 세션의 서버가 떠 있어 한 번은 옛 빌드를 본 것을 발견했다(M3-4 첫 확인이 새 빌드였는지는 불확실, 이후 M3-5 빌드 확인으로 M3-4 코드도 함께 검증됨).

- 2026-10-17 M3-4(월드/HUD 렌더링 분리): 포켓몬 스프라이트·발판·배경·VFX를 월드(`BattleWorld`: `SpriteRenderer` + 정수 배율 `PixelCamera`)로, HP 바·메뉴·메시지를 Overlay 캔버스 HUD로 나눴다(`BattleSideView`·`SpriteAnimator`·`VfxLayer`·`AnimationDirector` 이동 코드 이관, 피격 번쩍임은 `Sprite Flash` 셰이더를 본체 위에 겹침). 화면은 이관 전과 같게 보이도록 했고 WebGL Dev 빌드를 브라우저에서 열어 타이틀 → 등장 → 기술 선택 → 접촉 공격·물대포 파티클·피격 번쩍임·행동 메뉴를 확인했다(콘솔 오류 없음). 홀수 크기 창(733×611)에서도 가운데 정렬·도트 선명도 정상 → **"화면 크기가 바뀐 뒤 위치·배율이 어긋나는 문제"는 ScreenSpaceCamera 캔버스를 없애면서 해결됐을 가능성이 높다**(이전 증상을 직접 재현해 비교하진 않았다). 낙뢰·급소·기절·승리 연출은 이번에 직접 보지 않았다. 변경: 새 파일 `Battle/World/*`, `UI/PixelAlign.cs`, `Shaders/SpriteFlash.shader`, 삭제 `UiSpriteAnimator`·`UIFlash`.

- 2026-10-17 사용자 결정: **월드/HUD 렌더링 분리(A)로 간다.** 2.5D(그림자·원근 바닥·배경 레이어·카메라·Light2D·후처리 안개)를 위해 포켓몬 스프라이트·배경·VFX는 월드(`SpriteRenderer`·카메라)로, HP 바·메뉴·메시지는 HUD(uGUI)로 나눈다. 동기는 "Unity를 폭넓게 경험"이며 용량·작업량보다 우선(PROJECT.md 1장·5.5). 단계: M3-4 분리(화면 동일 이관) → M3-5 2.5D. 이관 대상은 `BattleSideView`·`UiSpriteAnimator`·`UIFlash`·`VfxLayer`·`AnimationDirector` 이동 코드(약 670줄). `Pixel Perfect Camera`와 HUD의 후처리 분리 방식은 이관 때 검증.

- 2026-10-17 GPT 2.5D·아키텍처 리뷰 검토: 2.5D 요소(그림자·원근 바닥·배경 레이어·카메라·안개·셰이더·타입별 VFX)는 채택하되 도트 정수 배율 유지, 이벤트에 타입을 넣지 않는 점 등을 조정(PROJECT.md 5.5). 아키텍처 리뷰는 `AnimationDirector`가 M3-2에서 이미 분리된 점을 모르는 오래된 스냅샷 기준이었고, 배틀 캔버스는 `ScreenSpaceCamera`인데 Overlay로 오해했다. Presentation FSM·`activeIndex` 검사·`NextBattle`/`Retry` 가드는 보류(PROJECT.md 14장), Unity 자동 검증(CI·PlayMode)은 Actions 한도와 테스트 최소 원칙에 따라 채택하지 않음(7.8). JSON 콘텐츠 유지·DI 컨테이너 미도입·DDD 과적용 금지는 기존 방침과 같다(7.1).

- 2026-10-17 사용자 결정: 용량보다 문제 가능성이 커서 **스트리핑 High → Minimal**(`ProjectSetup`, `ProjectSettings.asset`), **웹 Release 빌드 Disk Size LTO → Runtime Speed(LTO 없음)**(`WebBuild`)로 되돌림. 아직 WebGL 빌드로 확인하지 않았다(빌드는 사용자 에디터에서). IL2CPP 크기 우선·미사용 패키지 제거·TMP 기본 리소스 삭제는 그대로 둔다.

- 2026-10-17 사용자 결정: **용량은 최하위로 고려한다**(8MB 목표는 근거가 약한 추정, PROJECT.md 5.4). 후처리용 `PostProcessData`를 `Renderer2D`에 다시 연결하고 `ProjectSetup`의 참조 제거 단계를 없앴다(URP 17.3 소스 확인: 2D 렌더러는 `postProcessData`가 비어 있으면 후처리 패스를 만들지 않는다). 아직 Unity 에디터에서 열어 확인하지는 않았다. M0에서 용량 때문에 한 다른 설정(스트리핑 High, IL2CPP 크기 우선, Disk Size LTO, 미사용 패키지 제거, TMP 기본 리소스 삭제)은 품질에 필요해질 때 되돌린다.

- 2026-10-17 M3-3(사운드): 속성 공격음 8개를 다시 만들어 사용자가 속성마다 2안 중 선택(전기 낙뢰·물대포·파이어볼+활활·덩굴 휘감기·칼바람·오물 폭탄·원혼의 울음·사이코 빔), 고르지 않은 8개는 두 번째 기술용으로 보관(`TypeAttackSounds.Alternates`). 등장음·기 모으기 소리 추가, 공격음을 연출 발사·착탄 시각에 맞춤(전기는 크랙이 0.176초 뒤라 그만큼 먼저). **배틀 BGM**(직접 작곡한 8비트 40초 루프, `BattleMusic`, 사용자 확인) — 판마다 처음부터, 승패 시 줄이고 징글, WebGL AAC 루프 이음새 대책(무음 2048 샘플 + WAV smpl). 아이폰 무음 모드에서도 소리가 나도록 WebGL 템플릿에 `navigator.audioSession.type = "playback"`. 사운드 출처 기록 `Assets/Content/Audio/manifest.md`. 들어 보기는 오디오 아티팩트(렌더 WAV)로 진행. **Dev 빌드에서 확인:** 기 모으기 → 0.325초 뒤 전기 공격음 → 0.51초 뒤 피격음, 물 공격음 → 0.51초 뒤 피격음(설계와 일치), BGM 노드 1개만 반복(브라우저 loopStart 0.0929초·loopEnd 40.0928초 = 정확히 40초, smpl 루프가 반영됨), 효과음은 반복 없이 끝남. 발견: 페이지 크기가 바뀐 뒤 전투 화면이 오른쪽 아래로 밀리고 배율이 정수가 아니게 그려지는 경우가 있음(새로고침하면 정상) → M3 마무리에서 수정.

- 2026-10-17 `Run In Background` 켬(`ProjectSetup`): 캔버스가 포커스를 잃어도(iframe 밖 클릭, 다른 창) 게임이 멈추지 않는다. 탭이 숨겨지면 브라우저가 프레임 루프(requestAnimationFrame)를 멈추므로 그때는 여전히 멈춘다(설정으로 바꿀 수 없음).

- 2026-10-17 테스트·CI 최소화(사용자 요청): 테스트를 꼭 필요한 것만 남김 445 → 97개(규칙·계약은 대표 사례만, 겹치는 검사·인자 null 검사·문구 고정·조사용 밸런스 표 제거, CLAUDE.md "지킬 것"). CI는 PR에서만·초안 제외·경로 필터 유지로 바꿔 실행 횟수를 줄임(Actions 무료 한도 초과가 계기).

- 2026-10-17 사용자 결정: **M3에서는 울음소리(등장 소리)를 넣지 않는다**(PROJECT.md 8.3, 이후 여부는 14장). M3-2(PR #6)는 GitHub Actions 결제 문제로 CI가 시작되지 않아, 로컬 테스트(dotnet 445 통과, Unity EditMode 445 통과·1 건너뜀) 확인 후 사용자 승인으로 병합.

- 2026-10-17 M3-2(연출): `AnimationDirector`(접촉/발사/효과 + 프로필·전용 이펙트), Showcase 이펙트(피카츄 낙뢰, 거북왕 물대포), `UIFlash` 피격 셰이더, `VfxLayer`(Particle System + 번개·구체·고리, 화면 번쩍임·흔들림), `animation.json`/`AnimationCatalog`(PROJECT.md 5.2). 소리는 WebGL 실행 중 합성이 화면을 멈춰 에디터에서 미리 굽도록 변경(`AudioBaker`). **사용자 정정: 속성 합성 소리는 울음소리가 아니라 공격음** → 등장·기절에서 빼고 이름을 `TypeAttackSounds`로, 연출 데이터의 `cry` 제거, 울음소리는 미정(PROJECT.md 8.3·14).

- 2026-10-16 M3-1(전투 흐름·화면): `BattleSession`(흐름·도망·다음 상대·다시 도전), `BattleMessages`/`BattleTextKeys`(이벤트 → 로케일 키, ko/en/ja), 배틀 화면(240×216, 행동·기술 2×2·결과 메뉴, 타자기 메시지, HP 바, BW 스프라이트 재생, 기본 연출), 속성 기반 사운드 합성기 C# 이식(`Presentation/Audio/Synth`, 시제품 크롬 렌더와 RMS ±0.6dB) + 배틀 효과음 연결. 스프라이트 생성 위치 `Assets/Resources/Sprites`로 변경. `pokepedia.dev`가 Vercel에서 관리됨을 기록(PROJECT.md 10.4). 남은 것: M3-2 연출(AnimationDirector·Showcase 이펙트·울음소리 프로필), M3-3 사운드 다듬기.

- 2026-10-16 M2 완료: 순수 C# 배틀 엔진(TDD, 테스트 15 → 329개 + 수동 실행 조정용 표 1개, Unity·dotnet 양쪽 통과). 18타입 상성표(PokeAPI·Pokémon DB 324칸 대조), 능력치·데미지 계산(Showdown 소스로 단계별 정수 처리 검증), 턴 처리 엔진(난수 소비 순서 고정), NPC AI, 시드 고정 시뮬레이터, 콘텐츠 JSON(로스터 6종·기술 9개, PokeAPI 확인)·로더·이름 로케일. 밸런스: k 0.65, 대표 기술 위력 35 → 피카츄 vs 거북왕 승률 68.5%·평균 4.92턴(PROJECT.md 6.5). Unity는 asmdef마다 `IsExternalInit`이 필요해 `AssemblyLayoutTests`로 검사(dotnet CI가 못 잡는 문제). 병렬 작업은 서브에이전트 3명이 worktree에서 상성·계산·콘텐츠를 나눠 맡음.

- 2026-10-16 문서 구조 정리: `design.md` → `PROJECT.md`, `bridge-protocol.md`를 PROJECT.md 9.2로 통합, `M0-poc-result.md` → 이 문서, 에이전트 파일은 `CLAUDE.md` 하나로 일원화. PROJECT.md에서 끝난 PoC 목록·단계별 서술(이 문서와 중복)·옛 디렉터리 예시·시뮬레이션 메모·인수인계 문구를 제거하고 M0 확정 사항(폰트, 울음소리, GIF 변환)을 반영. CI가 도는 PR은 통과하면 병합하는 것이 기본(GitHub auto-merge는 이 저장소에서 사용 불가, CLAUDE.md).
- 2026-10-16 Unity CLI 연동 조사: 에디터 내장 MCP 서버는 deprecated, Unity CLI가 대체. 검증 결과는 PROJECT.md 15장. `unity pipeline install`은 아직 하지 않음. (PR #1)
- 2026-10-16 CI 경로 필터 적용: 도메인 테스트가 도는 파일이 바뀔 때만 `domain-tests` 실행.
- 2026-10-16 M1 완료: asmdef 계층 분리, 테스트(Unity + dotnet), 웹 빌드 설정(Brotli, 스트리핑 High, Data Caching 끔, 스플래시 로고 제거), `PixelCanvasScaler`, 진입 화면·브리지. M0 → M1 이관 항목 중 Cloudflare 실배포 헤더 검증만 M4로 남음.
- 2026-10-16 M0 완료: PoC 9가지 모두 결론. 아래 "M0 — PoC 결과".

## M0 — PoC 결과

**측정일: 2026-10-16 · Unity 6000.3.26f1 (6.3 LTS) · Universal 2D 템플릿 · PoC 프로젝트 `pokepedia-unity-poc`**

> 9개 검증 항목의 결론(가능 / 불가 / 우회책)만 정리한다. 수치는 이 PC(Windows 11, Chrome 152 계열 내장 브라우저)에서 잰 값이며 모바일 실기기 수치는 아니다.

### 요약

| # | 항목 | 결론 |
|---|---|---|
| 1 | 한·영·일 폰트 | **가능.** Galmuri11 + TMP Static 아틀라스(512×512 Alpha8)에 필요한 419자 전부 수록, 원본 TTF는 빌드에서 제외됨 |
| 2 | WebGL 빌드 용량·로딩 | **가능.** Brotli 9.4MB, 첫 로딩 로컬 1.9초 / 9Mbps 6.5초, 재방문 약 2초. URP 다이어트로 약 1.5MB 더 줄일 수 있음 |
| 3 | iframe + postMessage 왕복 | **가능.** 다른 origin(8000↔8001)에서 `ready` → `init` 왕복, 5초 타임아웃 → [RETRY] → 재연결 확인 |
| 4 | iframe 안 키 입력·스크롤 간섭 | **가능.** 방향키·Space·Z·X 인식, 부모 페이지 스크롤 0 유지 |
| 5 | 호스팅 헤더 | **로컬 모의 서버로 가능 확인.** Cloudflare 실배포는 미검증(계정 필요) |
| 6 | 공격 연출 | **가능.** Animator 클립(돌진) + AnimationEvent → Particle + 효과음 + 피격 흔들림 |
| 7 | BW GIF 변환·재생 | **가능.** GIF → 시트 PNG + 지연 JSON → 런타임 `SpriteFramePlayer` 재생 |
| 8 | 모바일 브라우저 레이아웃 | **기본 가능.** 세로·가로 모두 10:9 유지, 탭 입력 동작. 실기기·실제 터치 이벤트는 미검증 |
| 9 | 오디오 | **가능.** START 클릭 후 효과음 재생. PokeAPI 원본 울음소리는 쓰지 않음. 속성 기반 합성 소리를 만듦 — **이것은 공격음이다**(2026-10-17 정정, 울음소리는 M3에서 넣지 않음) |

### 1. 폰트

- **Galmuri11 채택 권장.** 한글·히라가나·가타카나·JIS 한자를 모두 가져 보조 폰트가 필요 없다. 11 Bold는 가나·한자·♀♂가 없고, 11 Condensed는 한자가 없다.
- TMP Static 폰트 에셋: 12pt 샘플링, `RASTER_HINTED`, 512×512 Alpha8, 419자 전부 추가(누락 0). 에셋 크기(비압축) 295KB. 소스 TTF 참조를 끊어 5.4MB TTF가 빌드에 들어가지 않음을 빌드 리포트로 확인.
- 라이선스 OFL-1.1(Reserved Font Name 없음). 배포 시 저작권 고지 + 라이선스 동봉 필요.
- 주의
  - CanvasScaler가 정수 배율이 아니면 도트가 고르지 않다 → M3에서 **정수 배율(pixel-perfect) 스케일링** 필요.
  - TMP Essentials를 넣으면 `Resources/`의 LiberationSans SDF(1MB)·TTF(344KB)·EmojiOne(341KB)이 빌드에 포함된다 → M1에서 TMP Settings 기본 폰트를 Galmuri로 바꾸고 제거.
- 도구: `Tools/fonts/extract_glyphs.py`(locale JSON → 글리프 목록), `check_coverage.py`, `Assets/Editor/PocFontBuild.cs`(Static 아틀라스 생성).

### 2. WebGL 빌드 용량·로딩

| 빌드 | 설정 | wasm | data | 합계(전송) |
|---|---|---|---|---|
| 기준 Gzip | 기본 | 12.1MB | 4.6MB | 16.8MB |
| 기준 Brotli | 기본 | 8.6MB | 3.9MB | 12.6MB |
| Opt1 | + 스트리핑 High, IL2CPP 크기 우선, Disk Size LTO | 5.7MB | 3.3MB | 9.1MB |
| Opt2 | + 미사용 패키지 제거 | 5.4MB | 3.2MB | 8.7MB |
| Opt3 | Opt2 + 폰트·스프라이트 2마리·TMP | 5.5MB | 3.8MB | 9.4MB |

| 로딩(시작 가능 시점까지) | 로컬 | 9Mbps + 60ms(Fast 4G 근사) |
|---|---|---|
| 기준 Brotli, 첫 방문 | 2.1초 | 9.6초 |
| Opt3, 첫 방문 | 1.9초 | 6.5초 |
| Opt3, 재방문(캐시) | 2.3초 | 2.1초 |

- 재방문은 다운로드가 0이고(wasm은 HTTP 캐시, data는 IndexedDB 캐시) 남은 2초는 wasm 컴파일·엔진 초기화 시간이다. 중급 휴대폰에서는 더 길 것으로 예상(미측정).
- 문서 목표(데스크톱 5초 이하, Fast 4G 10초 이하, 재방문 3초 이하)는 이 PC 기준 모두 충족. 측정은 `Tools/web/serve.py --mbps 9 --latency-ms 60`(서버 측 속도 제한)으로 했다.
- **Data Caching 주의:** 같은 origin을 두 탭에서 동시에 로딩했을 때 IndexedDB에 캐시된 data가 깨져 `The file 'level0' is corrupted!`가 1회 발생했다. 깨끗한 순차 재방문에서는 재현되지 않았다. Safari는 iframe 안에서 IndexedDB를 지원하지 않기도 하므로, **M1에서는 Data Caching을 끄고 HTTP 캐시(해시 파일명 + `immutable`)에만 의존하는 것을 권장**한다.

- 로딩 시간은 거의 **wasm 다운로드 시간**이 결정한다(9Mbps에서 wasm 완료 6.2초 → 시작 6.5초). 전송량을 줄이면 바로 빨라진다.
- data의 남은 큰 항목: 스플래시 로고 텍스처 2.7MB(스플래시를 꺼도 포함됨), URP FilmGrain 텍스처 약 2.8MB, TMP 기본 리소스 약 1.7MB(모두 비압축 기준). → 렌더 파이프라인 비교 결과는 아래 "URP vs Built-in" 참고.
- 처음 잰 8.4초는 다른 Unity 빌드가 CPU를 쓰던 중의 값이라 폐기.
- 빌드 시간: 첫 WebGL 빌드 약 14.5분, LTO 빌드 약 7~10분, 캐시가 있는 비LTO 재빌드 약 1분.
- Unity 6.3: Personal에서도 스플래시를 끌 수 있다(`SplashScreen.show=false`). 이때 로딩 후 바로 게임이 보인다.

### 3. iframe + postMessage

- 구조: C# `HostBridge` → `.jslib`(`window.parent.postMessage`) / 템플릿의 `message` 리스너가 `event.origin`을 허용 목록과 대조한 뒤 `unityInstance.SendMessage("HostBridge", "OnHostInit", json)`.
- `?host=pokepedia` 쿼리는 jslib로 `location.search`를 읽어 판별(iframe에서 `Application.absoluteURL`의 의미가 불명확하므로 쓰지 않음).
- 확인: `ready` → `init`(보유 목록 4마리) 수신, init 미전송 시 5초 후 [RETRY] 표시(자동 Demo 전환 없음), RETRY → `ready` 재전송 → `init` 수신.
- 남은 일: 정식 빌드에서 jslib의 `targetOrigin`을 `'*'`에서 허용 origin으로 제한.

### 4. 키 입력

- 템플릿에서 방향키·Space의 `keydown` 기본 동작을 막고, 포인터/터치 시 캔버스에 포커스.
- 부모 페이지가 스크롤 가능한 상태에서 iframe 포커스 후 방향키·Space를 눌러도 `scrollY = 0` 유지. Z/X는 Input System `Keyboard.current`로 인식.

### 5. 호스팅

- 로컬 모의 서버(`Tools/web/serve.py`)로 `.br` 사전 압축 파일 + `Content-Encoding: br` + `application/wasm` + `frame-ancestors` 조합이 정상 동작함을 확인(Chrome은 `http://localhost`에서도 br을 받음).
- Cloudflare Workers 정적 자산에서 `_headers`로 `.br` 파일의 `Content-Encoding`을 지정할 수 있는지는 **문서에 명시가 없어 미검증**. 실배포로 확인 필요(계정 필요). 안 되면 `run_worker_first` Worker에서 헤더를 붙인다.
- Safari는 iframe 안에서 IndexedDB(Data Caching)를 지원하지 않는다(Unity 6.3 문서) → 재방문 캐시는 HTTP 캐시(해시 파일명 + `immutable`)에 의존.

### 6. 공격 연출

- Animator(Idle 루프 + Attack 트리거) 클립 안의 AnimationEvent가 `FireSpark` 호출 → ParticleSystem 버스트 + 절차 생성 효과음 + 대상 흔들림·깜빡임(코드 트위닝).
- 규칙(엔진)과 무관한 순수 연출 경로로 동작함을 확인. Showcase 품질의 이펙트 제작은 M3.

### 7. 스프라이트

- PokeAPI BW 애니메이션 GIF 12개(6마리 × 앞/뒤) 변환 성공. 알파는 0/255뿐이라 Point 필터 + 비압축으로 깨끗하다.
- 시트 PNG 합계: 기본 856KB → `--dedupe --indexed` 245KB.
- **재생은 코드 프레임 플레이어 권장**(`SpriteFramePlayer`: JSON을 읽어 `Sprite.Create`로 런타임 분할, 프레임별 지연 반영). 프레임 지연이 50~400ms로 불균일하고 최대 145프레임이라 Animator 클립을 포켓몬마다 만드는 것은 비효율적이다. 피격·기절 같은 상태 전환에만 Animator를 쓴다.
- 발 위치 pivot (0.5, 0), PPU 16(게임보이 160×144 기준 카메라)에서 피카츄·거북왕이 투명도 문제 없이 표시됨.
- 도구: `Tools/sprites/fetch_sprites.py`, `convert_gif.py`.

### 8. 모바일 레이아웃

- 세로(375×812): 10:9 화면이 폭에 맞고 위아래가 비어 있다 → **M3에서 세로 모드 하단 여백에 터치 버튼(방향·A·B)을 배치**하는 레이아웃 권장.
- 가로(812×375): 높이에 맞춰 10:9 유지.
- 확인 못 한 것: 실기기, 실제 터치 이벤트(테스트 브라우저는 클릭으로 전달), safe-area 값, iOS Safari 메모리.

### 9. 오디오

- START 버튼 클릭 안에서 첫 효과음 → "Audio context resumed" 로그 확인. 이후 Z 키 효과음·공격 효과음 정상.
- PokeAPI 원본 울음소리(latest/legacy)는 6마리 모두 어색해 **사용하지 않는다.** 대신 **속성 기반 합성 소리**를 만들었다. **2026-10-17 정정: 이 소리는 울음소리가 아니라 기술을 쓸 때 나는 공격음이다.** 울음소리(등장 소리)는 M3에서 넣지 않는다(PROJECT.md 8.3·14).
  - 시제품(기록): [`Tools/audio/type-attack-sounds-prototype.html`](../Tools/audio/type-attack-sounds-prototype.html) — 브라우저에서 열고 버튼으로 재생(Web Audio 즉석 합성). 페이지 안 "울음소리"·"pitch/len" 표기는 당시 이름이다.
  - 규칙: 주 속성 레시피가 소리의 성격을 정하고, 보조 속성은 세기 0.45로 덧칠한다. 종족별 음높이(반음)·길이 배수는 데이터로 둔다. 시드 고정이라 매번 같은 소리가 난다.
  - 레시피(v3, 사용자 청취 후 OK): 전기 = 충전 → 낙뢰 크랙 → 전류 아크 → 천둥 꼬리(라이트닝 스킬), 물 = 물살 + 거품 + 마법 반짝임, 불꽃 = 화악 발사 + 화염 + 착탄 폭발(파이어볼), 풀 = 바람 + 잎사귀 + 덩굴 채찍, 비행 = 날갯짓 + 돌풍(휘파람 제거), 독·고스트·에스퍼 = 음 위주 합성(v1 유지).
  - 청취 이력: v1(음 위주)은 물·불·풀이 속성으로 들리지 않음 → v2에서 노이즈 가공으로 전환 → v3에서 전기를 라이트닝 스킬형으로, 비행을 휘파람에서 날갯짓·돌풍으로 교체.
  - 장점: 원본 음원의 권리 문제가 없고 용량이 거의 0이다. 로스터를 늘려도 속성 레시피만 재사용한다.
  - Unity 이식(M3): 같은 레시피를 `AudioClip.Create`로 절차 생성하거나 오프라인 렌더로 WAV를 구워 쓴다(WebGL은 `AudioClip.Create`에 `stream=false`만 지원).

### URP vs Built-in

**결론: URP(2D Renderer) 유지 + 다이어트 설정.** 프로젝트 사본 두 개(`pokepedia-unity-poc-rp`, `pokepedia-unity-poc-builtin`)로 비교했다. Opt2 기준이며 폰트·스프라이트는 없다.

| 변형 | wasm | data | 합계 |
|---|---|---|---|
| Opt2 (URP 기본) | 5,447KB | 3,196KB | 8,741KB |
| A: URP + 스플래시 로고 제거 + PostProcessData 제거 | 5,530KB | 1,653KB | 7,281KB |
| B: Built-in RP | 4,805KB | 1,225KB | 6,127KB |

- 스플래시 로고 텍스처(2.7MB)는 `SplashScreen.show=false`만으로는 빠지지 않고 `showUnityLogo=false`까지 꺼야 빠진다(Unity Issue Tracker UUM-63958, Won't Fix). A에서 실제로 빠진 것을 확인했다. B에는 이 설정을 넣지 않아 로고가 남아 있으므로 B의 실제 최소치는 표보다 더 작다.
- PostProcessData를 null로 두면 FilmGrain·SMAA 텍스처와 후처리 셰이더가 빠진다. 후처리가 필요해지면 다시 연결하면 된다.
- Built-in이 약 1.2MB 더 작지만, Unity가 Built-in을 6.5부터 deprecated로 지정했고 신규 프로젝트에는 권장하지 않는다(Unity "Render Pipelines strategy for 2026"). 또 2D Light, Sprite-Lit, Volume 후처리, URP용 2D Shader Graph를 잃는다. 1.2MB 차이는 이 비용을 감수할 만큼 크지 않다.
- 두 변형 모두 브라우저에서 START·오디오·공격 연출까지 정상 동작했다. URP 빌드 콘솔에는 `Hidden/CoreSRP/CoreCopy`, `Hidden/Universal/HDRDebugView` 셰이더 미지원 로그가 찍히지만 화면에는 영향이 없다.

### M1로 넘길 결정·작업

- 폰트 Galmuri11 확정, TMP 기본 리소스 정리, 정수 배율 스케일링.
- 렌더 파이프라인: URP 2D 유지, `showUnityLogo=false` → `show=false`, PostProcessData 제거.
- 빌드 설정: Brotli, 스트리핑 High, Disk Size LTO, 미사용 패키지 제거(`Tools/poc/manifest.lean.json`), Data Caching 끔.
- 스프라이트: `--dedupe --indexed` 시트 + `SpriteFramePlayer`.
- Cloudflare 실배포 헤더 검증.
