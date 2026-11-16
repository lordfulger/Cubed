# Testing and CI

Spheres has two automated checks that play the game without a human:

- **Smoke test.** Opens every menu screen and the map editor (including loading every map and a test play), runs
  training, pauses a match and opens the options overlay. Then it plays a bots-only match on **every registered map**,
  about 60 simulated seconds each and always including a card pick phase, followed by each extra game mode. The
  test fails if anything logs an error, exception or failed assert. The report lists every error with its stack trace,
  grouped by map or section.
- **Card balance simulator.** Plays hundreds of fast bot-vs-bot matches. For every card it records how often the card
  was offered and picked, and how often players holding it won. It writes a CSV with every card and a Markdown summary
  that flags cards whose holders win more than 60% or less than 40% of matches.

Both run inside the game at an accelerated, fixed time step, so a minute of play takes a few seconds. They can run in
three ways: in GitHub Actions (section 2), from the Unity editor on your machine (section 3), or from a built game
(section 4).

The code lives in `Assets/Spheres/Automation/` (runtime) and `Assets/Spheres/Editor/SpheresAutomation.cs` (editor
entry points). It only switches on when asked to, so a normal game launch never runs it.

---

## 1. One-time setup: give GitHub Actions a Unity license

Building a Unity game in the cloud needs a Unity license. A free **Personal** license is enough. You add it to the
repository as three *secrets*: values that GitHub keeps encrypted and that never show up in logs. Until they are added,
the Unity jobs skip with the warning "Unity jobs skipped". Everything else in the workflow still runs.

GameCI's activation guide is the official source for these steps: <https://game.ci/docs/github/activation>. If it
disagrees with this page, follow GameCI.

1. **Get a Unity account and Unity Hub.** Sign up at <https://id.unity.com>, then install Unity Hub from
   <https://unity.com/download>.
2. **Activate a Personal license on your computer.** In Unity Hub, sign in, open **Settings** (the gear icon) >
   **Licenses**, choose **Add** > **Get a free personal license**, and accept.
3. **Find the license file.** Activation creates a file named `Unity_lic.ulf`:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`. `ProgramData` is hidden; paste the path into File Explorer's address bar.
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
4. **Copy its contents.** Open the file in Notepad (or any text editor), select everything (Ctrl+A) and copy it. It is
   XML that starts with `<?xml` and ends with `</root>`. Copy all of it, including the first and last lines.
5. **Add the secrets on GitHub.** In the repository, go to **Settings** > **Secrets and variables** > **Actions** >
   **New repository secret**, and create these three:

   | Name | Value |
   |---|---|
   | `UNITY_LICENSE` | The full contents of `Unity_lic.ulf` from step 4. |
   | `UNITY_EMAIL` | The email address of your Unity account. |
   | `UNITY_PASSWORD` | The password of your Unity account. |

   If you have a Pro/Plus serial key, add `UNITY_SERIAL` instead of `UNITY_LICENSE` (keep the email and password).
6. **Check that it works.** Open the **Actions** tab > **Build** > **Run workflow** > **Run workflow**. The first run
   is slow because Unity imports the whole project. Later runs reuse a cache.

If the build log says the license is invalid or activation failed: make sure you pasted the whole file with no extra
spaces, and that the email and password are correct. If your Unity account uses two-factor authentication, check the
GameCI activation guide for its current advice. Personal licenses sometimes need to be re-activated. If builds suddenly
fail at activation months later, repeat steps 2 to 5.

## 2. Using CI

The **Build** workflow (`.github/workflows/build.yml`) runs on every push to `main` and when you start it by hand
(**Actions** > **Build** > **Run workflow**). It:

1. builds Windows, Linux and macOS players with GameCI,
2. runs the **smoke test** on the Linux build, headless (no license needed for this part), plus a short informational
   run with software rendering that also covers the menu and HUD drawing code,
3. if you ticked **Run the card balance simulator** when starting the run by hand, runs the **balance simulator** on
   the Linux build. You can set the number of matches, the map, 1v1 or 2v2, and the pick policy,
4. packages the example Thunderstore mod and validates the built-in maps (no Unity needed).

A red **Smoke test** job means the game logged an error somewhere. Open the run's **Summary** page. The first lines
of the report are shown there.

### Downloading builds and reports

1. Open the **Actions** tab and click the workflow run (each push creates one).
2. Scroll to the bottom of the run's **Summary** page, to the **Artifacts** section.
3. Click an artifact to download it as a `.zip`:
   - `Spheres-<version>-StandaloneWindows64`, `...-StandaloneLinux64`, `...-StandaloneOSX`: the game builds,
   - `smoke-report`: `smoke-report.txt` (and the player logs),
   - `balance-report`: `balance-report.csv` and `balance-report.md` (only when the simulator ran).
4. Unzip it. The builds are `.tar.gz` archives inside the zip, which keeps the Linux/macOS executable bits. On
   Windows 10 or 11, extract one from a terminal in that folder:

   ```bat
   tar -xzf Spheres-0.1.0-StandaloneWindows64.tar.gz
   ```

   (or use 7-Zip). Then run `Spheres.exe`.

Artifacts are deleted after 14 days (balance reports after 30).

## 3. Running locally from the Unity editor (Windows)

Close the project in the Unity editor first. Unity refuses to open a project that is already open. Replace the
paths below with yours. Unity Hub shows the editor path under **Installs** (gear > Show in Explorer).

**Smoke test** (Command Prompt):

```bat
set UNITY="C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
start /wait "" %UNITY% -batchmode -projectPath "C:\dev\Spheres" -executeMethod Spheres.EditorTools.SmokeTest.Run -logFile "C:\dev\Spheres\Builds\smoke.log"
echo Exit code: %ERRORLEVEL%
type "C:\dev\Spheres\Builds\smoke-report.txt"
```

**Balance simulator:**

```bat
start /wait "" %UNITY% -batchmode -projectPath "C:\dev\Spheres" -executeMethod Spheres.EditorTools.BalanceSim.Run -balanceMatches 500 -logFile "C:\dev\Spheres\Builds\balance.log"
```

Results: `Builds\balance-report.csv` (open it in Excel) and `Builds\balance-report.md`.

PowerShell equivalent:

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
$p = Start-Process $unity -Wait -PassThru -ArgumentList '-batchmode','-projectPath','C:\dev\Spheres','-executeMethod','Spheres.EditorTools.SmokeTest.Run','-logFile','C:\dev\Spheres\Builds\smoke.log'
"Exit code: $($p.ExitCode)"
```

