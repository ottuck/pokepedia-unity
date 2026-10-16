#!/usr/bin/env python
"""PokeAPI/sprites Gen5 (Black/White) 애니메이션 GIF 다운로더 (매니페스트 기반, 커밋 SHA 고정).

평소 실행 (매니페스트대로 받고 sha256 검증):
  python -I fetch_sprites.py [--manifest manifest.json] [--out _work/raw]

매니페스트 갱신 (로스터 변경 / 원본 저장소 커밋 갱신 시):
  python -I fetch_sprites.py --update-manifest --ids 25,9,6,3,94,150 [--commit <SHA>|--ref master]

받은 파일은 신뢰할 수 없는 데이터: URL 은 고정 형식으로만 조립하고, 크기 상한·GIF 매직·sha256 을
검사한 뒤 {side}_{id}[_shiny].gif 로 저장한다. 표준 라이브러리만 사용(Pillow 불필요).
"""
import argparse
import datetime
import hashlib
import json
import os
import re
import sys
import urllib.request

REPO = "PokeAPI/sprites"
RAW_URL = "https://raw.githubusercontent.com/{repo}/{commit}/{path}"
API_COMMIT = "https://api.github.com/repos/{repo}/commits/{ref}"
ANIM_DIR = "sprites/pokemon/versions/generation-v/black-white/animated/"
SIDES = ("front", "back")
VARIANTS = ("normal", "shiny")
MAX_BYTES = 2 * 1024 * 1024
UA = "pokepedia-unity-sprites/1.0"
HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_MANIFEST = os.path.join(HERE, "manifest.json")
DEFAULT_OUT = os.path.join(HERE, "_work", "raw")

SHA_RE = re.compile(r"^[0-9a-f]{40}$")
PATH_RE = re.compile(r"^" + re.escape(ANIM_DIR) + r"(back/)?(shiny/)?[1-9][0-9]{0,4}\.gif$")
REF_RE = re.compile(r"^[A-Za-z0-9._/-]{1,100}$")


def sprite_path(pid, side, variant):
    sub = ("back/" if side == "back" else "") + ("shiny/" if variant == "shiny" else "")
    return f"{ANIM_DIR}{sub}{pid}.gif"


def local_name(pid, side, variant):
    return f"{side}_{pid}{'_shiny' if variant == 'shiny' else ''}.gif"


def http_get(url, limit):
    if not url.startswith("https://"):
        raise ValueError("https only")
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=30) as r:
        data = r.read(limit + 1)
    if len(data) > limit:
        raise ValueError(f"too large (>{limit} bytes)")
    return data


def fetch_gif(commit, path):
    data = http_get(RAW_URL.format(repo=REPO, commit=commit, path=path), MAX_BYTES)
    if data[:6] not in (b"GIF87a", b"GIF89a"):
        raise ValueError("not a GIF (magic mismatch)")
    return data


def write_atomic(dest, data):
    tmp = dest + ".part"
    with open(tmp, "wb") as f:
        f.write(data)
    os.replace(tmp, dest)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def resolve_commit(ref):
    if not REF_RE.match(ref):
        raise ValueError(f"bad ref: {ref!r}")
    body = http_get(API_COMMIT.format(repo=REPO, ref=ref), 4 * 1024 * 1024)
    sha = json.loads(body.decode("utf-8")).get("sha", "")
    if not SHA_RE.match(sha):
        raise ValueError("GitHub API returned no valid sha")
    return sha


def validate_entry(e):
    """매니페스트 항목 검증 -> (id, side, variant, path, sha256, bytes)."""
    pid, side, variant, path = e["id"], e["side"], e["variant"], e["path"]
    if not (isinstance(pid, int) and 0 < pid < 100000):
        raise ValueError(f"bad id: {pid!r}")
    if side not in SIDES or variant not in VARIANTS:
        raise ValueError(f"bad side/variant: {side!r}/{variant!r}")
    if path != sprite_path(pid, side, variant) or not PATH_RE.match(path):
        raise ValueError(f"unexpected path: {path!r}")
    digest, size = e["sha256"], e["bytes"]
    if not re.match(r"^[0-9a-f]{64}$", digest) or not (isinstance(size, int) and 0 < size <= MAX_BYTES):
        raise ValueError("bad sha256/bytes")
    return pid, side, variant, path, digest, size


