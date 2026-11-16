# BepInEx, Thunderstore and r2modman

Spheres is a Unity **Mono** build with managed code stripping turned off (`Spheres > Build` sets this up), which is
what BepInEx 5 needs. Once Spheres is built, all three of the setups below work.

## A. Manual BepInEx install (works today)

1. Download **BepInEx 5** (x64, e.g. `BepInEx_win_x64_5.4.23.x.zip`) from
   <https://github.com/BepInEx/BepInEx/releases>. Use v5, not the v6 IL2CPP builds.
2. Extract it into the folder that contains `Spheres.exe`. You should end up with `winhttp.dll`,
   `doorstop_config.ini` and `BepInEx/` sitting next to the exe.
3. Run the game once. BepInEx creates `BepInEx/plugins` and `BepInEx/config`.
4. Put mods in `BepInEx/plugins/<ModName>/`:
   * code mods: the `.dll`
   * data mods: the `*.spherescards.json` / `*.spheresmap.json` / `*.spheresblocks.json` files
5. The main menu footer shows "BepInEx active", and **Mods** lists everything that loaded.

Linux builds use BepInEx's `run_bepinex.sh`. macOS builds need the macOS BepInEx package and its launch script.

## B. Thunderstore packages

r2modman installs Thunderstore packages, which are zips laid out like this:

```
manifest.json      name, version_number, website_url, description, dependencies
icon.png           256x256
README.md
plugins/           DLLs (code mods)
...                any other files, e.g. cards/*.spherescards.json
```

Build one with:

```
python Tools/package_thunderstore.py Modding/ExampleDataPack --author YourTeam --out dist
python Tools/package_thunderstore.py Modding/ExampleCardMod/thunderstore --author YourTeam --out dist
```

When r2modman installs a BepInEx package, it extracts it into `BepInEx/plugins/<Team>-<Name>/` inside the
profile. Spheres scans that folder recursively, so JSON content anywhere in the package is picked up.
Spheres finds the active plugin folder through `BepInEx.Paths.PluginPath`, so r2modman profiles (which live outside
the game folder) work.

The example manifests depend on `BepInEx-BepInExPack-5.4.2100`. Change that to whatever BepInEx pack the Spheres
Thunderstore community ends up using.

## C. Getting Spheres into r2modman

r2modman only lists games that are defined in Thunderstore's **ecosystem schema**. A new game needs:

1. **A Thunderstore community** for Spheres. You request this from the Thunderstore team (see
   <https://thunderstore.io> and their Discord). It hosts the packages.
2. **An entry in the ecosystem schema** (<https://github.com/thunderstore-io/ecosystem-schema>). It describes the
   game to mod managers: display name, executable names (`Spheres.exe`, `Spheres.x86_64`), data folder
   (`Spheres_Data`), where to find the game (Steam app id, or a manual folder picker for non-Steam installs),
   the package loader (**BepInEx**), and the install rules (plugins to `BepInEx/plugins`, and so on).
   Copy an existing Unity Mono + BepInEx 5 game entry and change the names. Follow the schema's current
   README; the format changes over time.
3. **A BepInEx pack** published to that community, usually a repack of BepInEx 5 x64 such as
   `BepInEx-BepInExPack_Spheres`, which mods then list as a dependency.

After the schema change ships, Spheres appears in r2modman's game list. Point it at your Spheres install, and
packages built with `Tools/package_thunderstore.py` install with one click.

Until then, testers can use **A**, or extract a package zip into `BepInEx/plugins/<Team>-<Name>/` by hand. The
layout is the same, so nothing changes once r2modman support arrives.