Notes:

- **Do not add `-quit`.** The editor has to keep running to play the game, and it exits by itself when the run ends.
- `start /wait` matters. Without it, Command Prompt does not wait for Unity and `%ERRORLEVEL%` is meaningless.
- Exit codes: `0` passed, `1` errors were logged (see the report), `2` timed out, `3` could not enter play mode
  (usually compile errors; see the log), `4` play mode was stopped before the run finished.
- Leave out `-batchmode` to watch the run in the editor window. That also covers the menu and HUD drawing code, which
  Unity never runs in batchmode.
- Inside the editor: **Spheres > Tests > Run Smoke Test**, **Run Quick Smoke Test** or **Run Balance Simulator**. The
  result is printed to the Console and the reports go to `Builds/`.
- The run temporarily changes the match setup (players, map, rules) and puts it back at the end. Your profile and
  achievements are not touched, because no human player takes part. If you kill Unity mid-run, re-check your match setup.

## 4. Running from a built game

The same tests are built into the game and switch on with a command-line flag. This is how CI runs them, and it is
useful for checking a build on another machine:

```bat
Spheres.exe -batchmode -nographics -spheresSmokeTest -smokeReport smoke-report.txt -logFile smoke.log
Spheres.exe -batchmode -nographics -spheresBalanceSim -balanceMatches 500 -balanceOut balance-report -logFile balance.log
```

Linux: `./Spheres.x86_64` with the same arguments.

## 5. Options

Add any of these after the command (editor or built game).

| Option | Default | Meaning |
|---|---|---|
| `-smokeSeconds 60` | 60 | Simulated seconds per map. |
| `-smokeModeSeconds 40` | 40 | Simulated seconds per extra game mode (Deathmatch, King of the Hill, Card Rush). |
| `-smokeBots 4` | 4 | Bots per match, split over two teams. |
| `-smokeMaps a,b` | all | Only these map ids, e.g. `builtin:garden,builtin:spire`. |
| `-smokeSkip x,y` | none | Skip sections: `menus`, `editor`, `training`, `pause`, `maps`, `modes`. |
| `-smokeReport path` | `Builds/smoke-report.txt` | Report file. |
| `-balanceMatches 500` | 500 | Matches to play. How long one takes depends on the machine; the log prints progress every 10 matches. |
| `-balanceMap id` | `builtin:duel_cell` | Map to play on. |
| `-balanceTeamSize 1` | 1 | 1 = 1v1, 2 = 2v2. |
| `-balancePicks random` | random | `random` (every card gets seen, gives an "average deck" win rate) or `bot` (BotBrain's own choices, also measures pick rate). |
| `-balanceDifficulty Hard` | Hard | Bot difficulty (Easy, Normal, Hard, Brutal). |
| `-balanceRounds 3` | 3 | Rounds needed to win a match. |
| `-balanceStartingPicks 2` | 2 | Cards everyone picks before round 1 (everyone also picks after each round). |
| `-balanceMinSamples 20` | 20 | Matches a card must be held in before it can be flagged. |
| `-balanceSeed n` | random | Seed for the random pick policy. |
| `-balanceOut prefix` | `Builds/balance-report` | Output prefix (`.csv` and `.md` are added). |
| `-balanceFailOnError` | off | Exit with code 1 if the game logged errors during the simulation. |
| `-simFps 50` | 50 | Simulated frames per second of game time. Lower is faster but coarser. `0` = real time. |
| `-simTimeScale 2` | 2 | Game speed multiplier on top of that. |
| `-automationTimeout 60` | 60 (smoke), 600 (balance) | Wall-clock limit in minutes. |
| `-automationIgnore regex` | none | Ignore log messages that match this regular expression (e.g. a known harmless warning on a CI machine). |

### Reading the balance report

There are about 300 cards, so each card only appears in a small share of matches. With 500 1v1 matches and about six
picks per player per match, a typical card is held in only about 20 matches, so its win rate is still noisy (see the
95% interval column) and few cards reach the flagging threshold. Use 2000 or more matches for decisions, and treat a flag as "look at this card", not as proof. Win rates are measured with
**everyone picking** after each round, so holding a card does not mean you were losing. The default **random**
picks give each card an unbiased "average deck" win rate. With `-balancePicks bot`, the pick rate also tells you
which cards the bots value.
