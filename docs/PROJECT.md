# Pokepedia Battle — PROJECT

The **current spec** for design, architecture and the Pokepedia integration contract. Current status and parked work are in [ROADMAP.md](ROADMAP.md), build and deploy commands in [DEVELOPMENT.md](DEVELOPMENT.md), agent rules in [CLAUDE.md](../CLAUDE.md).

> Code comments reference section numbers, so do not renumber sections.

## At a glance

| Item | Details |
|---|---|
| What it is | A Unity 2D turn-based game that handles the **battle** part of Pokepedia's quiz → sticker collection → **battle** loop. Shipped as WebGL on `battle.pokepedia.dev` and embedded by Pokepedia in an iframe |
| Public scope | Pikachu (player) vs Blastoise (opponent), 1v1. The four Standard Pokémon are kept as data only (5.1) |
| Quality tiers | Showcase / Standard, one engine, effects as override data (5.2) |
| Battle flow | No trainers, Fight/Run, the NPC mixes moves per turn, on faint [Cheer on / Handle it yourself], with a single opponent it ends on the victory scene (5.3) |
| Screens | Title and VS in HTML, battle in 240×216 integer-scaled pixel-art 2.5D (world/HUD split) (5.5) |
| Rules | Modern data, L50, physical/special, accuracy and critical hits, 3 moves, no PP or status conditions (6.3, 6.4) |
| Balance | 4–6 turns, base stat compression k 0.65, Demo Pikachu win rate 85–93% (6.5) |
| Tech stack | Unity 6.3 LTS, C# 9, URP 2D, uGUI + TMP, pure C# Domain, NUnit, JSON (4) |
| Assets | PokeAPI BW animated sprites (not committed), Galmuri11, all sounds synthesized in code (7.7, 8) |
| Integration | iframe + one-way postMessage, Unity computes owned ∩ supported (9.2) |
| Infrastructure | No server or DB, Cloudflare Pages, no Pokepedia DB changes (9.3, 10) |

---

## 1. Project overview

- A **Unity + C# 2D turn-based battle** where you fight an NPC with the sticker Pokémon you collected in Pokepedia. The goal is **to design and present a small scope properly**, not to cover many Pokémon.
- Deployed to the web (WebGL) and entered from Pokepedia through an iframe. It can also be played standalone (Demo).
- There is no mobile app, but it **must look perfect in mobile browsers.**
- Uses Unity broadly (separate world/UI rendering, Light2D, post-processing, shaders, VFX, camera work). This takes priority over size and effort.

**Definition of done** (all met): pure C# rules with automated tests · UI, animation, sound and effects built directly in Unity · a finished, browser-playable mini game released publicly · documentation that explains the architecture and design intent. The number of Pokémon is not a criterion.

---

## 2. Pokepedia

- Site https://pokepedia.dev · repository https://github.com/ottuck/pokepedia · design doc is `docs/system_design.md` in that repository, merge policy is that repository's `CLAUDE.md`.
- Pokepedia's game is a **silhouette name-guessing quiz**. Battle and move data are created by this repository.

| Item | Details |
|---|---|
| Stack | Next.js 16, React 19, TypeScript, Tailwind v4, Zustand, next-intl, Supabase (Postgres, Auth, Storage), Vercel, Vitest |
| Accounts | Start as a guest (anonymous), link and merge with Google |
| Collection | `user_sticker (user_id, pokemon_id, variant normal\|shiny, quantity)` — RLS allows SELECT on own rows |
| Pokémon data | `pokemon` table (public read): Korean/English/Japanese names, types, 6 stats, artwork path. PokeAPI → sync script → Supabase |
| Artwork | Supabase Storage `pokemon-artwork/normal/{3-digit dex number}.webp` (the battle title and VS screens use this too) |
| Environments | Prod and Preview share one Supabase project; migrations are applied automatically on merge to main |

---

## 3. Game loop

```text
Pokepedia quiz (React)
   ↓ For users without a Pikachu sticker, the first question of a new run = Pikachu (9.7)
Sticker reward screen → [Battle with my Pikachu] (a lone gold button for a first Pikachu)
   ↓ /[locale]/battle (iframe)
Unity battle: owned ∩ playable → 1v1 against the NPC → ends on the victory scene
```

- There are two ways into the battle: **Try the battle (Demo)** and **the Pikachu sticker** (Collection). The "Battle" menu in the navigation is the same page.
- The connection is **one-way** (React → Unity). Battle results do not change stickers or rankings.

---

## 4. Tech stack

