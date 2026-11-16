# Modding Spheres

Spheres supports two kinds of mods. Both can be distributed as Thunderstore packages and installed with
r2modman (see [R2MODMAN.md](R2MODMAN.md)).

| | Data mods (JSON) | Code mods (BepInEx plugins) |
|---|---|---|
| Needs BepInEx | No (but works inside BepInEx plugin folders) | Yes, BepInEx 5 |
| Can add | Cards (stat modifiers + existing effects), block types, maps | Anything: new effects, game rules, UI, Harmony patches |
| Example | `Modding/ExampleDataPack` | `Modding/ExampleCardMod` |

All game code is in **`Spheres.dll`** (`<Game>_Data/Managed/Spheres.dll`) under the `Spheres` namespace. Classes and
members are public and non-sealed, so you can call them directly or patch them with Harmony.

---

## 1. Data mods

On startup, and again when you press **Mods > Reload content**, Spheres scans these folders recursively:

1. `StreamingAssets/Content` (content shipped with the game)
2. `<persistentDataPath>/Content`. The Mods screen shows the exact path and has an **Open content folder** button.
3. **The BepInEx plugin folder** (`BepInEx/plugins`, including r2modman profiles; it is found through `BepInEx.Paths.PluginPath`)
4. Any folder a code mod passes to `SpheresAPI.LoadContentFolder(path)`

| File suffix | Contains |
|---|---|
| `*.spherescards.json` | A card pack |
| `*.spheresblocks.json` | Block types |
| `*.spheresmap.json` | One map (the same format the map editor saves) |

Load errors are shown on the in-game Mods screen and written to the Unity/BepInEx log.

### Card pack

```json
{
  "author": "YourName",
  "cards": [
    {
      "id": "yourname.big_bouncy",
      "title": "Big Bouncy",
      "description": "Huge bullets that bounce twice.",
      "rarity": "Uncommon",
      "color": "#ffaa33",
      "maxStacks": 3,
      "weight": 1.0,
      "modifiers": [
        { "stat": "BulletSize", "op": "Multiply", "value": 2.5 },
        { "stat": "Bounces",    "op": "Add",      "value": 2 },
        { "stat": "Spread",     "op": "Set",      "value": 0 }
      ],
      "effects": [ "frost_tips" ],
      "tags": [ "bullets" ]
    }
  ]
}
```

* `rarity` is `Common` (weight 10), `Uncommon` (5), `Rare` (2) or `Legendary` (0.7). `weight` multiplies the rarity weight.
* `maxStacks: 0` means a player can take the card any number of times.
* Modifiers combine as `value = Set ?? (base + ΣAdd) × ΠMultiply`, then the result is clamped. The order you pick cards in doesn't matter.
* A card that reuses an existing `id` replaces that card. Use this to rebalance built-in `spheres.*` cards.

**Stats** (base value in parentheses):

