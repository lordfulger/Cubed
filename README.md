# Cubed

A 3D arena shooter built around card picks. You fight in rounds, and after each round the losers choose a
card that changes their stats or adds an ability. The game supports teams, bots, local split-screen, a
built-in map editor, damaging borders, and fall damage that blocking negates. It is built in Unity
(Mono backend) so it can be modded with **BepInEx 5** and installed through **r2modman / Thunderstore**.

Everything (players, maps, UI, sounds) is generated from code. The project has no prefabs or art assets,
so you can open it and press Play.

---

## Features

| Area | What's in |
|---|---|
| **Rounds & cards** | First team to N round wins takes the match. After each round a pick phase deals each picker a hand of cards. **407 cards**: 87 hand-made cards (a base set plus an expansion) and 320 generated mix-and-match cards (pure upgrades, double upgrades, trade-offs with drawbacks, effect cards and high-risk gambles). They adjust 29 stats or attach 46 effects: reflect, shockwaves, walls, blink, poison, frost, echo/burst fire, turrets, mines, orbiting shields, auras, executes, stuns, wall-piercing, boomerang and homing bounces, barriers, last stands and more. |
| **Card options** | Cards per hand (1-10), picks per round (0-10), picks before round 1 (0-10), pick timer (off/5-120 s, picks the highlighted card when time runs out), who picks (losers / everyone / winners / nobody), new hand per pick or keep the same hand, and a Card Library (filter by hand-made, generated or rarity) to enable or disable individual cards. |
| **Teams** | Up to 8 players on up to 8 teams (from 4v4 to an 8-player free-for-all), plus friendly fire on/off. Scoring is per team, spawns are per team, and eliminated players spectate their teammates. |
| **Game modes** | Elimination, Deathmatch (respawns, first team to N kills), King of the Hill (hold the zone), Card Rush (every kill draws a card), and **Training**: a guided tutorial plus a practice range with target dummies and an on-demand card picker. |
| **Profile** | Lifetime stats, favorite cards, 20 achievements with pop-up notifications, and 12 unlockable cube hats that other players see online. |
| **Bots** | 4 difficulties, plus target dummies. Bots path-find across each map (step-ups, drops, gap jumps and bounce pads) and lead their shots (accounting for bullet drop), strafe, avoid ledges and lava, jump gaps, block incoming bullets, and block before they hit borders or land hard. They also pick cards. You can watch a bots-only match. |
| **Local multiplayer** | Keyboard+mouse plus up to 8 gamepads. Split-screen handles 1-8 humans in 2x2, 3x2 or 4x2 grids. In Match Setup, press **Start** on a gamepad to join. |
| **Online multiplayer** | Host a lobby, and friends join by IP. Games on your local network appear automatically. An optional Steam build adds friend invites and relay networking, so nobody needs to forward ports. Hosts can mix online players with split-screen players and bots on any team. The host runs the match. Your own movement is predicted locally so it responds instantly. Remote shots get latency compensation. Dropped players reconnect into their own fighter, a bot fills in while they're gone, and new players can join mid-match from the next round. Mod mismatches show a warning. |
| **Controls** | Xbox, PlayStation and Switch controller layouts are detected per OS. Controllers hot-plug, and if a player's controller disconnects mid-match the game auto-pauses. Keyboard and gamepad bindings are fully rebindable. Gamepads work in every menu. Rumble works when Unity's Input System package is installed. |
| **Options & accessibility** | Resolution and window mode, vsync and FPS cap, quality presets, FOV, UI scale, screen-shake and flash reduction, colorblind-safe and high-contrast team palettes, damage number toggle, and separate music and sound volume. |
| **Blocking** | Blocking raises a full 360-degree bubble. Bullets touching it from any side are stopped, along with explosions, border hits and fall damage. |
| **Presentation** | Textured voxel blocks, a gradient sky with distant floating cubes, drifting particles, glowing bullets, muzzle and explosion light flashes, grid borders that glow as you get close, and animated lava and bouncers. Players are animated: walk cycle with stepping feet, lean, squash and stretch, recoil, reload spin, blinking, spawn and shield pops. The camera adds screen shake and a FOV kick, and the HUD has hit markers, a damage and low-HP vignette, a heartbeat, dealt card animations and a sliding kill feed. Sound is positional and synthesized, and there is original procedural music (battle and menu loops). |
| **Damaging borders** | The arena is a box. Touching a side or the ceiling deals damage and bounces you back in. Optionally the borders close in after X seconds. |
| **Fall damage** | Falling into the bottom of the arena deals damage and launches you back up. Landing hard on blocks also hurts. **Blocking at the moment of impact negates it** (and still triggers on-block card effects). |
| **Maps** | 14 built-in arenas (towers, lanes, ice lakes, a crate maze, a lava castle, island rings, an 8-player plaza and a duel box). `Tools/validate_maps.py` checks spawns and bounds. |
| **Map editor** | Fly camera, paint blocks on a grid, 9 block types (stone, bouncer, lava, ice, breakable glass/crate...), team spawns, X/Z mirroring for symmetric maps, arena resize, undo/redo, save/load, and one-click **Test Play** against bots. |
| **Modding** | BepInEx plugins get a public C# API (`CubedAPI`, `GameEvents`, `CardEffect` hooks) and work with Harmony patches. You can also make mods with no code: drop `*.cubedcards.json`, `*.cubedmap.json` or `*.cubedblocks.json` into any BepInEx plugin folder. |

## Getting started

