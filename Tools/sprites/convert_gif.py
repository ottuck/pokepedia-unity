#!/usr/bin/env python
"""애니메이션 GIF -> Unity용 스프라이트 시트 PNG + 프레임 메타 JSON 변환기 (Pillow).

사용:
  python -I convert_gif.py <gif 파일 또는 폴더>... --out <출력 폴더>
         [--layout grid|row] [--max-width 2048] [--align gif|perframe]
         [--no-dedupe] [--rgba] [--preview] [--summary <json>]

배포 기본값 = PoC 의 `--dedupe --indexed --align gif` 조합.
입력 파일명은 {front|back}_{id}[_shiny].gif 여야 하며, 출력은 {같은 이름}_sheet.png / _sheet.json.

처리:
  1. Pillow ImageSequence 로 프레임 분해(GIF disposal·프레임별 투명 인덱스 합성) -> RGBA.
  2. 모든 프레임의 알파 bbox 합집합으로 트림 -> 공통 캔버스.
     발 기준점(pivot) = (프레임별 bbox 중심 x 의 중앙값, 모든 프레임 중 가장 낮은 바닥 y).
     캔버스는 pivot x 가 가로 중앙이 되도록 좌우 대칭 패딩 -> Unity Sprite pivot (0.5, 0) 고정.
       --align gif      : GIF 논리 화면 좌표 유지(제작자가 의도한 상하 바운스 보존, 기본)
       --align perframe : 프레임별 bbox 바닥-중앙을 기준점에 강제 정렬(바운스 제거)
  3. 동일 프레임은 같은 셀 재사용(dedupe, 타임라인은 유지) 후 시트 배치(grid/row).
  4. 고유 불투명색 <=255 면 무손실 팔레트 PNG(투명 인덱스 0), 아니면 RGBA PNG.
입력 GIF 는 신뢰할 수 없는 데이터: 파일 크기·화면 크기·프레임 수 상한을 검사한다.
"""
import argparse
import glob
import hashlib
import json
import math
import os
import re
import statistics
import sys

from PIL import Image, ImageSequence

MAX_FILE_BYTES = 2 * 1024 * 1024
MAX_SCREEN = 512          # GIF 논리 화면 한 변 상한(px). BW 애니메이션은 96~ 수준
MAX_FRAMES = 1024
NAME_RE = re.compile(r"^(front|back)_([1-9][0-9]{0,4})(_shiny)?$")

Image.MAX_IMAGE_PIXELS = MAX_SCREEN * MAX_SCREEN * 64  # 시트 포함 여유 상한(디컴프레션 폭탄 방지)


def decompose(path):
    """GIF -> (frames[RGBA full-screen], delays_ms, loop_raw, info)"""
    if os.path.getsize(path) > MAX_FILE_BYTES:
        raise ValueError("file too large")
    with open(path, "rb") as f:
        if f.read(6) not in (b"GIF87a", b"GIF89a"):
            raise ValueError("not a GIF (magic mismatch)")
    im = Image.open(path)
    if im.format != "GIF":
        raise ValueError("not a GIF")
    if im.width > MAX_SCREEN or im.height > MAX_SCREEN:
        raise ValueError(f"screen too large: {im.size}")
    frames, delays, disposals = [], [], []
    for fr in ImageSequence.Iterator(im):
        if len(frames) >= MAX_FRAMES:
            raise ValueError("too many frames")
        disposals.append(getattr(fr, "disposal_method", 0))
        delays.append(int(fr.info.get("duration", 100)))
        frames.append(fr.convert("RGBA"))
    loop = im.info.get("loop")  # None = 키 없음(1회 재생), 0 = 무한, N = N회 추가 반복
    return frames, delays, loop, {"screen": im.size, "disposals": disposals}


def alpha_mask(img):
    return img.getchannel("A").point(lambda a: 255 if a else 0)


