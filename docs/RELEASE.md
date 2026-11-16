# Release checklist

This is the checklist for shipping a Spheres build to players, on Steam, Thunderstore/r2modman, or as a plain download.
Work through it top to bottom for the first release. For later releases you usually only need sections 2 to 4, the depot upload in section 5 and package publishing in section 6.

Store, signing and mod-manager processes change often. When a step below links to official docs, follow those
docs if they disagree with this file.

---

## 1. First-time project setup (once)

The repository holds only code and a minimal `ProjectSettings/`. Unity has to generate the rest before CI or a
clean checkout can build.

- [ ] Open the project in Unity **2022.3 LTS**. The version in `ProjectSettings/ProjectVersion.txt` is the one CI uses.
- [ ] Let `Spheres > Setup Project` run (it runs automatically on first load). It creates
      `Assets/Spheres/Scenes/Main.unity`, adds it to Build Settings, adds the gamepad input axes, and sets
      **Mono** + **managed stripping disabled**.
- [ ] Check **Edit > Project Settings > Player**: Company Name and Product Name are final. They decide where
      settings, saves and logs are stored (see section 7). Changing them after release moves every player's data
      to a new folder, so decide now.
- [ ] Commit every generated `.meta` file, `Assets/Spheres/Scenes/Main.unity`, `ProjectSettings/*.asset` and
      `Packages/packages-lock.json`. Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or `Builds/`
      (they are already in `.gitignore`).
