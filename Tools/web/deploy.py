"""Cloudflare Pages 배포 (PROJECT.md 10.5): 로컬 Release 빌드를 그대로 올린다.

사용: python -I Tools/web/deploy.py [--build Builds/Web] [--project pokepedia-battle] [--branch main]
 - Builds/Web 을 Builds/deploy 로 복사하고 Tools/web/_headers 를 넣은 뒤 `npx wrangler pages deploy` 로 올린다.
 - --branch 가 프로젝트의 production 브랜치(main)면 본 배포, 다른 이름이면 미리보기 배포(<branch>.<project>.pages.dev).
 - wrangler 로그인(`npx wrangler login`)이 필요하다. 빌드는 먼저 Unity 에서 WebBuild.Release 로 만든다.
"""
import argparse
import os
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--build", default="Builds/Web")
    ap.add_argument("--project", default="pokepedia-battle")
    ap.add_argument("--branch", default="main")
    a = ap.parse_args()

    src = os.path.join(ROOT, a.build)
    if not os.path.isfile(os.path.join(src, "index.html")):
        sys.exit(f"빌드가 없다: {src} (Unity WebBuild.Release 를 먼저 실행)")
    out = os.path.join(ROOT, "Builds", "deploy")
    shutil.rmtree(out, ignore_errors=True)
    shutil.copytree(src, out)
    shutil.copy(os.path.join(ROOT, "Tools", "web", "_headers"), os.path.join(out, "_headers"))

    cmd = ["npx", "--yes", "wrangler@4", "pages", "deploy", out,
           "--project-name", a.project, "--branch", a.branch, "--commit-dirty=true"]
    print(" ".join(cmd), flush=True)
    sys.exit(subprocess.call(cmd, shell=(os.name == "nt")))


if __name__ == "__main__":
    main()