def build_frames(frames, align):
    boxes = [alpha_mask(f).getbbox() for f in frames]
    valid = [b for b in boxes if b]  # 완전 투명 프레임은 계산에서 제외
    if not valid:
        raise ValueError("all frames are fully transparent")
    ux0 = min(b[0] for b in valid); uy0 = min(b[1] for b in valid)
    ux1 = max(b[2] for b in valid); uy1 = max(b[3] for b in valid)
    pivot_x = statistics.median((b[0] + b[2]) / 2 for b in valid)  # 원본 화면 좌표
    pivot_y = uy1                                                  # 가장 낮은 바닥(지면)
    if align == "perframe":
        shifts = [(0, 0) if b is None else (round(pivot_x - (b[0] + b[2]) / 2), pivot_y - b[3])
                  for b in boxes]
        moved = [(b[0] + dx, b[1] + dy, b[2] + dx) for b, (dx, dy) in zip(boxes, shifts) if b]
        ux0 = min(m[0] for m in moved); uy0 = min(m[1] for m in moved); ux1 = max(m[2] for m in moved)
        uy1 = pivot_y
    else:
        shifts = [(0, 0)] * len(frames)
    half = max(math.ceil(pivot_x) - ux0, ux1 - math.floor(pivot_x), 1)
    cw = half * 2                                  # 짝수 폭: pivot x = cw/2 (정수)
    ch = uy1 - uy0
    px_int = round(pivot_x)
    out, foot_dev = [], []
    for f, b, (dx, dy) in zip(frames, boxes, shifts):
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        ox, oy = dx - px_int + half, dy - uy0      # 원본 (sx,sy) -> 캔버스 (sx+ox, sy+oy)
        canvas.alpha_composite(f, dest=(max(ox, 0), max(oy, 0)), source=(max(-ox, 0), max(-oy, 0)))
        out.append(clean_transparent(canvas))
        if b:
            foot_dev.append(((b[0] + b[2]) / 2 + dx - pivot_x, b[3] + dy - pivot_y))
    return out, cw, ch, (pivot_x, pivot_y), foot_dev


def clean_transparent(img):
    """완전 투명 픽셀의 RGB 를 0 으로 (인코더/팔레트 잔여색 제거)."""
    return Image.composite(img, Image.new("RGBA", img.size, (0, 0, 0, 0)), alpha_mask(img))


def alpha_metrics(frames_full, canvas_frames, foot_dev):
    opaque, binary_ok, corner_ok, pure_edge = [], True, True, 0
    colors = set()
    for f in canvas_frames:
        hist = f.getchannel("A").histogram()
        if any(hist[i] for i in range(1, 255)):
            binary_ok = False
        opaque.append(hist[255])
        colors.update(c[1][:3] for c in (f.getcolors(maxcolors=1 << 20) or []) if c[1][3])
    for f in frames_full:
        w, h = f.size
        if any(f.getpixel(p)[3] for p in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1))):
            corner_ok = False
        for x in range(w):
            for y in (0, h - 1):
                r, g, b, a = f.getpixel((x, y))
                if a and (r, g, b) in ((255, 255, 255), (0, 0, 0)):
                    pure_edge += 1
    return {
        "alphaBinaryOnly": binary_ok,
        "fullScreenCornersTransparent": corner_ok,
        "opaquePxMin": min(opaque), "opaquePxMax": max(opaque),
        "uniqueOpaqueColors": len(colors),
        "pureBlackWhiteOnScreenTopBottomEdge": pure_edge,
        "footJitterXpxMaxAbs": round(max((abs(d[0]) for d in foot_dev), default=0), 2),
        "footBottomYpxRange": round((max(d[1] for d in foot_dev) - min(d[1] for d in foot_dev)) if foot_dev else 0, 2),
    }


