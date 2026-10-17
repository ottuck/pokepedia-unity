# 사운드 매니페스트 (PROJECT.md 8.3)

게임에 나오는 모든 소리의 출처·라이선스 기록. **현재 모든 소리는 이 저장소의 코드로 직접 합성한다.**
외부 음원·AI 생성음·원작 음원은 쓰지 않는다. 그래서 라이선스는 이 저장소의 코드와 같다.

- 합성 코드: `Assets/Scripts/Presentation/Audio/Synth/` (`TypeAttackSounds`, `BattleSfx`)
- 굽기: 에디터 메뉴 **Pokepedia → Bake Audio**(Setup Project 에 포함)가 `Assets/Resources/Audio/{키}.wav` 로 굽는다(커밋 안 함, 22050Hz mono).
- 들어 보기·측정: `dotnet run -c Release --project Tools/audio/render` → `Tools/audio/out/*.wav` + 길이·피크·RMS 표
- 속성 공격음 첫 시제품(지금 레시피와 다름): `Tools/audio/type-attack-sounds-prototype.html`

| 키 | 언제 | 합성 | 비고 |
|---|---|---|---|
| `move_electric` | 전기 기술 발사 | 낙뢰: 충전 → 콰직 크랙 → 찌지직·치지직 고역 아크 → 치리리릭 → 천둥 | 크랙이 소리 시작 0.176초 뒤 → 발사보다 그만큼 먼저 튼다 |
| `move_water` | 물 기술 발사 | 물대포: 솟구침 → 콸콸 → 촤악 → 물보라 | 착탄 0.5초(물줄기에 맞춤) |
| `move_fire` | 불꽃 기술 발사 | 파이어볼 + 활활: 화악 → 불덩이 → 펑 → 일렁이며 타오름 | 착탄 0.35초 |
| `move_flying` | 비행 기술 발사 | 칼바람: 쉭-쉭 공기 베기 → 휘이잉 회오리 | |
| `move_grass` | 풀 기술 발사 | 덩굴 휘감기: 잎사귀 → 휙휙휙 감기며 조여드는 삐걱 → 착 → 끼이익 | 착탄 0.35초 |
| `move_poison` | 독 기술 발사 | 오물 폭탄: 뿌직 → 꿀렁 → 철퍽 → 치이익 산성 거품 | 착탄 0.35초 |
| `move_ghost` | 고스트 기술 발사 | 원혼의 울음: 유령 목소리 + 속삭임 → 어긋난 종 메아리 | 착탄 0.5초 |
| `move_psychic` | 에스퍼 기술 발사 | 사이코 빔: 링 변조 광선 + 피융 → 착탄 + 메아리 | 착탄 0.5초 |
| `movesfx_tackle` | 몸통박치기(animation.json `moveSfx`) | 슈욱 돌진(위로 쓸리는 밴드패스) → 딸깍 + 퍽 + 쿵(낮게 떨어지는 사인) → 흙먼지 쓸림·작은 파편 | 충돌이 소리 시작 0.09초 뒤 = 돌진 시간 |
| `movesfx_water_geyser` | 물의맹세(`moveSfx`) | 물기둥마다 "촤아": 45ms 에 걸쳐 곡선으로 차오르는 출렁이는 물살(대역이 위로) + 물보라 쏴 + 꿀렁 + 거품 공명 구름(짧게 위로 미끄러지는 사인), 다음 물기둥 전에 지수로 잦아듦. 작은 물기둥 4번 점점 세게 → 큰 물기둥 한 번 크고 길게. 10ms 안팎에 터졌다 줄어드는 노이즈·쿵 저음은 총소리처럼 들려 쓰지 않는다 | 첫 물기둥에 맞춰 시작, 0.12초 간격(연출과 같음) |
| `sfx_sendout` | 포켓몬 등장 | 노이즈 "휙" + 펄스 "뿅"(번쩍임에 맞춤) | 울음소리 아님(울음소리는 넣지 않음, PROJECT.md 14) |
| `sfx_charge` | 기 모으기(준비 0.25초 이상) | 오르는 펄스 + 떨림 | 공격음이 시작될 때 끊는다 |
| `sfx_hit` | 피격 | LFSR 노이즈 + 낙하 사각파 | |
| `sfx_criticalhit` | 급소 피격 | 크랙 + 깊은 낙하 + 두 번째 타격 | |
| `sfx_miss` | 빗나감 | 밴드패스 노이즈 "휙" | |
| `sfx_hptick` | HP 바 감소 | 짧은 펄스 | |
| `sfx_faint` | 기절 | 하강 펄스 + 떨림 | |
| `sfx_victory` | 승리 | C5-E5-G5 → C6 | |
| `sfx_defeat` | 패배 | G4-F#4-F4 → E4 | |
| `sfx_cursormove` | 메뉴 커서 이동 | 짧은 펄스 | |
| `sfx_confirm` | 결정 | 2음 상승 | |
| `sfx_cancel` | 취소 | 2음 하강 | |
| `bgm_battle` | 배틀 중 반복 | `BattleMusic`(직접 작곡한 8비트 4채널: 펄스 2 + 삼각파 + 노이즈) | 원작 곡을 쓰거나 베끼지 않은 창작곡. 앞 2048 샘플 무음 + WAV smpl 루프 구간(WebGL AAC 루프 이음새 대책) |

## 보관한 공격음

속성마다 두 안 중 고르지 않은 쪽. 코드로 보관한다(`TypeAttackSounds.Alternates.cs`, `RenderAlternate(id)`). `animation.json`의 `moveSfx`에 적은 것만 굽는다(지금은 없음).

| id | 소리 |
|---|---|
| `water_magic` | 물 마법: 거품이 차오름 → 물줄기 → 첨벙 + 거품 폭발 → 물방울 |
| `fire_flamethrower` | 화염 방사: 점화 → 거칠게 타오르는 굉음 → 콰앙 → 계속 활활 |
| `grass_root_burst` | 뿌리 솟구침: 땅이 우지끈 → 굵은 뿌리 끄으윽 → 휘감아 → 꽉 조이는 끼익 |
| `electric_discharge` | 방전: 고압 험 → 파지직 혼돈의 아크 + 지직 3줄기 → 탁 → 오존 쉬이 |
| `flying_wing_strike` | 날개 강타: 묵직한 펄럭 → 휘익 내려침 → 소용돌이 돌풍 |
| `poison_toxic_bog` | 독 늪: 부글부글 거품 + 메스꺼운 어긋난 음 → 꿀렁 펑 |
| `ghost_shadow_ball` | 섀도볼: 차오르는 어둠 + 섬뜩한 음 → 웅! → 메아리 |
| `psychic_telekinesis` | 염동력: 빨라지는 맥동 + 공명 스윕 → 일그러지며 휘는 세 음 + 링 변조 + 바람 |

외부 소스를 쓰게 되면 이 표에 출처 URL·라이선스·수집일(AI 생성이면 도구와 요금제)을 함께 적는다.
