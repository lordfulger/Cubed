# Steam integration (optional)

Spheres builds and plays without Steam. The Steam features are compiled in only when the scripting define
**`SPHERES_STEAM`** is set, using [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks) (MIT), a
C# wrapper around Valve's Steamworks API. Without the define, none of the Steam code is compiled, so you don't need the
DLLs for normal development or for non-Steam releases (itch.io, direct download, Thunderstore testers).

The Steam code is still being written, so this page describes the setup and the feature set in general terms.
Menu labels and details may differ slightly from the final build.

---

## 1. Install Facepunch.Steamworks

1. Download a release from <https://github.com/Facepunch/Facepunch.Steamworks/releases>. Use the .NET Standard
   or .NET Framework build of the DLLs. Unity 2022.3 Mono supports both .NET Standard 2.1 and .NET Framework 4.x.
2. Create `Assets/Plugins/Facepunch.Steamworks/` and copy in:

   | File | Platforms (set in the Inspector under *Plugin Import Settings*) |
   |---|---|
   | `Facepunch.Steamworks.Win64.dll` | Editor (Windows), Standalone **Windows x86_64** only |
   | `Facepunch.Steamworks.Posix.dll` | Editor (macOS, Linux), Standalone **Linux x86_64** and **macOS** only |
   | `steam_api64.dll` | Editor (Windows), Standalone Windows x86_64 |
   | `libsteam_api.so` (64-bit) | Editor (Linux), Standalone Linux x86_64 |
   | `libsteam_api.dylib` or `steam_api.bundle` | Editor (macOS), Standalone macOS |

   The Win64 and Posix managed DLLs contain the same types. **Only one may be enabled for any given platform**,
   otherwise Unity reports duplicate types. Untick *Any Platform* on both and tick the platforms above.
3. The native `steam_api` libraries ship with the Facepunch release (in its redistributable folder). If they're
   missing, take them from the Steamworks SDK (`sdk/redistributable_bin/`, from <https://partner.steamgames.com>).
   Use the SDK version that the Facepunch release was built against, which its release notes state. A mismatched
   `steam_api` can crash on startup.

Unity copies the native libraries next to the player on build (`Spheres_Data/Plugins/x86_64/` on Windows/Linux,
`Spheres.app/Contents/PlugIns/` on macOS). Check a test build to make sure they are there.

The Steamworks redistributable libraries may be shipped with your game under the Steamworks SDK agreement. Check
that agreement before committing them to a **public** repository. Many projects keep them out of git and add them
on the build machine.

## 2. App id for testing: Spacewar (480)

Valve's public test app **480 ("Spacewar")** lets you test lobbies, invites and networking before you have
your own app id.

- Create `steam_appid.txt` containing just `480`:
  - **in the editor:** in the project root (next to `Assets/`), and
  - **in a build:** next to the executable (`Spheres.exe`, `Spheres.x86_64`). On macOS, Steam looks in the working
    directory, which is not the `.app` when launched from Finder. Don't put the file inside a signed `Spheres.app`.
  - Facepunch's `SteamClient.Init(appId)` passes the app id to Steam directly, so the file is mostly a fallback
    and for other tools. It's still the usual way to test.
- The Steam client must be running and logged in. Steam shows you as playing "Spacewar".
- Testing online needs **two Steam accounts on two machines** (or a machine plus a VM). Steam allows one logged-in
  account per machine.
- Spacewar has its own achievements and rich presence strings, so Spheres's achievements and presence text only
  work once you use your real app id (section 5).

For release, replace 480 with your app id in `SteamIntegration.AppId` (`Assets/Spheres/Scripts/Net/SteamIntegration.cs`). **Don't ship `steam_appid.txt`** in Steam depots. The game should be launched by Steam.

## 3. Turn on the define

**Edit > Project Settings > Player > (PC, Mac & Linux Standalone tab) > Other Settings > Script Compilation >
Scripting Define Symbols**: add `SPHERES_STEAM` and press *Apply*. Unity recompiles. If the Facepunch DLLs are
missing or set up for the wrong platform, you'll get compile errors about the `Steamworks` namespace.

To build a non-Steam version again, remove the define. CI (`.github/workflows/build.yml`) builds without it unless
`SPHERES_STEAM` is committed in `ProjectSettings/ProjectSettings.asset`. If you commit it, the Facepunch DLLs must be
available to CI too.

## 4. What it enables

| Feature | What players get |
|---|---|
| **Steam lobbies and invites** | Host a lobby from **Online > Host Steam lobby**, then press **Invite friends** in Match Setup to invite people through the Steam overlay. Friends can also join from their Steam friends list. No IP addresses or port forwarding. |
| **Relay networking** | Match traffic goes through Steam Datagram Relay. Hosts don't need to open TCP 27015, NAT isn't a problem, and players' IP addresses are hidden from each other. The host is still authoritative, as in IP play. |
| **Achievements** | Unlocked during play, shown in the Steam overlay and on the player's profile. |
| **Rich presence** | Friends see what you're doing ("In Match Setup", "In a match, round 3", "Editing a map") and can join a joinable lobby from your profile. |

Direct IP and LAN play keep working in the Steam build. Steam and non-Steam players can play together only over
IP/LAN, and only if both run the same game version.

### How players use it

1. Start Spheres **from Steam** (the overlay and invites need Steam running).
2. **Host:** *Main Menu > Online > Host Steam lobby*. Then invite people with
   *Invite Friends*, or press **Shift+Tab** to open the overlay, right-click a friend and choose *Invite to Game*.
3. **Join:** accept the invite in the overlay or Steam chat, or right-click a friend who is in a lobby and choose
   *Join Game*. Spheres starts (if needed) and joins the lobby.
4. In the lobby, everything works like IP play: the host moves players between teams, picks rules and starts the match.

If the Steam entries aren't in the Online menu, the build was made without `SPHERES_STEAM`, or Steam failed to start.
The Player log (docs/RELEASE.md, section 7) shows why.

## 5. Moving to your own app id

1. In Steamworks, open your app and set up:
   - **Stats & Achievements:** create the achievements with the *API names* the game code uses (check the code for
     the list), add icons, then **publish** the changes.
   - **Rich Presence:** upload a localization file with the tokens the game sets (for example
     `"#Status_InMatch" "In a match"`). See <https://partner.steamgames.com/doc/features/enhancedrichpresence>.
   - **Networking:** Steam Datagram Relay works with no extra setup for most games. Check
     <https://partner.steamgames.com/doc/features/multiplayer/steamdatagramrelay> if anything changes.
2. Change the app id in `SteamIntegration.AppId` and in `steam_appid.txt` for local testing.
3. Upload a build to a beta branch (docs/RELEASE.md, section 5), install it through Steam and test invites,
   achievements and presence with two accounts.

Valve's documentation: <https://partner.steamgames.com/doc/home>. Facepunch.Steamworks docs and examples:
<https://wiki.facepunch.com/steamworks/>.