| Area | Technology |
|---|---|
| Engine | Unity 6.3 LTS (6000.3.26f1) — supported until December 2027 (6.0 LTS ends October 2026) |
| Language | Pinned to C# 9 (`Tests.Dotnet` catches newer syntax first) |
| Rendering | **URP 2D** (Light2D, Sprite-Lit, Volume post-processing) |
| UI | uGUI + TextMesh Pro (Galmuri11 Static atlas, 7.7) |
| Data | JSON → `ContentLoader` (pure C#) |
| Tests | NUnit (Unity Test Framework + `dotnet test`) |
| Web | Unity Web (WebGL), pre-compressed Brotli, Cloudflare Pages |
| Server and DB | None. Unity does not handle auth or databases |

**Rendering and build settings, and why**
- **URP 2D:** Built-in RP is about 1.2 MB smaller, but it is deprecated from Unity 6.5 and loses 2D Light, Sprite-Lit, Volume post-processing and 2D Shader Graph.
- **Splash logo:** The texture (2.7 MB) is not removed by `SplashScreen.show=false` alone; `showUnityLogo=false` must also be off (Unity Issue UUM-63958, Won't Fix). `ProjectSetup` turns both off.
- **Data Caching off:** Safari does not support IndexedDB inside iframes, and loading the same origin in two tabs at once can corrupt the IndexedDB cache. Repeat-visit caching relies on hashed file names + `immutable` HTTP caching.
- **Build:** Brotli, managed code stripping Minimal, IL2CPP optimized for size. Release uses Runtime Speed (no LTO), Dev prioritizes build speed. Stripping High and Disk Size LTO are not used because builds take 7–10 minutes and may cause problems.
- Using `init` on a `record` in Unity requires defining `IsExternalInit` yourself (`Assets/Scripts/Domain/IsExternalInit.cs`).

---

## 5. Game scope

### 5.1 Supported Pokémon roster

| Tier | Pokémon | Status |
|---|---|---|
| Showcase | Pikachu #025 (Electric) | **Public, the only player** |
| Showcase | Blastoise #009 (Water) | **Public, opponent only** |
| Standard | Charizard (Fire/Flying) · Venusaur (Grass/Poison) · Gengar (Ghost/Poison) · Mewtwo (Psychic, legendary) | Parked: data only, art removed from the build (`npcWeight` 0, 10.3) |

- `roster.json` defines the supported list and roles (`playable`, `npcWeight`). **The engine does not know how many Pokémon are supported, and engine and screen code never contain Pokémon names.**
- Adding one means adding a roster entry (data + sprites + effect overrides if needed). The full 18-type chart is implemented; moves and effects are made only for types the roster uses.
- Introductory docs state accurately that only some of the 151 are supported.

### 5.2 Quality tiers: Showcase / Standard

- There is one engine; **only the visual presentation differs.** Showcase also plays on the real engine.
- No separate system: species- and move-level **override data** sits on top of the effect director's default timelines (contact / projectile / effect): `Assets/Content/Data/animation.json` → `AnimationCatalog`.
  - Species profile (`animProfile` key): `windupSec` (wind-up time), `lungePx` (lunge), `hitShakePx` (hit shake), `knockbackPx` (knockback), `mouth` (mouth position in the front view, the launch point for water moves), `moveFx` (move id → dedicated effect id). Missing values fall back to `defaults`.
  - Move-specific attack sounds `moveSfx` (top level, move id → sound id, 8.3). Moves without one use the type attack sound.
  - Effect colors (primary, secondary) for each of the 18 types.
- Standard gets the shared timelines + type colors; Showcase gets dedicated effects (5.5) and sounds timed to them.

### 5.3 Battle flow and screen layout

No trainers (people) appear on screen. It plays as if you ran into a wild Pokémon.

```text
Loading → Title screen (HTML, [▶ START])
   ↓  with ?host=pokepedia, ready → wait for init (9.2)
VS scene (HTML, about 2 s, skipped with a confirm input)
   ↓
"A wild ○○ appeared! Go! ○○!"
   ↓
Action: Fight / Run ── Fight → move → resolve turn → (repeat)
   ↓
If the player faints: [Cheer on ○○ / Handle it yourself]
If the opponent faints: victory → (with a single opponent candidate) ends on the victory scene
```

**Flow**
- **Title screen:** drawn by the web template (5.5). States are loading / title / connecting / connection failed ([Retry], [Try Demo]) / no sticker ([Try Demo]). Keyboard Z, Enter, Space; L opens the lab.
- **Run:** With several opponent candidates, "Got away safely!" and then a new battle against a different opponent (full HP). With a single candidate (Demo, and Collection today), the first attempt shows "Can't escape!", and **from the second on, "Really run away?" [Quit / Keep fighting]** (default cursor on Keep fighting, counted again each battle) — so it never feels like a trap.
- **Choice on faint:** In the Demo and the first Collection battle there is no reason to retry after a loss, so **every time** the player faints they pick from "Trainer, what will you do?" (no cancel, `BattleSession.faintChoice`).
  - **Cheer on:** Gets back up with full HP and deals ×1.5 damage afterwards (`CheerPowerBoost`, does not stack). Engine `Revive`, `Revived`. Named "the trainer's cheer" instead of after a character from the original games.
  - **Handle it yourself:** A Poké Ball hits the opponent on the head, the opponent faints and **the player wins** (`battleEnded` win in Collection). Engine `KnockOut` (knocks out the side that just won, flipping the result), `TrainerThrewBall`. A finisher for users who want to end it right away.
  - So these battles never produce a loss. A loss ([Try again]) exists only in sessions with `faintChoice` off.
- **Victory:** With a single opponent candidate, it ends on the victory scene ("You won the battle!", sunset sky) with no result menu. Leaving is through the Pokepedia menu, or a reload at the standalone address. With several candidates: [Next opponent] / [Title screen]. No rewards or score.
- **NPC AI (one difficulty):** Ranks moves by expected damage (power, accuracy, STAB, type effectiveness) and **mixes them per turn**: turns 1 and 3 use the 1st, turn 2 the 2nd, turn 4 the 3rd, then repeats every 4 turns (no randomness, `NpcBattleAi`). Using the same move every time is monotonous.

**Screen and input**
- **Move menu:** Three large type-colored buttons (`MoveCards`) — name, power and accuracy are inside the button; pressing one shows a 2px press-and-bounce reaction and then uses the move immediately. The three buttons normally look the same; only when choosing with the keyboard arrows is the current button raised. Tapping outside the buttons goes back (phones have no X or Esc). Long names only get tighter letter spacing; English accuracy is "Acc", Japanese uses "威力·命中". No PP.
- **Turn messages:** "○○ used ○○!" → miss / critical / effectiveness text → HP bar drains → faint. Typewriter-style output. The opponent uses separate keys such as "The wild ○○" (the original games distinguish them, and in English capitalization makes a prefix approach impossible). Keys are listed in `BattleTextKeys`.
- **Layout:** Game Boy style (opponent front view top right, player back view bottom left, name, `:L50`, HP bar and numbers, message box and menu at the bottom).
- **Base resolution 240×216** (10:9, 1.5× of 160×144 — BW sprites are up to 112px). Scaled **only by integer factors** (`PixelCanvasScaler`, `PixelCamera`). Text is Galmuri11 12px.
- **Input:** Arrow keys + Z/X, click, touch.

### 5.4 Development approach and scope

- **Vertical Slice first:** Finish Pikachu vs Blastoise at final quality before adding content.
- **Size is the lowest priority:** Spend size when it helps screen quality (`PostProcessData` wired up for post-processing). The only real constraint is the host's 25 MiB per-file limit (10.2).
- **Performance baseline:** about 13 MB transferred, first load about 3.5 s on desktop, fine on 4G phones. 75 FPS on desktop (monitor cap), 50–60 FPS in iPhone Safari, so post-processing and lighting are not reduced. Low-end Android has not been measured.
- **Out of scope:** supporting all 151, unique animations per Pokémon, trainer appearances and intro scenes, difficulty selection, victory rewards and rankings, online PvP, game servers, reimplementing the quiz in Unity, PP and status conditions, 3D models, cameras and lighting.
- 3v3 battles and the switch/Bag menus can be added later because the engine uses a party structure (7.2). Work that can be brought back is under ROADMAP "Parked".

### 5.5 Screen presentation

**Two layers:** The title and VS screens are the **web template (HTML)**; the battle is **Unity pixel art (2.5D)**. Drawing the title screen in 240×216 Unity pixels would blur the text and logo.

**HTML title and VS screens** (`Assets/WebGLTemplates/Pokepedia/index.html`)
- **A sibling of the Pokepedia quiz title:** the same sky gradient, square clouds, four-pointed sparkles and two-layer pixel-bordered "Pokepedia" logo, with only the subtitle changed to "BATTLE". Pikachu and Blastoise face each other as official artwork silhouettes (dropped if they fail to load) with a pixel VS in the middle. Coming over from the quiz, it reads as one game.
- **Start:** A blinking "▶ START" like the quiz's PRESS START (English in all three languages, together with the "BATTLE" subtitle).
- **Loading:** A thin progress bar in the middle + percentage on the right + a TIP (100% when the download finishes, no "preparing" text).
- **Connecting / failed / no sticker:** A small notice + menu inside a Game Boy text box (double border). When the text box is up, the VS in the middle is hidden.
- **Menu:** The selected item always has a ▶ cursor in front (first item initially). Move with mouse or arrow keys and select with Z, Enter, Space (handled by the template; Unity only takes keys on the title screen).
- **Font: Galmuri11** (OFL, kept in the template folder). The logo and VS (Latin) use the same Bold as the quiz logo (`Galmuri11-Bold.woff2`); Korean uses Regular (`Galmuri11.woff`, only Korean, Latin and symbols extracted from the in-game TTF with fontTools). Japanese kana use DotGothic16 (Google Fonts).
  - **In Bold, the two top strokes of 'ㅌ' in '틀' merge, so "배틀" reads as "배들" (glyph design).** So Regular is overlaid horizontally by one pixel (1em/12) to embolden it (only vertical strokes thicken; horizontal gaps remain).
  - Font sizes are multiples of the 12px grid (CSS `round()`, otherwise the pixel rows blur).
- **VS (view = vs):** On the same sky, the logo and lower area disappear; at 0.62 s, with a white flash, both silhouettes regain color (the quiz's "answer reveal") and VS stamps in, growing in steps. Names sit under each Pokémon's feet in dark text with a thin white outline. From 1.75 s it fades over 0.45 s, revealing the pixel battle.
- **Sizing:** Everything is relative to the game width (cqw), so the composition is the same on desktop and phone. Overlay boxes use `overflow: clip` (hidden makes a scroll container, which shifts when the screen changes).
- **Unity ↔ HTML:** Unity sends the state, text (from locale files) and dex numbers (`HostBridge.ShowTitle` → `window.pokeTitle.show`, view = loading|idle|connecting|failed|no_partner|vs|hidden), and the template returns button presses via `SendMessage("App", "OnTitleAction", start|retry|demo)` — called directly inside the click handler so the first sound (audio unlock) is not blocked. VS sounds (entry sound, critical hit sound), timing and skipping (confirm input after 0.3 s) are handled by Unity (`BattleEventPlayer.PlayHtmlVs`).
- The editor has no HTML, so the title is a simple Unity panel and VS falls back to the pixel `VsIntro`.

**Unity battle screen: 2.5D + world/HUD split**
- Pokémon stay as 2D sprites; depth comes from the background, shadows, camera, lighting and post-processing. No 3D.
- **World** (`BattleWorld`: `SpriteRenderer`, `PixelCamera`, Light2D, post-processing, particles) and **HUD** (uGUI Overlay canvas: HP, menus, messages) are separate. The HUD gets no post-processing, so text stays crisp. Only the full-screen flash sits at the top of the HUD.
- **Never break the pixels:** Point filtering + integer scaling. Perspective comes from placement and fixed sizes; parallax and movement are in whole pixels. Zoom only for brief moments of motion (always 1× when still), no rotation. The margin left by integer scaling is the frame color `#262a35` (camera background, letterbox bars, web template).
- **Background "Rainy Evening"** (`BattleBackdrop`, dithered pixel art drawn in code): sky, clouds, two layers of mountains, two rows of conifers, grass, with per-layer parallax. Two layers of fog, rain, raindrop ripples on the ground, occasional lightning flashes, a vignette (sprite). A dithered ellipse shadow under each Pokémon (no platforms).
- **Lighting and post-processing:** Global light (cool blue) + type-colored point light `BattleLighting.Pulse` (additive blend, so only bright parts bloom). `BattlePostFx`: Bloom (threshold 1.0) and a slight saturation/contrast boost. Background and Pokémon are Sprite-Lit; effects, shadows, fog, rain and vignette are Sprite-Unlit.
- **Attack camera:** 1.12× toward the attacker during the wind-up, crossing to the target at 1.08× at the release moment (`ReleaseSec`, the same moment as the attack sound), +4% on impact (+8% on a critical), then back to center at 1×. It moves the world's Pan node and keeps the camera scale fixed. Center movement is clamped so nothing outside the background shows when zoomed.
- **Move effects** (`AnimationDirector`, assigned via `moveFx`):

| id | Move | Look |
|---|---|---|
| `thunder_arc` | Thunderbolt | Lightning reaching from outside the attacker's body to the **surface** of the target's body |
| `hydro_beam` | Hydro Pump | A straight three-layer water jet from **just in front of** Blastoise's mouth (`mouth`, first frame held while firing), spray at the surface, the target is pushed back |
| `hydro_geyser` | Water Pledge | The ground rumbles and bubbles boil underfoot, four small water columns around it → a big column erupts underfoot and lifts the target |
| `contact` | Tackle (shared contact) | Run-up → lunge with afterimages → pixel star impact sparks, debris and dust at the surface → brief hitstop → return |
| `lightning_strike` | Thunder | A lightning bolt falling from the sky |
| (spare) `thunder_fork`, `hydro_burst` | — | Lab only. Usable if assigned to a move |

  The shared timelines for projectile and effect types are a sphere (projectile) and a ring (effect). The end point is the body **surface** (aiming at the center looks like piercing the body).
- **Hit:** `Pokepedia/Sprite Flash` shader flash + shake + knockback; critical hits add a screen flash and shake; misses get a dodge motion.
- **Cheer revive:** The camera closes in and a golden light shaft comes down from the sky (`VfxLayer.LightShaft`) → the Pokémon rises in the light and reappears → golden screen flash → HP fills.
- **Handle it yourself:** From the bottom left of the screen, a Poké Ball (11×11 pixels drawn in code) spins along a parabola, hits the opponent on the head and bounces off → flash and flinch → stars circle overhead as it staggers → HP drops to 0 → faint (`AnimationDirector.TrainerBall`).
- **Victory:** At 1.4 s the rain, fog and clouds clear, a sunset sky layer appears and the global light warms. The winner hops three times in the light and the camera closes in (if the winner is the side that fainted, only the weather changes). The weather returns in the next battle.
- **HP UI:** A rounded double-bordered box, a base line on the Pokémon's side, a pixel "HP:" label, numbers as "126/ 126".

**Effects lab**
- Add `?lab` to the URL (or press L on the title screen). Loops the dedicated effects (three Thunderbolt variants, Hydro Pump, Water Pledge, Tackle from both sides), the Poké Ball (Ball), the cheer revive (Revive), VS and victory; the result button cycles hit → miss → critical.
- Sounds play just as in a real battle (Tackle = the dedicated Tackle sound, WP = water column eruption). HP does not change.
- `?fps` shows the FPS and the slowest frame (`?lab&fps` works). Record the chosen result in `moveFx` in `animation.json`.

---

## 6. Battle rules and data design

### 6.1 Principles

- Recreating the original games is not the goal. There is **one** engine, and Pokémon are expressed **only as data** (no per-Pokémon classes or scripts).
- Data follows **modern standards** (18 types, 6 stats, separate Sp. Atk/Sp. Def); rules are simplified rules of our own. The Game Boy feel comes from visuals and sound.
- Move names are decoration; data (type, power, accuracy, category) decides the rules.

### 6.2 Pokémon data

- `Assets/Content/Data/species.json`, `moves.json`, `roster.json`, `animation.json`. Loader `ContentLoader` (pure C#, strict schema: missing fields, unknown fields and invalid values throw a `FormatException` naming the entry).
- Base stats and types are **PokeAPI** `/pokemon/{id}` values (the same source as the Pokepedia DB). No runtime DB lookups.
- Names are not in the engine. They are looked up by `nameKey` (`species.025`, `move.thunderbolt`) in the locales (`Assets/Content/Locale/{ko,en,ja}.json`). Names are the PokeAPI ko / en / ja-Hrkt values.
- `roster.json`: `id`, `tier` (showcase|standard), `npcWeight` (weight for appearing as an opponent, 0 means never), `playable` (can be used as the player).

```json
// species.json
{ "id": 25, "types": ["electric"],
  "baseStats": { "hp": 35, "attack": 55, "defense": 40, "spAttack": 50, "spDefense": 50, "speed": 90 },
  "sprites": { "front": "front_25_sheet", "back": "back_25_sheet", "frontShiny": "front_25_shiny_sheet", "backShiny": "back_25_shiny_sheet" },
  "animProfile": "pikachu", "nameKey": "species.025", "legendary": false }
// roster.json
[ { "id": 25, "tier": "showcase", "npcWeight": 10, "playable": true }, { "id": 9, "tier": "showcase", "npcWeight": 10 },
  { "id": 6, "tier": "standard", "npcWeight": 0 }, ... ]
```

### 6.3 Move data

- Defined here (Pokepedia has no move data). **One Normal basic move + one signature move per roster type + a secondary move for single-type species**, 12 in total.
- Each Pokémon has **the basic move + 2 typed moves = 3 moves**, assigned automatically from its types: basic move → signature moves in the species' type order → (if single-type) that type's secondary move. Examples: Pikachu = Tackle, Thunderbolt, Thunder; Charizard = Tackle, Flamethrower, Wing Attack.
- Fields: `id, type, category(physical|special), power, accuracy, nameKey, animKind(contact|projectile|effect), role(basic|signature|secondary)`. Exactly one basic move; at most one signature and one secondary per type.
- A secondary move has lower expected damage (power × accuracy) than the signature move — giving choices like "strong but may miss" (Thunder) and "weak but always hits" (Water Pledge, Confusion).
- Values are lower than in the original games to keep battles at 4–6 turns (6.5). Moves that don't fit a simple power calculation, such as fixed-damage or two-turn moves, are not used.

| id | Name (en) | Type | Category | Power | Accuracy | animKind | role |
|---|---|---|---|---|---|---|---|
| tackle | Tackle | Normal | Physical | 16 | 100 | contact | basic |
| thunderbolt | Thunderbolt | Electric | Special | 35 | 100 | projectile | signature |
| hydro_pump | Hydro Pump | Water | Special | 45 | 85 | projectile | signature |
| flamethrower | Flamethrower | Fire | Special | 35 | 95 | projectile | signature |
| wing_attack | Wing Attack | Flying | Physical | 35 | 95 | contact | signature |
| razor_leaf | Razor Leaf | Grass | Physical | 35 | 95 | projectile | signature |
| sludge_bomb | Sludge Bomb | Poison | Special | 35 | 95 | projectile | signature |
| shadow_ball | Shadow Ball | Ghost | Special | 35 | 95 | effect | signature |
| psychic | Psychic | Psychic | Special | 35 | 95 | effect | signature |
| thunder | Thunder | Electric | Special | 45 | 70 | effect | secondary |
| water_pledge | Water Pledge | Water | Special | 30 | 100 | effect | secondary |
| confusion | Confusion | Psychic | Special | 28 | 100 | effect | secondary |

(Types and categories match PokeAPI `/move/{name}`. Water Pledge's original "Pledge" combination effect is not included.)

### 6.4 Battle rules

Formulas follow modern Pokémon (stats from Gen 3 on, damage from Gen 6 on). Sources: Pokémon Showdown source (`sim/battle.ts` statModify and modify, `sim/battle-actions.ts` getDamage and modifyDamage); the type chart follows PokeAPI `type` damage_relations and the Pokémon Database chart (all 324 cells match). URLs are in the code comments.

- **Stats:** Level 50, IVs 31, EVs 0, no nature modifier. Base stats are compressed first (6.5).
  - HP = ⌊(2×base+31)×level/100⌋ + level + 10 / others = ⌊(2×base+31)×level/100⌋ + 5
  - Actual values (k=0.65): Pikachu 126 / 84 / 74 / 81 / 81 / 107, Blastoise 154 / 102 / 113 / 103 / 116 / 99 (`StatCalculatorTests`).
- **Physical/special:** The move's `category`. Physical uses Attack ÷ Defense; special uses Sp. Atk ÷ Sp. Def.
- **Turn order:** Higher Speed goes first; ties are random.
- **Action:** Accuracy check (move accuracy, no evasion or accuracy stages) → `MoveMissed` on a miss → critical check → damage.
- **Critical hit:** 1/16, ×1.5.
- **Damage:** Integer handling at each step (`DamageCalculator`; the order affects the result):
  1. Base = ⌊⌊⌊⌊2L/5+2⌋ × power × A⌋ / D⌋ / 50⌋ + 2
  2. Critical ⌊×1.5⌋ → 3. Random ⌊×R/100⌋ (R 85–100) → 4. STAB ×1.5 (4096 fixed point, 0.5 rounds down) → 5. Effectiveness ⌊×multiplier⌋ (multiplied for dual types)
  6. 0 if effectiveness is 0, otherwise at least 1. After a cheer revive, ×`PowerBoost` after effectiveness.
- **Random number consumption order** (pinned by tests): `Next(2)` only on a Speed tie → per action, `Next(100)` if accuracy < 100 → on a hit, critical `Next(16)` → damage `Next(16)` (85 + value). All via injected `IRandom`.
- With effectiveness 0, a critical is not shown as a critical. Damage events carry the HP actually lost. HP 0 means fainted.
- **Excluded:** PP, status conditions, priority, stat stages, weather.
- **NPC AI:** 5.3.

### 6.5 Balance and NPC selection

- **Battle length** about 4–6 turns. Tuned through **move power (data)** with no HP multiplier rule. All modes use the same rules and data.
- **Base stat compression:** `stat' = 80 + (base − 80) × k`, rounded half up, minimum 1, **k = 0.65**. Narrows the base stat total gap (Pikachu 320 to Mewtwo 680).
- **Demo target:** Pikachu win rate **85–93%**, average 4–6 turns (`BalanceTests`, 5,000 seeded battles, Pikachu uses only its strongest move / the NPC mixes moves as in real play). At 60–70%, too many battles are lost to a single miss. Conversely, with Blastoise as the player the win rate is about 11%.
- Why the current values: Thunderbolt accuracy 100 vs Hydro Pump accuracy 85 (a reliable move vs a strong one that can miss); Hydro Pump power 45 and Water Pledge 30 to offset the NPC mixing in weaker moves.
- **The numbers are sensitive:** shifting a guaranteed n-hit KO by one step makes the win rate jump. When tuning, pick the center of a range where the target holds for the neighborhood too, not a single point. The tuning table output code (k × power) is in `BalanceTests.cs` at commit `cd45839`.
- **Opponent selection (Collection):** Weighted draw proportional to `npcWeight` (`OpponentPicker`), excluding the player's species. Legendaries get a low weight (baseline when reopened: Mewtwo 2 / others 10). With a single opponent candidate, Run is disabled.
- Duplicate stickers do not affect stats; shiny is cosmetic only.

---

## 7. Architecture

### 7.1 Design principles

1. Separate the engine (rules) from content (data, assets, text). The engine knows only IDs and numbers.
2. Domain, Content and Application do not reference `UnityEngine` (enforced by asmdef and `Tests.Dotnet`).
3. Randomness is injected through `IRandom`.
4. Do not generalize the rules (no `IRuleSet` etc.). What gets separated is content.
5. Do not create an interface for every class. No DI framework, Repository, event bus or ECS.
6. **The engine does not know how many Pokémon are supported.**
7. Priorities: independence of battle logic, testability, separation of screen and domain responsibilities.

### 7.2 Layers

```text
Presentation (Unity)  ← title screen wiring, battle screen flow, event playback, effects, audio (no rule decisions)
       ↓
Application (C#)      ← BattleSession (flow: NPC actions, next opponent, retry, run, choice on faint), NpcBattleAi, message keys, host connection
       ↓
Domain (Pure C#)      ← BattleEngine, DamageCalculator, TypeChart, BattleState
       ↑ read-only data
Content               ← species, moves, roster, animation.json, locale, sprites
Bridge (Platform)     ← IHostBridge implementations: WebGL (jslib ↔ postMessage, title screen) / editor (logging only)
```

- Domain: input `BattleState` + actions, output a list of `BattleEvent`s. It has a party (multiple Pokémon) structure, but switching comes with the 3v3 extension.
- The `Pokepedia.Application` assembly's namespace is `Pokepedia.App` (to avoid hiding `UnityEngine.Application`).

### 7.3 Engine ↔ screen: event log and effect director

Defined in `Assets/Scripts/Domain/Battle/BattleEvents.cs`. Side 0 = player, 1 = NPC.

```csharp
PokemonSentOut(int Side, int SpeciesId, int Hp, int MaxHp)   // NPC first, then the player
TurnStarted(int Turn)
MoveUsed(int Side, string MoveId)
MoveMissed(int Side, string MoveId)
DamageDealt(int TargetSide, int Amount, int HpAfter, double Effectiveness, bool Critical)
Fainted(int Side)
BattleEnded(int WinnerSide)
Revived(int Side, int Hp, int MaxHp)        // cheer revive
TrainerThrewBall(int TargetSide)            // Handle it yourself (followed by Fainted, BattleEnded)
```

Engine API: `new BattleEngine(state, rules, random)` → `Start()` → `PlayTurn(playerAction, npcAction)` (ends on the spot when one side faints), plus `Revive(side, boost)` and `KnockOut(side)`. The only action is `UseMove(moveIndex)`; running is handled by Application.

```text
BattleEngine ← BattleSession
     │ list of BattleEvents
     ▼
BattleScreen (flow, view state) ── BattleHud (HUD, world, menus) / BattleLab (lab)
     ▼
BattleEventPlayer (messages + effect playback, VS)
     ▼
AnimationDirector ── default timelines + AnimationProfile overrides → VfxLayer, Light, Camera, Audio
```

- `BattleScreen` handles only the flow and the **view state `BattleViewState`** (`Hidden → Entering → Command → Animating → Result`, plus `ConfirmQuit`, `ChooseFaint`, `Lab`). Input handlers act only in the matching state. Per-battle data (catalogs, locale, names, session, bridge) is gathered in a single `BattleContext`.
- The host connection (waiting for `init`) and choosing the player Pokémon are handled by `TitleScreen` before the battle screen. Once there are two or more player candidates, a selection state is added before `Entering`.
- Changing effects never changes the engine. Tests assert only on event lists, and messages are built as event → locale key → string (`BattleMessages`).

### 7.4 C# mapping for Java developers

| Java | C# |
|---|---|
| `record` | `record` (C# 9+) |
| `sealed interface` + pattern matching | `abstract record` + `switch` |
| `Optional<T>` | nullable (`T?`) |
| package | namespace + Assembly Definition (.asmdef) |
| JUnit | NUnit |
| Spring Bean injection | hand-written constructor injection |
| threads/Future | coroutines, `async/await` (Unity main-thread constraint) |

### 7.5 Directory structure

The single source of truth for the actual layout and assembly dependency direction is "Directories" in [DEVELOPMENT.md](DEVELOPMENT.md). Keep asmdefs at five or fewer.

### 7.6 Source of the supported Pokémon list

The single source of truth is Unity's `roster.json`. Pokepedia keeps the playable IDs as the code constant `BATTLE_PLAYABLE_IDS` (currently `[25]`, `src/features/battle/host.ts`) with a comment naming its source. Pokepedia's server paths never call the Unity host at runtime. When the playable Pokémon change, update both places together.

### 7.7 Fonts: Galmuri11 + TMP Static

- In-game text uses **Galmuri11** (Korean, kana, JIS kanji, OFL-1.1, notice in `Assets/Content/Fonts/Galmuri-OFL.txt`). 11 Bold lacks kana, kanji and ♀♂, and 11 Condensed lacks kanji, so neither is used.
- Since the output strings are fixed, a TMP **Static** atlas (12pt, `RASTER_HINTED`, 512×512) with **only the glyphs needed** is used. Dynamic is not used because it puts the original TTF (5.4 MB) in the build.
- **When locale text gains new characters, rebake the atlas:** `Pokepedia.EditorTools.FontAtlasBuilder.Build` (locale JSON + ASCII + fixed symbols). Missing characters render as □.
- The title and VS screens (HTML) use web fonts (5.5).

### 7.8 Test strategy

- Keep **only the essential minimum**: rules and contracts whose breakage would be a real bug (damage, type chart, stats, battle flow, data validation, integration contract, assembly boundaries). Group the same rule into a few representative cases.
- Domain tests use only fake species and moves built in code (`TestMon`, `TestMove`). Only `ContentDataFilesTests` and `BalanceTests` read the real JSON.
- Deterministic tests use an `IRandom` stub.
- Screens and effects are not automated (Actions limits). Check them by eye in the lab and the browser, and play one battle on a Release build before deploying.

---

## 8. Art and assets

### 8.1 Policy

- Pokémon pixel art is **PokeAPI sprites**; the title and VS screens use official artwork from Pokepedia Storage. Rights are handled at the same level as Pokepedia (unofficial, non-commercial fan project notice, `LICENSE`). No other original assets are used.
- **Sprites are not committed to the repository:** `Assets/Resources/Sprites/` is in `.gitignore`. Only the scripts in `Tools/sprites` and a **manifest pinned to a PokeAPI sprites commit SHA** are committed; after cloning, the script reproduces them (DEVELOPMENT.md).
- No trainer art is used. PokeAPI does not provide trainer art (the sprites repository has only badges, items, pokemon and types).

### 8.2 Sprites: Gen 5 Black/White animated

- Source: PokeAPI sprites `versions/generation-v/black-white/animated/` (front, `back/`, `shiny/`, `back/shiny/`).
- `Tools/sprites` splits the GIFs into frames → sheet PNGs (`--dedupe --indexed`) + frame-delay JSON. At runtime they are loaded by name with `Resources.Load` and played by `SpriteAnimator` (Animator clips are not used because frame delays are uneven and there are many frames). Import settings (Point, uncompressed, no mipmaps) are enforced by `SpriteSheetImporter`.
- The manifest contains only Pikachu and Blastoise. To use the four Standard Pokémon again, run `fetch_sprites.py --update-manifest --ids 25,9,6,3,94,150` and then `build_sprites.py` (same PokeAPI commit).
- Pivot at the feet, integer scaling, Point filtering. Comparison with other styles: [`sprite-style-compare.html`](sprite-style-compare.html).

### 8.3 Sound

- **Every sound is synthesized directly in this repository's code** (no external, AI-generated or original game audio). The list and recipes are in `Assets/Content/Audio/manifest.md`.
- **Type attack sounds:** The sound when a move is used (Electric = lightning strike, Water = water cannon, etc., seeded). Not Pokémon cries. Alternatives are kept in `TypeAttackSounds.Alternates`.
- **Move-specific attack sounds:** So two moves of the same type don't share a sound, `moveSfx` in `animation.json` assigns a sound per move. Available sounds are the dedicated ones (`TypeAttackSounds.MoveOnlyIds`) and the kept alternatives (`AlternateIds`); an unknown id stops the bake (`AudioBaker`).
  - Tackle: "whoosh → thwack/thud → dust" (impact matched to 0.09 s into the lunge).
  - Water Pledge: "splash" ×4 + a big "splaaash" (once per water column eruption, exactly following the effect timeline). The water sound is built from bubble resonance (a short upward-gliding sine) + sloshing band-limited noise, ramping up along a curve over 45 ms (noise that bursts and decays within about 10 ms, or a low thud, sounds like a gunshot).
  - Hydro Pump (water cannon), Thunderbolt and Thunder (both lightning strikes) use the type attack sound.
- **No cries (entry sounds).** The original PokeAPI cries did not fit the visuals.
- **Battle BGM:** One self-composed 8-bit loop (`BattleMusic`). Starts from the top each battle; on win/loss it fades down and a jingle plays. WebGL converts audio to AAC and touches the beginning, which can make the loop seam pop, so 2048 samples of silence are prepended + a WAV smpl loop region is set.
- **Synthesizing at runtime freezes the screen in WebGL,** so the editor bakes ahead of time (`AudioBaker` → `Assets/Resources/Audio/`, not committed). Short sounds are padded to at least 0.25 s because of an iOS decoding issue.
- **Timing is key:** Each attack sound is started a per-sound amount before the release moment (`ReleaseSec`) so the burst lines up. If the wind-up is long, the gap before it is filled with a charge-up sound.
- Browsers block audio before user input, so the first sound plays inside the start button handler (5.5).

---

## 9. Pokepedia (React) integration

### 9.1 Approach: iframe + postMessage

- Embedded as an iframe in the page **`/[locale]/battle`**. No cookies or auth are used, so `postMessage` is enough even across domains. Origin checks are mandatory.

### 9.2 Message contract (v1)

This section is the **single source of truth** for the Pokepedia ↔ Unity contract.

#### Layout

```text
Pokepedia page (/[locale]/battle, BattleFrame)
  └─ <iframe src="https://battle.pokepedia.dev/?host=pokepedia&lang=ko" allow="autoplay">
        Unity Web build (index.html template + HostBridge.jslib + HostBridge.cs)
```

- **One-way:** React passes the owned list once and Unity does not store results.
- The authenticated React side (server/RLS) reads the collection. **Unity computes owned ∩ supported** (React sends everything owned — the contract does not change as the roster grows).
- The passed owned list could be tampered with, but it is not used for rewards or rankings, so it is not validated.

#### Mode detection

| iframe URL | Behavior |
|---|---|
| No `?host=pokepedia` | **Demo** (Pikachu vs Blastoise) right after start |
| `?host=pokepedia` present | After start, send `ready` and wait **5 seconds** for `init` |

- If no `init` arrives within 5 seconds: "Could not connect." + [Retry] ([resend `ready`]) / [Try Demo]. **It never switches to Demo automatically.**
- Being inside an iframe is not used for detection (other sites can embed it too). The query is read from `location.search` via jslib.
- `lang` (`ko`/`en`/`ja`) is the language of the loading and title screens. Without it, the browser language (Korean if unknown). After connecting, the in-game language is `init.locale`.

| Mode | Player | Opponent |
|---|---|---|
| **Demo** | Pikachu, fixed | Blastoise, fixed |
| **Collection** | owned ∩ `playable` (only Pikachu today) | `npcWeight` draw (only Blastoise today) |

- If owned ∩ playable is **one, go straight to VS with no selection**; if **zero**, show "Win a Pikachu sticker in the quiz to battle with your own Pokémon" + [Try Demo]. A selection screen for two or more will be built when player Pokémon are added.
- If the owned Pikachu is a shiny sticker, the battle also uses shiny pixel art (cosmetic only).
- The engine is the same; only the Pokémon selection differs.

#### Messages

Every message is an object with a `type` and a protocol version `v`. There are no request IDs.

| Direction | Message | Meaning |
|---|---|---|
| Unity → React | `{ "type": "ready", "v": 1 }` | Loading and start are done. **React sends `init` only after receiving this.** Sent again if Unity reloads |
| React → Unity | `{ "type": "init", "v": 1, "locale": "ko", "owned": [ { "id": 25, "shiny": false } ] }` | `locale` ko/en/ja (anything else is ko), `owned` is every sticker. Ignored if `v` is not 1 |
| Unity → React | `{ "type": "battleEnded", "v": 1, "result": "win" }` | Each time a Collection battle is decided (`win`/`lose`; not sent on Run). Display only, not stored |

#### Security

| Where | Rule |
|---|---|
| Template `message` listener | Forwards `init` only when `event.origin` is in the allow list (`window.pokeHost.allowedOrigins`) |
| Unity → React (jslib) | Never uses `'*'` as `targetOrigin`; sends to each origin in the allow list |
| React | Handles `ready` only when `event.origin` is the Unity host (`BATTLE_ORIGIN`) |
| Unity host headers | `frame-ancestors` allows only Pokepedia, the portfolio hub, Vercel Preview and `localhost:3000` |

- Allow list (template): `https://pokepedia.dev`, `https://www.pokepedia.dev`, `http://localhost:3000` (Pokepedia dev server), `http://localhost:8000` (local mock page), and **this account's Pokepedia Preview regex** `^https://pokepedia-[a-z0-9-]+-vison901-gmailcoms-projects\.vercel\.app$` (the URL changes per PR). `frame-ancestors` only supports a one-level wildcard, so it contains `https://*.vercel.app` — other Vercel sites can embed it, but it is a public game so that has no impact, and `init` is accepted only from the regex above.
- Pokepedia has no CSP header, so it does not block the iframe. A dev server on another port (e.g. 3100) is not in `frame-ancestors`, so the game will not load there.

#### Input and size

- Inside the iframe, the default behavior of arrow keys and Space (scrolling the parent) is blocked, and the canvas gets focus on click or touch. The iframe has `allow="autoplay"`.
- **Pokepedia decides the size** (`BattleFrame`, `fitBattleScreen`): on desktop, the iframe is fitted inside the red Pokédex device frame at an **integer scale** matching the display scale (DPR) (at most 720 CSS px, within the window height). Scaling larger makes the text and dithering of the 240×216 image look broken. On phones it fills edge to edge. Switching only Pikachu and Blastoise to high resolution would mix pixel sizes, so looking large and smooth requires raising the game resolution (ROADMAP "Parked").

#### Local testing

```bash
python -I Tools/web/serve.py --dir Builds/Web --port 8001 --ancestors http://localhost:8000
python -I Tools/web/serve.py --dir Tools/web/host-test --port 8000
```

On the mock Pokepedia page at `http://localhost:8000/`, check the `ready` → `init` round trip, the timeout (don't send init), scroll interference, entry per ownership case (Pikachu / shiny / none), and receiving `battleEnded`.

### 9.3 DB policy

**The Pokepedia DB is not changed.** No new tables, RPCs or columns. Read only. If it ever becomes necessary, additive changes only (Prod and Preview share the DB, and migrations are applied automatically).

### 9.4 Pokepedia-side implementation (in that repository)

- Navigation "Battle", page `/[locale]/battle` (`BattleFrame`: message handling and size calculation), `src/features/battle/host.ts` (`BATTLE_ORIGIN`, `BATTLE_PLAYABLE_IDS`, `initFor`, `fitBattleScreen`).
- Quiz and collection integration: 9.6, 9.7.
- Text goes in all three of `messages/ko·en·ja.json` (there is a key-match test). For structural changes, update that repository's `docs/system_design.md` first and follow the merge policy in that repository's `CLAUDE.md`.

### 9.5 Tampering

Since the rules run on the client, results cannot be verified, but **they are not used for rewards or rankings**, so tampering only affects the player's own experience. No server-side validation. Revisit this if rewards or official rankings are added.

### 9.6 Battle-ready indicator in the collection

- The card of a battle-ready Pokémon (Pikachu) shows a **gold border + "Battle" badge** and serves as an entrance to the battle. It is distinct from the yellow (`volt`) border of shiny stickers.
- There is no `All | Battle-ready` filter (ROADMAP "Parked").

### 9.7 First-quiz Pikachu

- For **users without a Pikachu sticker**, **only the first question** of a new run is a Pikachu silhouette. The server (`startOrResume`) decides by reading the existing `user_sticker` — no DB change, cannot be tampered with, and consistent across devices.
- Why Pikachu: it matches the Demo (Pikachu vs Blastoise) as is, so the matchup, balance and effects need no changes. Since it is only the first question and only for users who don't own it, there are almost no side effects such as repeated or skewed quizzes.
- Answering correctly shows **[Battle with my Pikachu]** on the sticker reward screen (a lone gold button for a first Pikachu). The quiz in progress is kept and resumes on return.

---

## 10. Web deployment infrastructure

### 10.1 Overview

```text
Browser
 ├─ Pokepedia (Vercel) ── iframe ──▶ Unity static hosting (Cloudflare Pages `pokepedia-battle`, battle.pokepedia.dev)
 └─ Supabase ◀── accessed only by React. Unity never accesses it (except the title/VS screens reading Storage artwork via public URLs)
```

- No new server, DB or game server. Battle state lives only in the browser tab's memory (a reload starts over).

### 10.2 Hosting: Cloudflare Pages

- `pokepedia.dev` DNS is managed by Vercel (nameservers `ns1/ns2.vercel-dns.com`, registrar Name.com). Workers custom domains require the DNS to be on Cloudflare, but **Pages attaches with a single CNAME on external DNS** — Pages is used to avoid moving the production DNS. Static requests are free and unlimited, **25 MiB per file**, `_headers` supported.
- **Caution:** In wrangler 4.x, `wrangler pages project create` creates a Worker by default. To create a Pages project, pass `--force` the first time. Once a Pages project exists, later `pages deploy` calls go to Pages.

### 10.3 Hosting configuration

- `Tools/web/_headers`: for `Build/*.wasm.br`, `*.js.br`, `*.data.br`, `Content-Encoding: br` and the Content-Type of the inner extension (`.wasm` is `application/wasm` — streaming compilation). Cloudflare does not recompress (`Content-Length` equals the original `.br`).
- `Build/*` has hashed file names, so `Cache-Control: public, max-age=31536000, immutable`; everything else uses the Pages default (revalidate every time). Pages joins values with commas when the same header matches several rules, so **each header is set by only one rule**. `frame-ancestors` is set once on `/*`.
- The local `serve.py` mimics the same rules (`--mbps 9 --latency-ms 60` approximates Fast 4G).
- **Size:** about 13 MB transferred (code `wasm.br` about 7.7 MB + data `data.br` about 5.2 MB). It is fine on the user's 4G phone, so it is not optimized. Pixel sheets have many flat-color areas and compress well, so removing art barely reduces transfer size (removing the four Standard Pokémon's art saves only 0.4 MB).
- For up to about a minute after a deploy, old files may still be served (propagation). Re-run hash comparisons after a short wait.

### 10.4 Domain and environments

- **`battle.pokepedia.dev`** → CNAME `pokepedia-battle.pages.dev` (a record in Vercel DNS, takes precedence over the root and wildcard ALIAS). CAA allows Google and Let's Encrypt.
- Setup order (reversed gives a 522): ① Cloudflare dashboard Pages `pokepedia-battle` → Custom domains → `battle.pokepedia.dev` → **My DNS provider**. ② `npx vercel dns add pokepedia.dev battle CNAME pokepedia-battle.pages.dev` (the Vercel MCP returns 403 when reading this domain's records, so use the CLI). ③ Cloudflare verifies and issues the certificate.
- Pokepedia's iframe URL is the code constant `BATTLE_ORIGIN` (`src/features/battle/host.ts`).

### 10.5 Build, deploy, CI

- **Local Unity Release build → `python -I Tools/web/deploy.py`**: copies `Builds/Web` to `Builds/deploy`, adds `_headers`, then runs `wrangler pages deploy` (a preview if `--branch` is not main). After deploying, it verifies by comparing the sha256 of `Build/*.br` with the live responses.
- Unity build and tests are not in CI (license, time). **CI runs only the pure C# tests** (`dotnet test Tests.Dotnet`, on PRs only, path filters) — rules are in the CI section of [CLAUDE.md](../CLAUDE.md).
- Build output (`Builds/`) is not committed. Force-killing a batchmode build leaves `bee_backend` running, making the next build fail, and leaves temporary build values (`preloadedAssets`) in `ProjectSettings.asset` — kill the process and revert the file.

### 10.6 Monitoring, security, cost

Cloudflare Web Analytics is enough. There are no secrets; embedding is restricted by the `init` origin check and `frame-ancestors`. Cost is effectively zero.

### 10.7 When a server becomes necessary

When adding victory rewards, official rankings, cloud saves or online PvP. Even then, the default is to extend the existing Pokepedia backend rather than build a new game server.

### 10.8 Pokepedia hosting

Unity being on Cloudflare is no reason to move Pokepedia (separate origin + postMessage). Out of scope for this project.

---

## 11. Repositories

```text
ottuck/pokepedia          # Pokepedia (Next.js). Only the minimum changes needed for integration, via that repository's PRs and policies
ottuck/pokepedia-unity    # this repository
```

- Why separate: structure, tooling, CI and deployment are completely different, and merging them would let Unity changes affect Pokepedia's CI.
- The only touch points are the iframe URL, the `postMessage` contract (9.2) and the playable IDs (7.6). This document is the single source of truth for the contract.

---

## 12. Development guidelines

This project is built while understanding C# and Unity. Agent work rules (branches, PRs, CI, editor locking) are in [CLAUDE.md](../CLAUDE.md).

1. Briefly note the reason for major technology choices. Compare new C#/Unity concepts with Java (7.4).
2. Keep the Unity lifecycle separate from pure C# domain logic. Write **tests first** for rules.
3. Do not add unnecessary frameworks, infrastructure or abstractions. Always keep the web build viable.
4. **Do not modify Pokepedia code or DB casually.** Before changing anything, check the relevant source, `docs/system_design.md` and that repository's `CLAUDE.md`.
5. Do not put Pokémon names, sprites or strings in engine code. Do not write per-Pokémon code. **The engine does not know the roster size.**
6. Showcase also uses the real engine. Never play back fake battles or prebuilt effects only.
7. **Do not fill in facts by guessing.** Verify Pokepedia, PokeAPI and Unity through code, docs and APIs, and do not take technology names, rules or licenses from external suggestions (GPT, etc.) at face value. Check facts such as versions, limits and prices on the web, and record the source along with anything unverified.
8. The user has limited Pokémon domain knowledge, so explain rules and terms first, then present a recommendation with reasons before deciding.
9. Record the source and license of sounds and assets in the manifest.
10. Scope-expanding work (ROADMAP "Parked": releasing new Pokémon, 3v3, cries, resolution, etc.) is the user's decision. When bringing it up, present a recommendation with reasons.

### Unity CLI and agent integration

Source: [Unity CLI as the replacement for the in-Editor MCP server](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli).

- The in-Editor AI Assistant's MCP server is deprecated and replaced by the Unity CLI's `unity mcp`. When a shell is available, **calling `unity command` and `unity eval` directly instead of MCP** is faster and uses fewer tokens (official guidance) → the default for this repository.
- Headless builds and tests also work without the editor (`unity test . --mode EditMode`; batch builds in DEVELOPMENT.md). Local CLI `1.0.0-beta.13`. `unity pipeline install` has not been run (it adds packages, so verify it in a PR).
- Unverified: the `winget install Unity.CLI` package name, the agent names for `unity skill install`, and whether `unity command` and `unity eval` require a running editor.
- `unity eval` runs arbitrary C# in the editor. Put only the needed commands in the permission allow list. Single-editor locking and parallel work rules are in [CLAUDE.md](../CLAUDE.md).
