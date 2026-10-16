#!/usr/bin/env python
"""스프라이트 일괄 재현: 매니페스트대로 다운로드(sha256 검증) -> 시트 변환 -> Assets/Resources/Sprites/.

사용 (저장소 루트에서):
  Tools/sprites/.venv/Scripts/python -I Tools/sprites/build_sprites.py [--out Assets/Resources/Sprites] [--preview]

각 단계는 별도 프로세스(`python -I`)로 실행한다. -I 는 스크립트 폴더를 sys.path 에 넣지 않으므로
모듈 import 대신 경로로 호출한다.
"""
import argparse
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
MANIFEST = os.path.join(HERE, "manifest.json")
RAW = os.path.join(HERE, "_work", "raw")
DEFAULT_OUT = os.path.join(ROOT, "Assets", "Resources", "Sprites")


def run(*argv):
    cmd = [sys.executable, "-I", *argv]
    print("$", " ".join(os.path.relpath(a, ROOT) if os.path.isabs(a) else a for a in cmd[2:]), flush=True)
    return subprocess.call(cmd)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--preview", action="store_true", help="미리보기 PNG 도 출력(Assets 밖 폴더 권장)")
    args = ap.parse_args()

    if run(os.path.join(HERE, "fetch_sprites.py"), "--manifest", MANIFEST, "--out", RAW):
        print("fetch 실패: 위 로그를 확인하세요.", file=sys.stderr)
        return 1

    with open(MANIFEST, encoding="utf-8") as f:
        entries = json.load(f)["files"]
    names = [f"{e['side']}_{e['id']}{'_shiny' if e['variant'] == 'shiny' else ''}" for e in entries]
    gifs = [os.path.join(RAW, n + ".gif") for n in names]

    os.makedirs(args.out, exist_ok=True)
    extra = ["--preview"] if args.preview else []
    if run(os.path.join(HERE, "convert_gif.py"), *gifs, "--out", args.out, *extra):
        print("convert 실패: 위 로그를 확인하세요.", file=sys.stderr)
        return 1

    expected = {n + "_sheet" + ext for n in names for ext in (".png", ".json")}
    stale = sorted(f for f in os.listdir(args.out)
                   if f.endswith(("_sheet.png", "_sheet.json")) and f not in expected)
    if stale:
        print(f"주의: 매니페스트에 없는 시트 {len(stale)}개가 출력 폴더에 남아 있습니다: {', '.join(stale)}")
    total = sum(os.path.getsize(os.path.join(args.out, f)) for f in expected)
    print(f"done: {len(names)} sheets -> {args.out} ({total} bytes, png+json)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
