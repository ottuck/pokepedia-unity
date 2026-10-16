# M0 PoC 결과

**측정일: 2026-10-16 · Unity 6000.3.26f1 (6.3 LTS) · Universal 2D 템플릿 · PoC 프로젝트 `pokepedia-unity-poc`**

> 9개 검증 항목의 결론(가능 / 불가 / 우회책)만 정리한다. 수치는 이 PC(Windows 11, Chrome 152 계열 내장 브라우저)에서 잰 값이며 모바일 실기기 수치는 아니다.

## 요약

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
| 9 | 오디오 | **가능.** START 클릭 후 효과음 재생. 울음소리는 원본 대신 **속성 기반 합성으로 확정**(세부는 M3) |

## 1. 폰트

- **Galmuri11 채택 권장.** 한글·히라가나·가타카나·JIS 한자를 모두 가져 보조 폰트가 필요 없다. 11 Bold는 가나·한자·♀♂가 없고, 11 Condensed는 한자가 없다.
- TMP Static 폰트 에셋: 12pt 샘플링, `RASTER_HINTED`, 512×512 Alpha8, 419자 전부 추가(누락 0). 에셋 크기(비압축) 295KB. 소스 TTF 참조를 끊어 5.4MB TTF가 빌드에 들어가지 않음을 빌드 리포트로 확인.
- 라이선스 OFL-1.1(Reserved Font Name 없음). 배포 시 저작권 고지 + 라이선스 동봉 필요.
- 주의
  - CanvasScaler가 정수 배율이 아니면 도트가 고르지 않다 → M3에서 **정수 배율(pixel-perfect) 스케일링** 필요.
  - TMP Essentials를 넣으면 `Resources/`의 LiberationSans SDF(1MB)·TTF(344KB)·EmojiOne(341KB)이 빌드에 포함된다 → M1에서 TMP Settings 기본 폰트를 Galmuri로 바꾸고 제거.
- 도구: `Tools/fonts/extract_glyphs.py`(locale JSON → 글리프 목록), `check_coverage.py`, `Assets/Editor/PocFontBuild.cs`(Static 아틀라스 생성).

## 2. WebGL 빌드 용량·로딩

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

## 3. iframe + postMessage

- 구조: C# `HostBridge` → `.jslib`(`window.parent.postMessage`) / 템플릿의 `message` 리스너가 `event.origin`을 허용 목록과 대조한 뒤 `unityInstance.SendMessage("HostBridge", "OnHostInit", json)`.
- `?host=pokepedia` 쿼리는 jslib로 `location.search`를 읽어 판별(iframe에서 `Application.absoluteURL`의 의미가 불명확하므로 쓰지 않음).
- 확인: `ready` → `init`(보유 목록 4마리) 수신, init 미전송 시 5초 후 [RETRY] 표시(자동 Demo 전환 없음), RETRY → `ready` 재전송 → `init` 수신.
- 남은 일: 정식 빌드에서 jslib의 `targetOrigin`을 `'*'`에서 허용 origin으로 제한.

## 4. 키 입력

- 템플릿에서 방향키·Space의 `keydown` 기본 동작을 막고, 포인터/터치 시 캔버스에 포커스.
- 부모 페이지가 스크롤 가능한 상태에서 iframe 포커스 후 방향키·Space를 눌러도 `scrollY = 0` 유지. Z/X는 Input System `Keyboard.current`로 인식.

## 5. 호스팅

- 로컬 모의 서버(`Tools/web/serve.py`)로 `.br` 사전 압축 파일 + `Content-Encoding: br` + `application/wasm` + `frame-ancestors` 조합이 정상 동작함을 확인(Chrome은 `http://localhost`에서도 br을 받음).
- Cloudflare Workers 정적 자산에서 `_headers`로 `.br` 파일의 `Content-Encoding`을 지정할 수 있는지는 **문서에 명시가 없어 미검증**. 실배포로 확인 필요(계정 필요). 안 되면 `run_worker_first` Worker에서 헤더를 붙인다.
- Safari는 iframe 안에서 IndexedDB(Data Caching)를 지원하지 않는다(Unity 6.3 문서) → 재방문 캐시는 HTTP 캐시(해시 파일명 + `immutable`)에 의존.

## 6. 공격 연출

