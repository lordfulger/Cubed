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
| **Rounds & cards** | First team to N round wins takes the match. After each round a pick phase deals each picker a hand of cards. 42 built-in cards adjust 29 stats or attach effects (reflect, shockwaves, walls, blink, poison, frost, chain zaps, splitting bullets...). |
| **Card options** | Cards per hand (1-10), picks per round (0-5), picks before round 1, pick timer (off/5-120 s, picks the highlighted card when time runs out), who picks (losers / everyone / winners / nobody), new hand per pick or keep the same hand, and a Card Library to enable or disable individual cards. |
| **Teams** | Up to 8 players on 4 teams, plus friendly fire on/off. Scoring is per team, spawns are per team, and eliminated players spectate their teammates. |
| **Bots** | 4 difficulties. Bots lead their shots (accounting for bullet drop), strafe, avoid ledges and lava, jump gaps, block incoming bullets, and block before they hit borders or land hard. They also pick cards. You can watch a bots-only match. |
| **Local multiplayer** | Keyboard+mouse plus up to 4 gamepads. Split-screen handles 1-4 humans. |
| **Damaging borders** | The arena is a box. Touching a side or the ceiling deals damage and bounces you back in. Optionally the borders close in after X seconds. |
| **Fall damage** | Falling into the bottom of the arena deals damage and launches you back up. Landing hard on blocks also hurts. **Blocking at the moment of impact negates it** (and still triggers on-block card effects). |
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

## Not done yet

* Online multiplayer. Multiplayer is local only (split-screen). `IPlayerInput` is the place to plug
  in networked input.
* Menus need a mouse. Gamepads work in matches and in card picks.
* There are no art or audio assets: visuals are flat-shaded cubes and sounds are synthesized.