def layout(n, cw, ch, mode, max_width):
    if mode == "row":
        return n, 1
    cols = max(1, min(n, max_width // cw))
    ideal = max(1, round(math.sqrt(n * ch / cw)))  # 폭 한도 내에서 정사각형에 가깝게
    if ideal * cw <= max_width:
        cols = min(cols, ideal)
    return cols, math.ceil(n / cols)


def to_indexed(sheet):
    """고유 불투명색 <=255 면 무손실 팔레트 PNG(투명 인덱스 0)로 변환. 불가하면 None."""
    opaque = {c[1][:3] for c in (sheet.getcolors(maxcolors=1 << 20) or []) if c[1][3]}
    if len(opaque) > 255:
        return None
    pal = [(0, 0, 0)] + sorted(opaque)
    idx = {c: i for i, c in enumerate(pal)}
    out = Image.new("P", sheet.size, 0)
    out.putpalette([v for c in pal for v in c])
    px, sp = out.load(), sheet.load()
    for y in range(sheet.height):
        for x in range(sheet.width):
            r, g, b, a = sp[x, y]
            px[x, y] = idx[(r, g, b)] if a else 0
    out.info["transparency"] = 0
    return out


def convert(path, out_dir, args):
    name = os.path.splitext(os.path.basename(path))[0]
    m = NAME_RE.match(name)
    if not m:
        raise ValueError(f"unexpected file name: {name!r} (expected front|back_<id>[_shiny])")
    side, pid, variant = m.group(1), int(m.group(2)), ("shiny" if m.group(3) else "normal")
    raw_size = os.path.getsize(path)
    frames, delays, loop, info = decompose(path)
    canvas_frames, cw, ch, (pivot_x, pivot_y), foot_dev = build_frames(frames, args.align)

    cells, order, seen = [], [], {}
    for f in canvas_frames:
        key = hashlib.sha1(f.tobytes()).hexdigest() if args.dedupe else len(cells)
        if key not in seen:
            seen[key] = len(cells)
            cells.append(f)
        order.append(seen[key])
    cols, rows = layout(len(cells), cw, ch, args.layout, args.max_width)
    sheet = Image.new("RGBA", (cols * cw, rows * ch), (0, 0, 0, 0))
    for i, f in enumerate(cells):
        sheet.paste(f, ((i % cols) * cw, (i // cols) * ch))

    png_path = os.path.join(out_dir, f"{name}_sheet.png")
    ind = None if args.rgba else to_indexed(sheet)
    if ind is not None:
        ind.save(png_path, optimize=True, transparency=0)
        mode = "indexed"
    else:
        sheet.save(png_path, optimize=True)
        mode = "rgba"
    if args.preview:
        bg = Image.new("RGBA", sheet.size, (255, 0, 255, 255))
        bg.alpha_composite(sheet)
        bg.convert("RGB").save(os.path.join(out_dir, f"{name}_preview.png"))

    frames_meta = [{"x": (k % cols) * cw, "y": (k // cols) * ch, "w": cw, "h": ch, "delayMs": d}
                   for k, d in zip(order, delays)]
    metrics = alpha_metrics(frames, canvas_frames, foot_dev)
    meta = {
        "source": os.path.basename(path), "id": pid, "side": side, "variant": variant,
        "canvas": {"w": cw, "h": ch},
        "pivot": {"x": cw / 2, "y": ch, "originTopLeft": True,
                  "unityNormalized": {"x": 0.5, "y": 0.0}},
        "loop": loop == 0, "loopCount": loop if loop is not None else 1,
        "totalDurationMs": sum(delays),
        "align": args.align,
        "sourceScreen": {"w": info["screen"][0], "h": info["screen"][1]},
        "sourcePivot": {"x": pivot_x, "y": pivot_y},
        "sheet": {"file": os.path.basename(png_path), "w": sheet.width, "h": sheet.height,
                  "cols": cols, "rows": rows, "uniqueCells": len(cells), "pngMode": mode},
        "frames": frames_meta,
        "metrics": metrics,
    }
    with open(os.path.join(out_dir, f"{name}_sheet.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, ensure_ascii=False, indent=1)
        f.write("\n")
    return {
        "name": name, "gifBytes": raw_size, "frames": len(frames), "cells": len(cells),
        "screen": info["screen"], "canvas": (cw, ch), "sheet": (sheet.width, sheet.height),
        "pngBytes": os.path.getsize(png_path), "mode": mode, "loop": meta["loop"],
        "totalMs": sum(delays), "metrics": metrics,
    }


def collect(inputs):
    files = []
    for p in inputs:
        if os.path.isdir(p):
            files += sorted(glob.glob(os.path.join(p, "*.gif")))
        else:
            files.append(p)
    return files


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("inputs", nargs="+", help="GIF 파일 또는 GIF 가 든 폴더")
    ap.add_argument("--out", required=True)
    ap.add_argument("--layout", choices=["grid", "row"], default="grid")
    ap.add_argument("--max-width", type=int, default=2048)
    ap.add_argument("--align", choices=["gif", "perframe"], default="gif")
    ap.add_argument("--no-dedupe", dest="dedupe", action="store_false", help="동일 프레임 셀 재사용 끄기")
    ap.add_argument("--rgba", action="store_true", help="팔레트(indexed) PNG 대신 항상 RGBA PNG")
    ap.add_argument("--preview", action="store_true", help="마젠타 배경 합성 미리보기 PNG 추가 출력")
    ap.add_argument("--summary", help="요약 JSON 저장 경로")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    files = collect(args.inputs)
    if not files:
        print("no input GIFs", file=sys.stderr)
        return 1
    rows, rc = [], 0
    for p in files:
        try:
            rows.append(convert(p, args.out, args))
        except Exception as ex:  # noqa: BLE001
            rc = 1
            print(f"FAIL {os.path.basename(p)}: {ex}", file=sys.stderr)
    print("name               gifB  fr cell canvas   sheet       pngB mode    loop totalMs")
    tg = tp = 0
    for r in rows:
        tg += r["gifBytes"]; tp += r["pngBytes"]
        print(f"{r['name']:<17}{r['gifBytes']:>7}{r['frames']:>4}{r['cells']:>5} "
              f"{r['canvas'][0]}x{r['canvas'][1]:<6} {r['sheet'][0]}x{r['sheet'][1]:<8}"
              f"{r['pngBytes']:>7} {r['mode']:<7} {str(r['loop']):<5}{r['totalMs']}")
    print(f"TOTAL sheets={len(rows)} gif={tg} png={tp}")
    if args.summary:
        with open(args.summary, "w", encoding="utf-8") as f:
            json.dump(rows, f, ensure_ascii=False, indent=1)
    return rc


if __name__ == "__main__":
    sys.exit(main())
