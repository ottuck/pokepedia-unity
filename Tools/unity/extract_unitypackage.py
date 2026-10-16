""".unitypackage(tar.gz: <guid>/asset, asset.meta, pathname)를 프로젝트에 직접 푼다.
배치 모드에서 AssetDatabase.ImportPackage 가 -quit 전에 끝나지 않는 문제의 우회책.
사용: python -I extract_unitypackage.py <패키지> <프로젝트 루트>
"""
import os
import sys
import tarfile

pkg, root = sys.argv[1], sys.argv[2]
entries = {}
with tarfile.open(pkg, "r:gz") as tar:
    for m in tar.getmembers():
        if not m.isfile():
            continue
        guid, _, name = m.name.lstrip("./").partition("/")
        entries.setdefault(guid, {})[name] = tar.extractfile(m).read()

count = 0
for guid, files in entries.items():
    if "pathname" not in files:
        continue
    rel = files["pathname"].decode("utf-8").splitlines()[0].strip()
    if not rel.startswith("Assets/") or ".." in rel.split("/"):
        raise SystemExit(f"unexpected path: {rel}")
    dst = os.path.join(root, *rel.split("/"))
    if "asset" in files:
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        with open(dst, "wb") as f:
            f.write(files["asset"])
        count += 1
    else:
        os.makedirs(dst, exist_ok=True)
    if "asset.meta" in files:
        with open(dst + ".meta", "wb") as f:
            f.write(files["asset.meta"])
print(f"extracted {count} files")
