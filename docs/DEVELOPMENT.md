# Development Notes

Directory layout, first-time setup, tests, web build and local run, deployment commands. The introduction and overall architecture diagram are in [README.md](../README.md).

## Directories

```text
Assets/
  Scripts/
    Domain/        Pokepedia.Domain        Pure C#, no UnityEngine references (models, Battle/ engine and events, Rules/ type chart, stats, damage, IRandom)
    Content/       Pokepedia.Content       Pure C# (JSON parser, data loader and catalogs, locales)
    Application/   Pokepedia.Application   Pure C#, namespace Pokepedia.App (NPC AI, simulator, connection flow, IHostBridge)
    Bridge/        Pokepedia.Bridge        Unity/WebGL implementation of IHostBridge + HostBridge.jslib
    Presentation/  Pokepedia.Presentation  Screens, input, effects (no rule decisions)
  Editor/          ProjectSetup, FontAtlasBuilder, WebBuild
  Tests/EditMode/  NUnit tests (no UnityEngine → also run under dotnet)
  Content/         Data (species, moves, roster, animation.json), Locale (ko/en/ja), Fonts (Galmuri11 + Static atlas), Audio (sound manifest), VFX
  Resources/       Generated Sprites and Audio (not committed)
  WebGLTemplates/Pokepedia/  Web template (HTML title and VS screens, 10:9 screen, key-input blocking, origin checks)
Tests.Dotnet/      Project that runs the pure C# tests without Unity (linked sources)
Tools/             sprites (sprite reproduction), web (local server, mock host, deploy), audio (type attack sound prototypes, render tools), unity
docs/              PROJECT.md (design, architecture, integration contract), ROADMAP.md (status, milestones, parked work), this document, sprite style comparison page
```

Dependency direction: `Presentation → Application → Content → Domain`, `Presentation → Bridge → Application` (interface implementation). Enforced by asmdef.

Why the `Pokepedia.Application` assembly uses the namespace `Pokepedia.App`: a `Pokepedia.Application` namespace would hide `UnityEngine.Application` from all code inside `Pokepedia.*` and cause compile errors.

## First-time setup

1. In Unity Hub, install 6000.3.26f1 + the **Web Build Support** module
2. Generate sprites (images are not in the repository; pinned to a PokeAPI/sprites commit SHA) → [Tools/sprites/README.md](../Tools/sprites/README.md)
   ```sh
   py -3 -m venv Tools/sprites/.venv
   Tools/sprites/.venv/Scripts/python -I -m pip install -r Tools/sprites/requirements.txt
   Tools/sprites/.venv/Scripts/python -I Tools/sprites/build_sprites.py
   ```
3. Open the project in Unity. Run the menu **Pokepedia → Setup Project** once (player settings, font atlas, entry scene, baking synthesized sounds)

## Tests

```sh
dotnet test Tests.Dotnet          # Without Unity (same as CI)
unity test . --mode EditMode      # Unity Test Runner
```

`Tests.Dotnet/` links the sources in `Assets/Scripts/{Domain,Content,Application}` and `Assets/Tests/EditMode` without copying them and runs them with `dotnet test`. It does not reference UnityEngine, so an accidental `using UnityEngine;` in the pure C# areas or tests breaks the dotnet build immediately. The language version is also pinned to C# 9 to catch newer syntax that Unity cannot compile. CI (`.github/workflows/domain-tests.yml`) trigger conditions and test criteria are in [CLAUDE.md](../CLAUDE.md).

## Web build and local run

```sh
# Release (Brotli, Runtime Speed, no LTO). Dev is WebBuild.Dev
"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod Pokepedia.EditorTools.WebBuild.Release -logFile Logs/build-web.log
# Serve with headers that match the pre-compressed (.br) files
python -I Tools/web/serve.py --dir Builds/Web --port 8001 --ancestors http://localhost:8000
# Mock Pokepedia (iframe + ready/init round-trip test)
python -I Tools/web/serve.py --dir Tools/web/host-test --port 8000
```

- Standalone: `http://localhost:8001/` → Demo mode
- In an iframe: `http://localhost:8000/` → opened with `?host=pokepedia`, `ready` → `init` round trip
- `serve.py --mbps 9 --latency-ms 60` measures loading at roughly Fast 4G speed.
- To open it on a phone on the same Wi-Fi, add `--host 0.0.0.0` (requires allowing it through Windows Firewall) and open `http://<PC IP>:<port>/?fps` on the phone. `?fps` shows the FPS and the slowest frame (ms) in the top left.

## Deployment (Cloudflare Pages)

```sh
# After a Release build. Requires a wrangler login (npx wrangler login)
python -I Tools/web/deploy.py                 # Production → https://pokepedia-battle.pages.dev
python -I Tools/web/deploy.py --branch test   # Preview → https://test.pokepedia-battle.pages.dev
```

Header rules are in `Tools/web/_headers` (PROJECT.md 10.3). The local `serve.py` mimics the same rules.
