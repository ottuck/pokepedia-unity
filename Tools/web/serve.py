"""PoC 정적 서버: Unity WebGL 사전 압축 파일(.br/.gz)을 올바른 헤더로 서빙한다.

사용: python -I serve.py --dir <폴더> --port 8001 [--ancestors http://localhost:8000]
 - *.br / *.gz  -> Content-Encoding 지정, Content-Type 은 안쪽 확장자 기준 (wasm -> application/wasm)
 - Content-Security-Policy: frame-ancestors 로 임베드 허용 origin 제한 (운영 호스팅 헤더 모의)
 - Cache-Control: 해시 파일명(Build/*)은 immutable, 나머지는 no-cache
"""
import argparse
import mimetypes
import os
import time
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer

TYPES = {".js": "application/javascript", ".wasm": "application/wasm", ".data": "application/octet-stream",
         ".json": "application/json", ".html": "text/html; charset=utf-8", ".css": "text/css", ".png": "image/png"}


class Handler(SimpleHTTPRequestHandler):
    ancestors = "'self'"
    throttle_bps = 0  # 0 = 무제한. 예: 9 Mbps(크롬 Fast 4G 근사) = 1_125_000
    latency_s = 0.0

    def copyfile(self, source, outputfile):
        if not self.throttle_bps:
            return super().copyfile(source, outputfile)
        time.sleep(self.latency_s)
        chunk = 16384
        start = time.monotonic()
        sent = 0
        while True:
            buf = source.read(chunk)
            if not buf:
                break
            outputfile.write(buf)
            sent += len(buf)
            ahead = sent / self.throttle_bps - (time.monotonic() - start)
            if ahead > 0:
                time.sleep(ahead)

    def guess_type(self, path):
        base = path
        for ext in (".br", ".gz"):
            if base.endswith(ext):
                base = base[: -len(ext)]
        e = os.path.splitext(base)[1].lower()
        return TYPES.get(e) or mimetypes.guess_type(base)[0] or "application/octet-stream"

    def end_headers(self):
        p = self.path.split("?")[0]
        if p.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        elif p.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Content-Security-Policy", f"frame-ancestors {self.ancestors}")
        self.send_header("Access-Control-Allow-Origin", "*")
        if "/Build/" in p:
            self.send_header("Cache-Control", "public, max-age=31536000, immutable")
        else:
            self.send_header("Cache-Control", "no-cache")
        super().end_headers()


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--port", type=int, default=8001)
    ap.add_argument("--ancestors", default="http://localhost:8000")
    ap.add_argument("--mbps", type=float, default=0, help="전송 속도 제한(Mbps). 크롬 Fast 4G 근사 = 9")
    ap.add_argument("--latency-ms", type=float, default=0)
    a = ap.parse_args()
    Handler.ancestors = a.ancestors
    Handler.throttle_bps = int(a.mbps * 1_000_000 / 8)
    Handler.latency_s = a.latency_ms / 1000
    os.chdir(a.dir)
    print(f"serving {a.dir} on http://localhost:{a.port} (frame-ancestors {a.ancestors})", flush=True)
    ThreadingHTTPServer(("127.0.0.1", a.port), Handler).serve_forever()
