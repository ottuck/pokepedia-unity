# 스프라이트 도구 (Tools/sprites)

PokeAPI/sprites 저장소의 5세대(Black/White) 애니메이션 GIF를 받아 Unity용 스프라이트 시트(PNG)와 프레임 메타(JSON)로 변환합니다.

저장소에는 스프라이트 이미지를 커밋하지 않습니다(docs/PROJECT.md 8.1). 커밋하는 것은 이 폴더의 스크립트와, 원본 저장소 커밋 SHA로 고정한 파일 목록 `manifest.json`뿐입니다. clone 후 아래 명령을 한 번 실행하면 같은 결과가 재현됩니다.

## 최초 설정 및 빌드

저장소 루트에서 실행합니다(Windows, Python 3.10 이상이며 3.14.7에서 확인함).

```sh
py -3 -m venv Tools/sprites/.venv
Tools/sprites/.venv/Scripts/python -I -m pip install -r Tools/sprites/requirements.txt
Tools/sprites/.venv/Scripts/python -I Tools/sprites/build_sprites.py
```

macOS/Linux에서는 `py -3` 대신 `python3`, `.venv/Scripts/python` 대신 `.venv/bin/python`을 씁니다.

`build_sprites.py`는 다음 순서로 실행됩니다.

1. `fetch_sprites.py`: `manifest.json`에 적힌 파일을 `https://raw.githubusercontent.com/PokeAPI/sprites/<commit>/<path>`에서 받아 `Tools/sprites/_work/raw/{side}_{id}[_shiny].gif`로 저장합니다. 크기 상한(2MB), GIF 매직, 바이트 수와 sha256을 검사하며, 이미 받아 둔 파일의 해시가 맞으면 다시 받지 않습니다.
2. `convert_gif.py`: 배포 기본값으로 변환합니다. 기본값은 동일 프레임 셀 재사용(dedupe), 무손실 팔레트 PNG(indexed), GIF 좌표 유지(`--align gif`)입니다. 결과는 `Assets/Resources/Sprites/{side}_{id}[_shiny]_sheet.png`와 `_sheet.json`입니다.

다운로드한 GIF는 신뢰할 수 없는 데이터이므로 항상 `python -I`(격리 모드)로 실행합니다. 변환기도 파일 크기, 화면 크기(512px), 프레임 수(1024) 상한을 검사합니다.

## 매니페스트 갱신

로스터를 바꾸거나 원본 저장소의 새 커밋으로 올릴 때 실행합니다.

```sh
# master 의 현재 커밋으로 고정
Tools/sprites/.venv/Scripts/python -I Tools/sprites/fetch_sprites.py --update-manifest --ids 25,9,6,3,94,150
# 특정 커밋으로 고정
Tools/sprites/.venv/Scripts/python -I Tools/sprites/fetch_sprites.py --update-manifest --ids 25,9,6,3,94,150 --commit <40자리 SHA>
```

- 각 id마다 front/back × normal/shiny 4개 파일을 받아 sha256과 바이트 수를 기록합니다. 하나라도 실패하면 매니페스트를 갱신하지 않습니다.
- `--commit`을 생략하면 GitHub API(`/repos/PokeAPI/sprites/commits/<ref>`, 기본 `master`)로 SHA를 조회합니다.
- 갱신 후 `build_sprites.py`를 다시 실행하고 `manifest.json`의 변경분을 커밋합니다. 로스터에서 빠진 시트는 자동으로 지우지 않으며, 빌드 로그에 경고로 표시됩니다.

`manifest.json` 형식:

```json
{ "repo": "PokeAPI/sprites", "commit": "<SHA>", "fetchedAt": "YYYY-MM-DD",
  "files": [ { "id": 25, "side": "front", "variant": "normal",
               "path": "sprites/pokemon/versions/generation-v/black-white/animated/25.gif",
               "sha256": "...", "bytes": 23958 } ] }
```