`MaxHealth` (100), `MoveSpeed` (8), `JumpHeight` (2.3), `ExtraJumps` (0), `AirControl` (0.6),
`Damage` (25), `AttackCooldown` (0.32 s), `Ammo` (3), `ReloadTime` (1.6 s), `BulletSpeed` (45),
`BulletGravity` (6), `BulletsPerShot` (1), `Spread` (2°), `Bounces` (0), `BulletSize` (0.12),
`BulletLifetime` (4 s), `Knockback` (6), `Lifesteal` (0, fraction of damage dealt), `BlockCooldown` (4 s),
`BlockDuration` (0.35 s), `Regen` (0 HP/s), `Homing` (0), `ExplosionRadius` (0), `Pierce` (0),
`ExtraLives` (0), `PlayerSize` (1), `DamageTaken` (1), `HazardDamageTaken` (1, which covers border, fall and lava damage),
`HandSize` (0, extra cards shown in this player's hands).

**Built-in effect ids**:

* Base set: `pulse_guard`, `mirror_guard`, `cube_wall`, `phase_step`, `frost_tips`, `venom`, `arc_coil`,
  `rebound_boost`, `splitter`, `adrenaline`, `ammo_refund`, `thorns`, `overcharge`
* Expansion: `echo_1`, `echo_2`, `recoil_jets`, `full_clip`, `riposte`, `growth_rounds`, `fuse_shells`,
  `gravity_slug`, `chain_bounce`, `ghost_rounds`, `boomerang`, `stun_tips`, `hush_rounds`, `mending_guard`,
  `barrier_cell`, `guard_burst`, `sentry_drop`, `mine_layer`, `orbit_cubes`, `last_stand`, `second_guard`,
  `trophy`, `executioner`, `giant_slayer`, `backstab`, `close_quarters`, `long_shot`, `bloodletter`,
  `momentum`, `steady_hands`, `frenzy`, `decay_field`, `field_medic`

Code mods can register more.

The 320 generated cards (`spheres.gen.000`-`spheres.gen.319`, tagged `generated`) are built deterministically by `CardGenerator`. Every machine gets the same ids, which online play needs. Patch `CardGenerator.Generate` or call `CardRegistry.Unregister` to change the pool.

### Block types

```json
{
  "blocks": [
    { "id": "yourname.tar", "name": "Tar", "color": "#222222", "groundAccel": 0.3, "damagePerSecond": 5 },
    { "id": "yourname.mega_spring", "name": "Mega Spring", "color": "#ffee00", "bounceSpeed": 40 },
    { "id": "yourname.paper", "name": "Paper Wall", "color": "#ffffffaa", "destructible": true, "health": 10, "transparent": true }
  ]
}
```

New block types show up in the map editor palette.

### Maps

Maps are boxes of size `sizeX × sizeY × sizeZ`. The faces of the box are the damaging borders, and the bottom
face (y = 0) is the fall line. Blocks are axis-aligned boxes in integer cells. Spawns stand on the bottom of their
cell. `team: -1` means any team can use the spawn. If a team has no spawns, Spheres generates spawn points on solid ground.

```json
{
  "formatVersion": 1, "name": "Tiny", "author": "you", "description": "",
  "sizeX": 30, "sizeY": 28, "sizeZ": 30, "background": "#101420",
  "blocks": [ { "x": 8, "y": 7, "z": 8, "w": 14, "h": 1, "d": 14, "type": "solid" } ],
  "spawns": [ { "x": 10, "y": 8, "z": 15, "team": 0 }, { "x": 19, "y": 8, "z": 15, "team": 1 } ]
}
```

#### Moving parts (`mechs`)

Moving parts are driven by the round clock, so they line up on every machine online without extra traffic.

| `kind` | What it does | Main fields |
|---|---|---|
| `door` | Slides by `dx/dy/dz` cells, holds open, slides back | `w h d`, `dx dy dz`, `period` |
| `platform` | Smooth back-and-forth; riders are carried | `w h d`, `dx dy dz`, `period` |
| `elevator` | Like a platform but rests longer at each end | `w h d`, `dy`, `period` |
| `piston` | Slams out fast, retracts slowly (`deadly` hurts only while slamming) | `w h d`, `dx dy dz`, `period` |
| `spinner` | Rotating bar around its center | `w h d`, `axis` (0 X, 1 Y, 2 Z), `period` (negative = reverse) |
| `pendulum` | Spiked ball hanging from cell `x y z` | `w` (ball size), `length`, `angle`, `axis` |
| `saw` | Spinning blade that travels along `dx/dy/dz` | `w`/`d` (diameter), `axis`, `dx dy dz` |
| `spikes` | Floor trap on top of cell `y`: warns (red glow), stabs, retracts | `w d` |

All kinds take `phase` (0-1, to stagger neighbours), `deadly`, `damage` and `type` (block type used for the look).
Deadly parts deal `DamageType.Trap`, which blocking negates.

```json
"mechs": [
  { "kind": "pendulum", "x": 20, "y": 23, "z": 11, "w": 2, "length": 14, "angle": 55, "axis": 0, "period": 3.4, "deadly": true, "damage": 35 },
  { "kind": "door", "x": 21, "y": 8, "z": 10, "w": 2, "h": 6, "d": 4, "dy": 5.5, "period": 8 }
]
```

#### Physics props (`props`)

Loose objects the host simulates and streams to clients. `x z` is the center, `y` the floor height they stand on.
Built-in kinds: `crate`, `box`, `barrel` (explodes, credits whoever shot it), `ball`, `heavy`. Code mods can add
more with `SpheresAPI.RegisterPropKind(new PropKind { ... })`.

```json
"props": [ { "kind": "barrel", "x": 19.0, "y": 8, "z": 8.5 }, { "kind": "crate", "x": 12.5, "y": 8, "z": 6.5, "yaw": 30 } ]
```

Breakable block types: `glass`, `crate`, `wood`, `brick`, `crystal` and `blast` (explodes). A data-pack block type
can explode too: add `"explodeRadius": 4, "explodeDamage": 40` to its `*.spheresblocks.json` entry.

#### Death messages

`SpheresAPI.AddDeathMessage("barrel_k", "{k} gift-wrapped a barrel for {v}")` adds a kill-feed line. Categories
include `bullet`, `explosion`, `self`, `fall`, `border`, `lava`, `trap`, `spikes`, `pendulum`, `saw`, `piston`,
`spinner`, `barrel` and `friendly`. Add `_k` for lines where another player gets the credit. Set `DamageInfo.Cause`
in your own damage to use a category of your own.

The easiest way to make a map is the in-game editor. It saves to `<persistentDataPath>/maps/*.spheresmap.json`, and you
can copy that file into a mod.

---

#### Translations

Drop a `*.spheresloc.json` file into any content folder to add or improve a language. The English UI text is the key:

```json
{ "language": "es", "name": "Español", "entries": [ { "en": "Play", "text": "Jugar" }, { "en": "Options", "text": "Opciones" } ] }
```

The language then appears in *Options > Language*. Keys must match the English text exactly, including leading
spaces. `Tools/extract_strings.py` lists the current keys. Code mods can call `Loc.Add("es", "My Button", "Mi botón")`
and use `Loc.T(...)` for their own text.

## 2. Code mods (BepInEx 5)

### Project setup

`Modding/ExampleCardMod/ExampleCardMod.csproj` is a ready-to-build template:

```
dotnet build Modding/ExampleCardMod -c Release -p:SpheresDir="C:\Games\Spheres"
```

* It targets `netstandard2.1` and references `BepInEx.Core 5.4.x` (from `nuget.bepinex.dev`) and `UnityEngine.Modules`.
* It references `Spheres.dll` from `SpheresDir\Spheres_Data\Managed`, or from `Library/ScriptAssemblies` when you
  build inside this Unity project.
* After a build, the DLL is copied into `thunderstore/plugins/`, ready for packaging.

### Minimal plugin

```csharp
using BepInEx;
using Spheres;
using UnityEngine;

[BepInPlugin("you.spheres.sniper", "Sniper Pack", "1.0.0")]
public class SniperPack : BaseUnityPlugin
{
    void Awake()
    {
        SpheresAPI.RegisterMod("you.spheres.sniper", "Sniper Pack", "1.0.0", "you");

        SpheresAPI.RegisterCard(new CardDefinition("you.railgun", "Railgun",
                "One huge, instant shot.", Rarity.Legendary, Color.cyan) { Source = "Sniper Pack" }
            .Mul(Stat.Damage, 4f)
            .Mul(Stat.BulletSpeed, 4f)
            .Set(Stat.BulletGravity, 0f)
            .Set(Stat.Ammo, 1)
            .Mul(Stat.ReloadTime, 1.5f));
    }
}
```

You can register cards, effects, maps and blocks at any time, before or after the game finishes booting. Cards are drawn when a hand is dealt. `Awake()` is the usual place.

### Card effects

Subclass `CardEffect` and override any hooks you need. Each time a player picks the card, they get a new
instance, so picking a card twice stacks its effect.

```csharp
public class Vampiric : CardEffect
{
    public override void OnKill(Player victim) => Player.Heal(50f);
}

// Register it under an id. JSON cards can use the same id.
SpheresAPI.RegisterEffect<Vampiric>("you.vampiric", "Heal 50 on kill");
SpheresAPI.RegisterCard(new CardDefinition("you.trophy", "Trophy Hunter", "Kills heal 50.", Rarity.Rare, Color.red)
    .Effect("you.vampiric"));
```

| Hook | When |
|---|---|
| `OnApplied` / `OnRemoved` | The card is added to or removed from the player |
| `OnRoundStart` | Each spawn |
| `OnUpdate(dt)` | Every frame while alive and not frozen |
| `OnFire(bullet)` | For each bullet fired; you can change `bullet.Damage`, `Velocity`, `Radius` and so on |
| `OnBulletBounce` / `OnBulletHitTerrain` | Bullet interactions with terrain |
| `OnBulletUpdate(bullet, dt)` | Every frame for each of your bullets (steering, growth, fuses; `bullet.Age` and `SpawnPosition` help) |
| `OnBeforeDealDamage(target, info)` | Change damage you're about to deal (executes, backstabs, range bonuses) |
| `OnBulletHitPlayer(bullet, target, info)` | After one of your bullets damages someone |
| `OnBlock` | When block is activated |
| `OnBlockedDamage(info)` | A block negated something: a bullet, border, fall, lava or explosion |
| `TryReflectBullet(bullet)` | Return `true` to keep a blocked bullet alive (see `bullet.Reflect`) |
| `OnBeforeDamaged(info)` | Change `info.Amount` before it is applied |
| `OnDamaged` / `OnDealtDamage` / `OnKill` / `OnRevive` | After the fact |
| `ModifyStat(stat, ref value)` | Change a stat on the fly, e.g. conditional buffs. Read `Player.Stats[...]` here, **not** `Player.GetStat` |

Card hooks that don't need a separate effect class:

* `CardDefinition.CanAppear = p => ...` decides whether the card can be offered to this player now.
* `CardDefinition.OnApply = p => ...` is one-off code that runs when the card is picked.

### Events

`GameEvents` exposes `MatchStarted`, `MatchEnded`, `RoundStarted`, `RoundEnded`, `PickPhaseStarted`,
`PickPhaseEnded`, `PlayerSpawned`, `PlayerDamaging` (you can modify or cancel the damage), `PlayerDamaged`,
`PlayerDied`, `PlayerBlocked`, `DamageBlocked`, `CardPicked`, `BulletFired`, `MapLoaded` and `ContentLoaded`.
Each handler runs inside its own try/catch, so a crashing mod handler gets logged and doesn't break the game.

```csharp
GameEvents.PlayerDamaging += e =>
{
    if (e.Info.Type == DamageType.Fall) e.Info.Amount *= 2f;   // brutal falls
};
```

### Other API

```csharp
SpheresAPI.RegisterMap("you:arena", mapData);          // MapData has Box/Spawn/BoxMirror4 builder helpers
SpheresAPI.RegisterBlockType(new BlockType("you.goo", "Goo", Color.green) { GroundAccel = 0.2f });
SpheresAPI.SetBaseStat(Stat.MoveSpeed, 10f);            // global balance change
SpheresAPI.RemoveCard("spheres.brittle_fury");            // remove a built-in card
SpheresAPI.AddMenuButton("My Mod Settings", () => ...); // main-menu button
SpheresAPI.AddGuiDrawer(() => GUI.Label(...));          // IMGUI overlay (pixel coordinates)
SpheresAPI.RegisterSound("shoot", myClip);              // replace a synthesized sound
MusicPlayer.TrackOverride = state => myClip;           // custom music per game state
SpheresAPI.Players / SpheresAPI.Map / SpheresAPI.Settings / SpheresAPI.Game
Teams.SetTeam(0, "Crimson", new Color(.8f, 0, .2f));
player.ApplyStun(0.5f); player.AddBarrier(30f, 4f); player.ApplySlow(0.4f, 1f); player.ApplyPoison(6f, 3f, source);
bullet.TerrainPierceLeft++; bullet.SetRadius(r); Fx.Box(pos, rot, size, color, life);  // Fx.* is mirrored online
Player.SafeLandingSpeed, Player.BottomBounceSpeed, Player.SideBounceSpeed  // fall/border tuning
```

### Harmony

`BepInEx.Core` ships HarmonyX. Patch any public or private method in `Spheres.dll`:

```csharp
[HarmonyPatch(typeof(Player), nameof(Player.TryJump))]
static class SparkleJumpPatch
{
    static void Postfix(Player __instance, bool __result)
    {
        if (__result) Fx.Burst(__instance.transform.position, Color.white, 6, 4f);
    }
}
// in Awake: new Harmony("you.mod").PatchAll();
```

Useful patch targets include `GameManager.StartMatch`, `GameManager.Pick`, `CardRegistry.DrawHand`,
`Player.TakeDamage`, `Player.TryBlock`, `Weapon.Fire`, `Bullet.Spawn`, `BotBrain.ChooseCard` and `MapRuntime.Build`.

### Multiplayer notes for mod authors

* In online matches **only the host simulates**. Card effects, `OnApply`, `GameEvents` handlers and Harmony
  patches on gameplay code run on the host. Clients are display-only (`Player.NetworkPuppet == true`).
* Anything you draw with `Fx.*`, play with `Sfx.Play` (for the standard sound ids) or add with `SpheresHud.AddFeedText`
  on the host during a match is mirrored to clients automatically.
* Clients need your card definitions so they can show card names and stats. Ship the same mod to everyone.
* `GameManager.Instance.NetClientMode` tells you that you're running as a client.

### Profiles, achievements, modes

* `Profile.Current` holds lifetime stats. `Profile.AchievementUnlocked` fires for each unlock.
* Add achievements to `Achievements.All`, and hats to `Hats.All`. A hat is a cube builder that runs on top of the head.
* `GameManager.Settings.mode` selects the round rules. `GameManager.RoundKills`, `HillProgress` and `RespawnIn(player)` expose mode state.
* `CardIcons.Register(id, rows)` and `CardIcons.EffectGlyphs` let mods give their cards icons.
* `NavGraph.For(map).FindPath(from, to)` gives you bot paths for custom AI.

### Custom input / networking

Players are driven by `IPlayerInput` (`HumanInput`, `BotBrain`). To drive a player from a network peer or a
replay, implement the interface, then patch `GameManager.StartMatch` (or replace `player.Input` on
`GameEvents.MatchStarted`).
