# Roadmap

Current status and milestones. **Agents check "Current status" before starting work, and update this document in the same PR whenever the stage changes or new work appears.** Decisions and specs go in the matching section of [PROJECT.md](PROJECT.md); no dated progress log is kept here. Agent rules are in [CLAUDE.md](../CLAUDE.md).

## Current status

- **Complete.** The user has declared this state complete. Pokepedia quiz → Pikachu sticker → battle (Pikachu vs Blastoise) is live as one flow. Standalone: https://battle.pokepedia.dev ; inside Pokepedia: the "Battle" menu on https://pokepedia.dev and the first-quiz reward screen.
- **Next work:** none. Only bug fixes (if the flow breaks on web or mobile). The "Parked" items below are picked up when the user brings them back.

## Milestones (all done)

| | Scope |
|---|---|
| M0 | PoC: Korean/English/Japanese fonts, WebGL size and loading, iframe + postMessage, key input, hosting headers, attack effects, BW sprites, mobile layout, audio — conclusions in PROJECT.md sections 4, 7.7, 8, 10 |
| M1 | Real project and repository, asmdef, tests (`dotnet test`, CI), web build settings, entry screen and bridge |
| M2 | Pure C# engine: 18-type chart, stats and damage, accuracy and critical hits, turn order, win/loss, NPC AI, seeded balance simulation |
| M3 | Vertical Slice (Pikachu vs Blastoise): battle flow and screen, event playback, attack/hit effects, synthesized sound and BGM, world/HUD split, 2.5D screen |
| M4 | Web deployment (Cloudflare Pages, `battle.pokepedia.dev`), README introduction |
| M5 | Finished presentation: VS intro, attack camera, Tackle, screen structure cleanup (`BattleViewState`) |
| M6 | Pokepedia integration (Collection mode): `/[locale]/battle`, first-quiz Pikachu, battle button on the reward screen, gold border in the collection. Later polish: choice on faint, Blastoise move mixing, HTML title/VS screens, integer-scaled battle frame |
| M7 | Size measurement only (decided not to optimize, PROJECT.md 10.3) |

## Parked (not now)

- Releasing the four Standard Pokémon (Charizard, Venusaur, Gengar, Mewtwo): shared effects, per-body-shape display, balance, NPC weights. The data remains; only the art was removed from the build (PROJECT.md 5.1, 10.3).
- Opening other Pokémon as the player (Blastoise, etc.) — needs back-view effects and a balance check (Blastoise as player wins about 11%, PROJECT.md 6.5).
- 3v3 battles, Pokémon switching and the Bag menu.
- Raising the game resolution (240×216 → smooth when shown large) — a big job that reworks UI, backgrounds and fonts.
- Collection "battle-ready" filter (PROJECT.md 9.6), Pokémon cries (PROJECT.md 8.3).