경로 규칙은 다음과 같습니다. front는 `animated/{id}.gif`, `animated/shiny/{id}.gif`이고, back은 `animated/back/{id}.gif`, `animated/back/shiny/{id}.gif`입니다. 매니페스트의 `path`가 이 규칙과 다르면 다운로드를 거부합니다.

## 출력 형식

`{side}_{id}[_shiny]_sheet.png`: 프레임을 격자로 배치한 시트입니다. 폭은 2048px 이하이며, 가능하면 정사각형에 가깝게 배치합니다. 고유 색이 255개 이하이면 팔레트 PNG(투명 인덱스 0)로, 넘으면 RGBA PNG로 저장합니다. 알파는 0/255 이진값입니다.

`{side}_{id}[_shiny]_sheet.json`의 필드는 다음과 같습니다.

| 필드 | 설명 |
|---|---|
| `source` | 원본 GIF 파일명 (`_work/raw` 기준) |
| `id`, `side`, `variant` | 도감 번호, `front`/`back`, `normal`/`shiny` |
| `canvas` `{w,h}` | 프레임 한 칸의 크기(px). 모든 프레임이 같은 크기입니다 |
| `pivot` | 발 기준점입니다. `x,y`는 셀 좌상단 원점 기준 픽셀이며 항상 (w/2, h), 즉 바닥 중앙입니다. `unityNormalized`는 (0.5, 0)으로, `SpriteMetaData.pivot`에 그대로 쓰면 됩니다 |
| `loop`, `loopCount` | GIF NETSCAPE 루프입니다. `loop: true`는 무한 반복입니다. 루프 키가 없으면 `loop: false`, `loopCount: 1`입니다 |
| `totalDurationMs` | 전체 타임라인 길이(ms) |
| `align` | `gif`(원본 화면 좌표 유지, 상하 바운스 보존) 또는 `perframe`(프레임별 바닥 중앙 강제 정렬) |
| `sourceScreen`, `sourcePivot` | 원본 GIF 논리 화면 크기와, 그 좌표계에서의 기준점(디버그용) |
| `sheet` | `file`, `w`, `h`, `cols`, `rows`, `uniqueCells`(dedupe 후 셀 수), `pngMode`(`indexed`/`rgba`) |
| `frames[]` | 재생 순서대로 나열한 타임라인입니다. `x,y`는 시트 좌상단 원점 픽셀, `w,h`는 셀 크기, `delayMs`는 해당 프레임의 표시 시간입니다. dedupe 때문에 같은 `x,y`가 여러 번 나올 수 있습니다 |
| `metrics` | 변환 검증 지표: 알파 이진 여부, 화면 모서리 투명 여부, 불투명 픽셀 수 범위, 고유 색 수, 발 위치 흔들림 등 |

Unity에서 좌표를 쓸 때는 시트 좌상단 원점이므로 `rect.y = sheet.h - y - h`로 뒤집어야 합니다. PPU는 16입니다(카메라가 160×144px = 10×9 unit을 보여 주므로 도트 1px이 화면 1px).

수동 실행 옵션은 `convert_gif.py --help`에서 확인할 수 있습니다. `--align perframe`, `--layout row`, `--no-dedupe`, `--rgba`, `--preview`(마젠타 배경 미리보기, Assets 밖 폴더로 출력 권장)를 지원합니다.

## git에서 제외할 경로

- `Tools/sprites/.venv/`
- `Tools/sprites/_work/`
- `Assets/Resources/Sprites/` 및 `Assets/Resources/Sprites.meta`

## 저작권 고지

포켓몬 및 포켓몬 스프라이트의 모든 권리는 Nintendo, Creatures Inc., GAME FREAK inc.(및 The Pokémon Company)에 있습니다. 이 프로젝트는 위 권리자와 관계없는 비공식 팬 프로젝트이며, 상업적 목적이 없습니다. 스프라이트 파일은 저장소에 포함·재배포하지 않고, 사용자가 [PokeAPI/sprites](https://github.com/PokeAPI/sprites)에서 직접 받아 로컬에서만 변환합니다. 이 폴더의 스크립트와 매니페스트에는 이미지 데이터가 들어 있지 않습니다.
