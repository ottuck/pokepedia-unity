# 사운드 매니페스트 (PROJECT.md 8.3)

게임에 나오는 모든 소리의 출처·라이선스 기록. **현재 모든 소리는 이 저장소의 코드로 직접 합성한다.**
외부 음원·AI 생성음·원작 음원은 쓰지 않는다. 그래서 라이선스는 이 저장소의 코드와 같다.

- 합성 코드: `Assets/Scripts/Presentation/Audio/Synth/` (`TypeAttackSounds`, `BattleSfx`)
- 굽기: 에디터 메뉴 **Pokepedia → Bake Audio**(Setup Project 에 포함)가 `Assets/Resources/Audio/{키}.wav` 로 굽는다(커밋 안 함, 22050Hz mono).
- 들어 보기·측정: `dotnet run -c Release --project Tools/audio/render` → `Tools/audio/out/*.wav` + 길이·피크·RMS 표
- 속성 공격음 시제품(기록): `Tools/audio/type-attack-sounds-prototype.html`

| 키 | 언제 | 합성 | 비고 |
|---|---|---|---|
| `move_electric` | 전기 기술 발사 | `TypeAttackSounds` 전기(충전 → 낙뢰 크랙 → 아크 → 천둥) | 크랙이 소리 시작 0.176초 뒤 → 발사보다 그만큼 먼저 튼다 |
| `move_water` | 물 기술 발사 | 물살 + 거품 + 첨벙 + 마법 반짝임 | |
| `move_fire` | 불꽃 기술 발사 | 파이어볼(화악 → 화염 → 착탄) | |
| `move_flying` | 비행 기술 발사 | 날갯짓 3번 + 돌풍 | |
| `move_grass` | 풀 기술 발사 | 바람 + 잎사귀 + 덩굴 채찍 + 나무 울림 | |
| `move_poison` | 독 기술 발사 | 트레몰로 사각파 | |
| `move_ghost` | 고스트 기술 발사 | 맥놀이 사인 + 떨림 | |
| `move_psychic` | 에스퍼 기술 발사 | FM 금속성 톤 + 메아리 | |
| `sfx_sendout` | 포켓몬 등장 | 노이즈 "휙" + 펄스 "뿅"(번쩍임에 맞춤) | 울음소리 아님. 울음소리는 M3에서 넣지 않음 |
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

외부 소스를 쓰게 되면 이 표에 출처 URL·라이선스·수집일(AI 생성이면 도구와 요금제)을 함께 적는다.