- Animator(Idle 루프 + Attack 트리거) 클립 안의 AnimationEvent가 `FireSpark` 호출 → ParticleSystem 버스트 + 절차 생성 효과음 + 대상 흔들림·깜빡임(코드 트위닝).
- 규칙(엔진)과 무관한 순수 연출 경로로 동작함을 확인. Showcase 품질의 이펙트 제작은 M3.

## 7. 스프라이트

- PokeAPI BW 애니메이션 GIF 12개(6마리 × 앞/뒤) 변환 성공. 알파는 0/255뿐이라 Point 필터 + 비압축으로 깨끗하다.
- 시트 PNG 합계: 기본 856KB → `--dedupe --indexed` 245KB.
- **재생은 코드 프레임 플레이어 권장**(`SpriteFramePlayer`: JSON을 읽어 `Sprite.Create`로 런타임 분할, 프레임별 지연 반영). 프레임 지연이 50~400ms로 불균일하고 최대 145프레임이라 Animator 클립을 포켓몬마다 만드는 것은 비효율적이다. 피격·기절 같은 상태 전환에만 Animator를 쓴다.
- 발 위치 pivot (0.5, 0), PPU 16(게임보이 160×144 기준 카메라)에서 피카츄·거북왕이 투명도 문제 없이 표시됨.
- 도구: `Tools/sprites/fetch_sprites.py`, `convert_gif.py`.

## 8. 모바일 레이아웃

- 세로(375×812): 10:9 화면이 폭에 맞고 위아래가 비어 있다 → **M3에서 세로 모드 하단 여백에 터치 버튼(방향·A·B)을 배치**하는 레이아웃 권장.
- 가로(812×375): 높이에 맞춰 10:9 유지.
- 확인 못 한 것: 실기기, 실제 터치 이벤트(테스트 브라우저는 클릭으로 전달), safe-area 값, iOS Safari 메모리.

## 9. 오디오

- START 버튼 클릭 안에서 첫 효과음 → "Audio context resumed" 로그 확인. 이후 Z 키 효과음·공격 효과음 정상.
- 울음소리 **[확정]**: PokeAPI 원본(latest/legacy)은 6마리 모두 어색해 **사용하지 않는다.** 대신 **속성 기반 합성**으로 만든다. 세부 음색은 M3에서 다듬는다.
  - 시제품(기록): [`Tools/audio/type-cries-prototype.html`](../Tools/audio/type-cries-prototype.html) — 브라우저에서 열고 버튼으로 재생(Web Audio 즉석 합성).
  - 규칙: 주 속성 레시피가 소리의 성격을 정하고, 보조 속성은 세기 0.45로 덧칠한다. 종족별 음높이(반음)·길이 배수는 데이터로 둔다. 시드 고정이라 매번 같은 소리가 난다.
  - 레시피(v3, 사용자 청취 후 OK): 전기 = 충전 → 낙뢰 크랙 → 전류 아크 → 천둥 꼬리(라이트닝 스킬), 물 = 물살 + 거품 + 마법 반짝임, 불꽃 = 화악 발사 + 화염 + 착탄 폭발(파이어볼), 풀 = 바람 + 잎사귀 + 덩굴 채찍, 비행 = 날갯짓 + 돌풍(휘파람 제거), 독·고스트·에스퍼 = 음 위주 합성(v1 유지).
  - 청취 이력: v1(음 위주)은 물·불·풀이 속성으로 들리지 않음 → v2에서 노이즈 가공으로 전환 → v3에서 전기를 라이트닝 스킬형으로, 비행을 휘파람에서 날갯짓·돌풍으로 교체.
  - 장점: 원본 음원의 권리 문제가 없고 용량이 거의 0이다. 로스터를 늘려도 속성 레시피만 재사용한다.
  - Unity 이식(M3): 같은 레시피를 `AudioClip.Create`로 절차 생성하거나 오프라인 렌더로 WAV를 구워 쓴다(WebGL은 `AudioClip.Create`에 `stream=false`만 지원).

## URP vs Built-in

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

## M1로 넘길 결정·작업

- 폰트 Galmuri11 확정, TMP 기본 리소스 정리, 정수 배율 스케일링.
- 렌더 파이프라인: URP 2D 유지, `showUnityLogo=false` → `show=false`, PostProcessData 제거.
- 빌드 설정: Brotli, 스트리핑 High, Disk Size LTO, 미사용 패키지 제거(`Tools/poc/manifest.lean.json`), Data Caching 끔.
- 스프라이트: `--dedupe --indexed` 시트 + `SpriteFramePlayer`.
- Cloudflare 실배포 헤더 검증.