- [ ] Add the CI secrets `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` (step by step in
      [TESTING.md, section 1](TESTING.md#1-one-time-setup-give-github-actions-a-unity-license)), then run the
      **Build** workflow once with *Run workflow* to check that it works.

## 2. Versioning

Two numbers control compatibility. Both live in code:

| What | Where | When to change it |
|---|---|---|
| Game version | `SpheresAPI.GameVersion` in `Assets/Spheres/Scripts/Modding/SpheresAPI.cs` | Every release (`MAJOR.MINOR.PATCH`). |
| Network protocol | `NetConst.Protocol` in `Assets/Spheres/Scripts/Net/NetProtocol.cs` | Whenever a network message, snapshot layout or handshake changes. |
| Modding API | `SpheresAPI.ApiVersion` (same file as the game version) | When public API that mods use changes (bump the major version for breaking changes). |

- [ ] Bump `GameVersion`. The online handshake rejects players whose game version **or** protocol differs from
      the host's, so every release is incompatible online with every other release. That is on purpose: tell
      players to update.
- [ ] Bump `Protocol` if you changed networking code, even when the game version also changed. It keeps
      development builds with the same version string from connecting to each other with mismatched code.
- [ ] CI copies `GameVersion` into `PlayerSettings.bundleVersion`. For local builds, set
      **Player > Version** to the same value (macOS shows it in Finder; Windows shows it in file properties).
- [ ] Write release notes (what changed, known issues, whether mods need updating).
- [ ] Tag the release commit: `git tag v0.2.0 && git push origin v0.2.0`.

## 3. Platform builds

Build locally with **Spheres > Build > Windows / Linux / macOS** (output in `Builds/<platform>/`), or download the
artifacts from the **Build** GitHub Actions workflow. CI artifacts are `.tar.gz` files, so the Linux and macOS
executables keep their executable bit. Extract them with `tar -xzf`, not by double-clicking in Windows.

- [ ] The **Smoke test** job of the Build workflow is green for the release commit (or run it locally; see
      [TESTING.md](TESTING.md)). It plays every menu, the map editor and a bots-only match on every map and fails on
      any logged error. Read the "Smoke test with graphics" section of the run summary too.
- [ ] Optional: run the card balance simulator (**Run workflow** > *Run the card balance simulator*, 2000+ matches)
      and look at the flagged cards in `balance-report.md`.

For each platform:

- [ ] The game starts from a clean folder on a machine that has never run it (no old settings).
- [ ] Main menu, a local match against bots, the map editor and the card library work.
- [ ] Online: host on one machine and join from another. Try it on LAN, and over the internet with TCP 27015
      forwarded. LAN discovery uses UDP 27016.
- [ ] Gamepad: menus and a match work with only a controller.
- [ ] BepInEx: install BepInEx 5 (docs/R2MODMAN.md, section A). The main menu shows "BepInEx active", and the
      example mods load.
- [ ] No `*_BurstDebugInformation_DoNotShip` or `*_BackUpThisFolder_ButDontShipItWithYourGame` folders in what
      you ship. Unity writes these next to builds for some settings; leave them out of zips and Steam depots.

Executables: Windows `Spheres.exe` (with `UnityPlayer.dll`, `UnityCrashHandler64.exe`, `Spheres_Data/`),
Linux `Spheres.x86_64` (with `UnityPlayer.so`, `Spheres_Data/`), macOS `Spheres.app`.

## 4. Code signing

### Windows (optional)

Unsigned games run, but Windows SmartScreen may warn on direct downloads. Steam installs are usually not affected.

- Get a code-signing certificate from a CA. Since 2023, new OV and EV certificates come on a hardware token or
  in a cloud HSM, not as a `.pfx` file you can copy. Microsoft's cloud signing service (Azure Trusted Signing; Microsoft has renamed it
  before, so search for the current name) is often the cheapest option where it is available. Check the current
  eligibility rules.
- Sign `Spheres.exe` (and `UnityCrashHandler64.exe` if you like; `UnityPlayer.dll` is already signed by Unity):

  ```
  signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a Spheres.exe
  signtool verify /pa /v Spheres.exe
  ```

  `/a` picks the best certificate from the store or token. Use `/f cert.pfx /p ...` or your provider's
  dlib/plugin options instead if they say so. Always timestamp (`/tr`), so the signature stays valid after the
  certificate expires.
- Sign **before** you upload to Steam or zip the build, because signing changes the file.

### macOS (signing + notarization)

Without signing and notarization, macOS Gatekeeper blocks a downloaded `Spheres.app` ("cannot be opened because the
developer cannot be verified"). Notarization is needed for direct downloads and recommended for Steam.

You need an Apple Developer Program membership, a **Developer ID Application** certificate in your keychain, and
a Mac with Xcode command-line tools. Apple's guide:
<https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution>.
Unity's manual also has a page on notarizing macOS builds (search the 2022.3 manual for "notarization").

1. Create `Spheres.entitlements`. Mono needs JIT. `disable-library-validation` and
   `allow-dyld-environment-variables` let BepInEx's macOS launcher and native mod libraries load. Leave those
   two out if you don't want to support code mods on macOS.

   ```xml
   <?xml version="1.0" encoding="UTF-8"?>
   <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
   <plist version="1.0">
   <dict>
     <key>com.apple.security.cs.allow-jit</key><true/>
     <key>com.apple.security.cs.allow-unsigned-executable-memory</key><true/>
     <key>com.apple.security.cs.disable-library-validation</key><true/>
     <key>com.apple.security.cs.allow-dyld-environment-variables</key><true/>
   </dict>
   </plist>
   ```

2. Clean extended attributes, then sign the nested code first and the app last. Use the hardened runtime and a
   secure timestamp, which notarization requires.

   ```
   IDENTITY="Developer ID Application: Your Name (TEAMID)"
   xattr -cr Spheres.app
   chmod -R a+rX Spheres.app
   find Spheres.app/Contents -type f \( -name "*.dylib" -o -name "*.so" \) \
       -exec codesign --force --timestamp --options runtime --sign "$IDENTITY" {} \;
   find Spheres.app/Contents -type d -name "*.bundle" \
       -exec codesign --force --timestamp --options runtime --sign "$IDENTITY" {} \;
   codesign --force --timestamp --options runtime --entitlements Spheres.entitlements \
       --sign "$IDENTITY" Spheres.app
   codesign --verify --deep --strict --verbose=2 Spheres.app
   ```

   (`codesign --deep` for signing also works with Unity apps and is what Unity's docs show. Apple recommends
   signing inside out as above.)

3. Notarize. Store the credentials once with an app-specific password from <https://account.apple.com>, then submit:

   ```
   xcrun notarytool store-credentials spheres-notary --apple-id you@example.com --team-id TEAMID
   ditto -c -k --keepParent Spheres.app Spheres-notarize.zip
   xcrun notarytool submit Spheres-notarize.zip --keychain-profile spheres-notary --wait
   # if it says Invalid:
   xcrun notarytool log <submission-id> --keychain-profile spheres-notary
   ```

4. Staple the ticket and check it:

   ```
   xcrun stapler staple Spheres.app
   xcrun stapler validate Spheres.app
   spctl --assess --type execute --verbose=4 Spheres.app
   ```

5. Distribute the stapled app (zip it with `ditto -c -k --keepParent`, or upload it to the macOS Steam depot).
   Do not change anything inside `Spheres.app` after signing. That includes adding `steam_appid.txt` or mod files inside the bundle.

## 5. Steam

Valve's documentation is the source of truth: <https://partner.steamgames.com/doc/home>. The Steamworks UI changes
from time to time, so menu names below may differ.

### Store page and app setup

- [ ] Join Steamworks (<https://partner.steamgames.com>), complete the tax and bank forms, and pay the Steam Direct
      app fee. Valve enforces waiting periods between paying the fee and releasing. Check the current rules in
      <https://partner.steamgames.com/doc/gettingstarted>.
- [ ] Fill in the store page: description, screenshots, trailer, capsule and library art (Valve lists the current
      sizes in <https://partner.steamgames.com/doc/store/assets>), tags, system requirements, supported languages,
      controller support, the content survey, and multiplayer flags (online PvP, LAN, shared/split screen).
- [ ] Submit the store page for review and publish it as **Coming Soon**. Valve requires a minimum time in Coming
      Soon before launch. Check <https://partner.steamgames.com/doc/store/review_process>.
- [ ] Link the privacy policy (docs/PRIVACY.md, filled in and hosted somewhere public) if the game has online play.

### Depots and builds (SteamPipe)

Create one depot per OS in **App Admin > SteamPipe > Depots**, and set each depot's operating system so Steam
installs the right one. Add launch options in **Installation > General**: `Spheres.exe` (Windows), `Spheres.x86_64`
(Linux), `Spheres.app` (macOS). Publish the app-admin changes.

Get `steamcmd` from the Steamworks SDK (`sdk/tools/ContentBuilder`). Use a dedicated build account with Steam Guard
and only the build permissions it needs. Example layout and script, using the placeholder app id `1234560` and depots
`1234561`-`1234563`:

```
ContentBuilder/
  content/windows/   <- contents of Builds/Windows
  content/linux/     <- contents of Builds/Linux
  content/macos/     <- Spheres.app (signed + notarized)
  scripts/app_build_1234560.vdf
  output/
```

```
"AppBuild"
{
	"AppID"       "1234560"
	"Desc"        "Spheres 0.2.0"
	"ContentRoot" "../content/"
	"BuildOutput" "../output/"
	"SetLive"     ""        // e.g. "beta" to set a branch live automatically; never "default" from a script
	"Preview"     "0"       // "1" = dry run, nothing uploaded

	"Depots"
	{
		"1234561"   // Windows
		{
			"FileMapping" { "LocalPath" "windows/*" "DepotPath" "." "recursive" "1" }
			"FileExclusion" "*.pdb"
			"FileExclusion" "steam_appid.txt"
			"FileExclusion" "*_DoNotShip*"
			"FileExclusion" "*_ButDontShipItWithYourGame*"
		}
		"1234562"   // Linux
		{
			"FileMapping" { "LocalPath" "linux/*" "DepotPath" "." "recursive" "1" }
			"FileExclusion" "steam_appid.txt"
			"FileExclusion" "*_DoNotShip*"
			"FileExclusion" "*_ButDontShipItWithYourGame*"
		}
		"1234563"   // macOS
		{
			"FileMapping" { "LocalPath" "macos/*" "DepotPath" "." "recursive" "1" }
		}
	}
}
```

```
steamcmd +login <build_account> +run_app_build "$(pwd)/scripts/app_build_1234560.vdf" +quit
```

Then in **SteamPipe > Builds**, set the new build live on a beta branch, test it by installing through Steam, and
only after that set it live on `default`. Full reference: <https://partner.steamgames.com/doc/sdk/uploading>.

- [ ] Build the Steam version with `SPHERES_STEAM` enabled and the real app id (see docs/STEAM.md). Leave
      `steam_appid.txt` out of depots.
- [ ] Optional: Steam Cloud. Settings and user maps live under `persistentDataPath` (section 7). Steam Auto-Cloud
      can sync them with a root override per OS, without code changes.
- [ ] Submit the build for review (Valve checks that it launches and matches the store page) before the release date.

### Steam Deck

Valve tests games and assigns Verified / Playable / Unsupported. See
<https://partner.steamgames.com/doc/steamdeck/compat> for the current criteria. Things that matter for Spheres:

- [ ] **Input:** everything works with a controller, including menus, Match Setup (press Start to join), card
      picks and the map editor (or the editor is clearly marked as needing keyboard/mouse). On-screen prompts must
      show controller glyphs when a controller is used.
- [ ] **Text entry:** the online name, IP address and map name fields need an on-screen keyboard. Steam's floating
      keyboard can be opened by the game through Steamworks. Without it, the Deck falls back to Steam+X.
- [ ] **Display:** runs at 1280x800 (16:10) by default, and UI text is legible at that size. Valve's guideline
      is a minimum text height. IMGUI text in `UIKit.Small` is the most likely to fail.
- [ ] **Performance:** steady frame rate at Deck resolution. Split-screen with 4+ players is the worst case.
- [ ] **Runtime:** ship the native Linux depot, or confirm the Windows build runs under Proton. Under Proton,
      BepInEx needs the launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`. Mention it in mod docs.
- [ ] No launcher, no unsupported anti-cheat, and the game suspends/resumes cleanly (online sessions may drop;
      that's fine if it recovers to a menu).

## 6. Thunderstore and r2modman

docs/R2MODMAN.md explains the background. Thunderstore's process for new communities changes. Check
<https://wiki.thunderstore.io> and ask in the Thunderstore Discord (linked from <https://thunderstore.io>) before
you start.

- [ ] **Community request.** Ask the Thunderstore team for a Spheres community. They usually want the game to be
      public (or close to it), a store link, and confirmation that it supports BepInEx.
- [ ] **Ecosystem schema PR.** Open a PR to <https://github.com/thunderstore-io/ecosystem-schema>, following its
      README and copying an existing Unity Mono + BepInEx 5 game. It typically includes the display name,
      executable names (`Spheres.exe`, `Spheres.x86_64`), data folder (`Spheres_Data`), Steam app id and/or a manual
      folder picker for non-Steam installs, the `bepinex` package loader, install rules, and the **mod loader
      package** (`<Team>-BepInExPack_Spheres`, root folder `BepInExPack`). r2modman picks up schema changes on its own
      schedule. Some versions bundle the schema, so the game may only show up after an r2modman update.
- [ ] **BepInExPack.** Download the official BepInEx 5 Windows x64 zip from
      <https://github.com/BepInEx/BepInEx/releases>, then run:

      ```
      python Tools/make_bepinex_pack.py BepInEx_win_x64_5.4.23.x.zip --author <YourTeam> --out dist
      ```

      Check `dist/BepInExPack_Spheres/` (manifest, README, `BepInExPack/winhttp.dll`, `BepInExPack/BepInEx/core`,
      `doorstop_config.ini` targeting `BepInEx\core\BepInEx.Preloader.dll`). Upload the zip, and replace the
      placeholder icon with real art first if you have it. Ask the Thunderstore/BepInEx team whether they would
      rather publish the pack themselves. For some communities the BepInEx team publishes the official
      `BepInEx-BepInExPack_<Game>`.
- [ ] Update the dependency in `Modding/ExampleCardMod/thunderstore/manifest.json` (currently the placeholder
      `BepInEx-BepInExPack-5.4.2100`) to the pack you actually published, e.g. `<YourTeam>-BepInExPack_Spheres-5.4.2302`.
- [ ] **Publish packages.** Create a team on thunderstore.io and upload zips through the website's upload page, or
      from CI with the Thunderstore CLI (`tcli`, <https://github.com/thunderstore-io/thunderstore-cli>) and a
      service-account token stored as a GitHub secret. The **Build** workflow already produces the example data
      pack zip as an artifact.
- [ ] Install through r2modman on a clean machine. Choose Spheres, create a profile, install the BepInExPack and the
      example mods, press *Start modded*, and confirm "BepInEx active" plus the Mods list.

## 7. Crash reports and logs

Tell testers where to find these. The company and product names (`Spheres`/`Spheres` unless changed in Player
Settings) are part of every path.

| | Windows | macOS | Linux |
|---|---|---|---|
| Unity `Player.log` (and `Player-prev.log`) | `%USERPROFILE%\AppData\LocalLow\Spheres\Spheres\` | `~/Library/Logs/Spheres/Spheres/` | `~/.config/unity3d/Spheres/Spheres/` |
| `persistentDataPath` (settings, maps, `Content/`) | `%USERPROFILE%\AppData\LocalLow\Spheres\Spheres\` | `~/Library/Application Support/Spheres/Spheres/` | `$XDG_CONFIG_HOME/unity3d/Spheres/Spheres/` (usually `~/.config/unity3d/...`) |
| Game log | `<persistentDataPath>\logs\` | `<persistentDataPath>/logs/` | `<persistentDataPath>/logs/` |
| Native crash dumps | `%TEMP%\Spheres\Spheres\Crashes\` | `~/Library/Logs/DiagnosticReports/` | `Player.log` (stack trace at the end) |
| BepInEx log | `<game or r2modman profile>\BepInEx\LogOutput.log` | same | same |

- The game's own log folder (`<persistentDataPath>/logs`) is new. Check that the shipped build writes to it.
- Under Proton, Windows paths are inside `steamapps/compatdata/<appid>/pfx/drive_c/users/steamuser/`.
- In the editor, **Spheres > Open User Data Folder** opens `persistentDataPath`.
- Bug reports should include the game version (shown in the main menu footer), the OS, whether BepInEx/mods were
  installed, `Player.log`, and the game log.

## 8. Legal

This section is a checklist, not legal advice. Have a lawyer review anything you are unsure about, especially
before you spend money on marketing.

- [ ] **Trademark search for the name "Spheres".** "Spheres" is a common word and probably already used by games,
      apps and other products. **The owner, ideally with a trademark lawyer, has to do this search. It was not
      done for you.** At minimum, search the USPTO (<https://tmsearch.uspto.gov>), EUIPO / TMview
      (<https://www.tmdn.org/tmview>) and WIPO's Global Brand Database (<https://branddb.wipo.int>) in the
      classes for video games and software (Nice classes 9, 28 and 41). Also search Steam, itch.io, console stores
      and app stores for similar names. Rename before launch if there is a conflict. Renaming later also means
      changing the product name, the data folder paths, the Thunderstore community and the ecosystem schema entry.
- [ ] **BepInEx (LGPL-2.1).** The game doesn't need to include BepInEx, and players can install it themselves. If
      you distribute it (in a BepInExPack, inside the Steam depot, or in a download):
  - include the LGPL-2.1 license text and a notice that the package contains BepInEx, with a link to its source;
  - point to, or provide, the exact source code of the version you ship (the matching release tag is enough if you
    didn't modify it);
  - if you change BepInEx itself, publish your changes under LGPL-2.1;
  - keep it replaceable: users must be able to swap in another BepInEx build (true as long as it stays separate DLLs);
  - keep the notices of the components it bundles (HarmonyX, MonoMod and Mono.Cecil are MIT; Unity Doorstop has its
    own license; check each).
  Spheres's own code is not affected by shipping BepInEx next to it as separate files, but check this with your lawyer
  if you link to BepInEx in other ways.
- [ ] **Other third-party code.** Facepunch.Steamworks is MIT (keep its license notice). Steam's `steam_api` libraries
      are covered by the Steamworks SDK agreement. Unity runtime terms depend on your Unity plan (revenue limits,
      splash screen rules for Unity Personal). Check the current Unity terms for 2022.3.
- [ ] **A separate identity from the games that inspired Spheres.** Card-pick arena shooters (for example ROUNDS) are a
      genre, and game mechanics are generally not protected. Names, logos, art, UI layout, card names and card text,
      sounds and store descriptions can be. Don't use another game's name in Spheres's title or marketing beyond a
      factual comparison, don't copy its card names, descriptions or visual style, and review the 407 card names
      for any that match another game's. Use original store art and a logo that doesn't look like theirs.
- [ ] **Privacy policy.** Online play sends player names and IP addresses between players (see docs/PRIVACY.md).
      Laws like the GDPR treat IP addresses as personal data, so publish a privacy policy before release. Fill in
      the template in docs/PRIVACY.md, have it reviewed, host it at a stable URL, and link it from the store page
      and the game's README/website. Update it whenever you add crash upload, analytics, Steam features or accounts.
- [ ] **Age rating / content.** Fill in Steam's content survey honestly. Consoles and some regions need IARC, ESRB
      or PEGI ratings.
- [ ] **Mod policy.** Say in the README or EULA what mods may do (for example: no cheating in public online
      matches, no distributing the game's own files) and that modded games are unsupported.