def update_manifest(args):
    ids = [int(s) for s in args.ids.replace(" ", "").split(",") if s]
    if not ids or any(not (0 < i < 100000) for i in ids):
        raise SystemExit("--ids 는 쉼표로 구분한 양의 정수 목록이어야 합니다")
    commit = args.commit or resolve_commit(args.ref)
    if not SHA_RE.match(commit):
        raise SystemExit(f"--commit 은 40자리 SHA 여야 합니다: {commit!r}")
    os.makedirs(args.out, exist_ok=True)
    files, rc = [], 0
    for pid in ids:
        for side in SIDES:
            for variant in VARIANTS:
                path = sprite_path(pid, side, variant)
                try:
                    data = fetch_gif(commit, path)
                except Exception as ex:  # noqa: BLE001
                    rc = 1
                    print(f"FAIL {side:5} {variant:6} {pid:4} {ex}", file=sys.stderr)
                    continue
                write_atomic(os.path.join(args.out, local_name(pid, side, variant)), data)
                files.append({"id": pid, "side": side, "variant": variant, "path": path,
                              "sha256": sha256(data), "bytes": len(data)})
                print(f"OK   {side:5} {variant:6} {pid:4} {len(data):7} bytes")
    if rc:
        print("실패 항목이 있어 매니페스트를 갱신하지 않았습니다.", file=sys.stderr)
        return rc
    manifest = {"repo": REPO, "commit": commit,
                "fetchedAt": datetime.date.today().isoformat(), "files": files}
    tmp = args.manifest + ".part"
    with open(tmp, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")
    os.replace(tmp, args.manifest)
    print(f"manifest: {args.manifest} ({len(files)} files @ {commit})")
    return 0


def fetch_from_manifest(args):
    with open(args.manifest, encoding="utf-8") as f:
        m = json.load(f)
    if m.get("repo") != REPO or not SHA_RE.match(m.get("commit", "")):
        raise SystemExit("manifest: repo/commit 값이 올바르지 않습니다")
    commit = m["commit"]
    os.makedirs(args.out, exist_ok=True)
    rc = got = cached = 0
    for e in m["files"]:
        try:
            pid, side, variant, path, digest, size = validate_entry(e)
            dest = os.path.join(args.out, local_name(pid, side, variant))
            if os.path.isfile(dest) and os.path.getsize(dest) == size:
                with open(dest, "rb") as f:
                    if sha256(f.read()) == digest:
                        cached += 1
                        continue
            data = fetch_gif(commit, path)
            if len(data) != size or sha256(data) != digest:
                raise ValueError("sha256/size mismatch with manifest")
            write_atomic(dest, data)
            got += 1
            print(f"OK   {side:5} {variant:6} {pid:4} {size:7} bytes")
        except Exception as ex:  # noqa: BLE001
            rc = 1
            print(f"FAIL {e.get('side')} {e.get('variant')} {e.get('id')}: {ex}", file=sys.stderr)
    print(f"fetch: downloaded={got} cached={cached} failed={len(m['files']) - got - cached} @ {commit[:12]}")
    return rc


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--manifest", default=DEFAULT_MANIFEST)
    ap.add_argument("--out", default=DEFAULT_OUT, help="원본 GIF 저장 폴더 (전용 폴더 사용)")
    ap.add_argument("--update-manifest", action="store_true", help="--ids 로 매니페스트를 새로 작성")
    ap.add_argument("--ids", help="쉼표 구분 도감 번호 (예: 25,9,6,3,94,150)")
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--commit", help="고정할 PokeAPI/sprites 커밋 SHA (40자리)")
    g.add_argument("--ref", default="master", help="--commit 미지정 시 SHA 를 조회할 브랜치/태그")
    args = ap.parse_args()
    if args.update_manifest:
        if not args.ids:
            ap.error("--update-manifest 에는 --ids 가 필요합니다")
        return update_manifest(args)
    return fetch_from_manifest(args)


if __name__ == "__main__":
    sys.exit(main())