1. Install **Unity 2022.3 LTS** (any 2022.3.x works; newer versions should work too).
2. Open this folder as a project in Unity Hub.
3. On first load the `Cubed > Setup Project` step runs automatically. It creates `Assets/Cubed/Scenes/Main.unity`,
   adds it to Build Settings, makes sure the gamepad input axes exist, and sets the build to
   **Mono + no managed stripping**, which BepInEx needs.
4. Press **Play** (in any scene). The game builds itself at startup.
5. To make a release build, use **Cubed > Build > Windows / Linux / macOS**. Output goes to `Builds/<platform>/`.

> Unity will generate `.meta` files and other `ProjectSettings` the first time you open the project. Commit them.

## Controls

| Action | Keyboard + mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Look / aim | Mouse | Right stick |
| Shoot | Left mouse | RT or RB |
| **Block** | Right mouse, Left Shift or F | LT, LB or B |
| Jump / air jump | Space | A |
| Reload | R | X |
| Pick card | Click, or A/D + Space | Stick or LB/RB, then A |
| Pause | Esc | Start |
| Menus | Mouse, or arrows + Enter / Esc | Stick or d-pad, A, B |
| Join Match Setup | - | Start |

Map editor: hold **right mouse** and use WASD/QE to fly (Shift to go fast). Left click places blocks (drag to
paint). The mouse wheel or `[` `]` changes the build layer. `1-9` pick a brush, `X` picks the eraser, `F`
frames the map, `Ctrl+Z`/`Ctrl+Y` undo/redo, `Ctrl+S` saves.

## How a match works

1. **Match Setup** lets you add players (keyboard, gamepads, bots), assign teams, and set rules.
2. Optionally everyone picks starting cards.
3. Each round has a 3-2-1 countdown, then the last team standing wins the round.
4. The pick phase deals cards to the players allowed to pick (by default, the losing teams).
5. The map is rebuilt between rounds, which restores breakable blocks, and can rotate to the next map or a random one.
6. The first team to reach the target round wins.

## Online play

1. **Main Menu > Online**. Enter your name and pick your input device.
2. **Host:** press **Host Lobby** (default port 27015). You land in Match Setup with a lobby panel showing your
   IP. Joining players appear as "Online" slots you can move between teams. Press **Start Match**
   when everyone's in.
3. **Join:** pick a game under *Games on your network*, or type the host's IP and port. In the lobby you can
   change your team. The host picks the rules and map, including custom editor maps, which are sent over automatically.
4. Over the internet, the host forwards **TCP port 27015** on their router. LAN discovery uses UDP 27016.

The host is authoritative. It runs the physics, bots and card picks and sends snapshots 30 times a second.
Clients send their input and card choices. Aiming is local, so it feels instant. Movement shows up after one
round trip, which works well on LAN and is fine on a decent internet connection. Every machine needs the same
game version. Card mods only need to be on the host, but clients without them see placeholder cards.

## Project layout

```
Assets/Cubed/Scripts/      Game code (assembly: Cubed.dll; this is what mods reference)
  Core/        GameManager (match flow), MatchSettings, GameEvents, visuals, synthesized audio
  Players/     Player (movement, block, damage, borders), input, camera, BotBrain
  Combat/      Weapon, Bullet, Explosion
  Cards/       Stats, CardDefinition, CardRegistry, built-in cards & effects
  Maps/        MapData (JSON format), BlockTypes, MapRuntime, BuiltInMaps, MapEditor
  UI/          IMGUI toolkit, menus, HUD and card pick screens
  Modding/     CubedAPI, ContentLoader (JSON mods)
  Net/         TCP transport + LAN discovery, protocol, NetSession (host/client)
Assets/Cubed/Editor/       Project setup + build menu
Modding/ExampleCardMod/    Example BepInEx plugin (C#, Harmony) + Thunderstore metadata
Modding/ExampleDataPack/   Example no-code mod (JSON cards, blocks, map)
Tools/                     Thunderstore packager, InputManager generator
docs/                      MODDING.md, R2MODMAN.md
```

## Modding

* **[docs/MODDING.md](docs/MODDING.md)** covers the C# API, card effects, events, JSON formats and Harmony.
* **[docs/R2MODMAN.md](docs/R2MODMAN.md)** covers installing BepInEx, packaging for Thunderstore, and getting
  Cubed listed in r2modman.

## Release & ops

* `docs/RELEASE.md`: release checklist covering builds, signing and notarization, Steam, Thunderstore, and legal.
* `docs/STEAM.md`: how to enable the optional Steam build. Set `CUBED_STEAM`, add Facepunch.Steamworks, and set the app id in `SteamIntegration.AppId`.
* `docs/PRIVACY.md`: a privacy-policy **template**.
* `.github/workflows/build.yml`: CI builds through GameCI. This needs Unity license secrets.
* Logs and crash reports are written to `<persistentDataPath>/logs`. In-game: Mods > Open logs. Crash reports are only uploaded if `CrashLog.UploadUrl` is set.
* `Tools/make_bepinex_pack.py` builds a `BepInExPack_Cubed` Thunderstore package from the official BepInEx zip.

## Not done yet

* **Nothing has run in Unity yet.** Expect a round of first-run fixes and tuning.
* **Art and audio** are procedural placeholders, so a real visual identity needs an artist and a sound designer.
* **Card balance** needs playtesting.
* **Things only the owner can do:**
  * a trademark search
  * Steam and Thunderstore accounts
  * a Unity license for CI
  * code-signing certificates

  See `docs/RELEASE.md`.
* **Menus use Unity's IMGUI.** They work fine, but a production game would likely rebuild them in UI Toolkit/uGUI.
