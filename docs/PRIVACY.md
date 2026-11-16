# Privacy policy (TEMPLATE)

> **This is a template, not a finished policy.** The game's owner must review it, fill in every `[bracketed]`
> placeholder, delete what doesn't apply to the build being shipped, and ideally have it checked by someone who
> knows the privacy laws where the game is sold (for example the GDPR in the EU/UK and the CCPA/CPRA in
> California). Update it whenever the game starts collecting anything new. Host the final version at a stable
> public URL and link it from the store page.

---

**Spheres privacy policy**
Last updated: [date]

This policy explains what information the game Spheres ("the game") uses, made by [owner / company name]
("we", "us"). Contact: [email address]. [If required: postal address / EU or UK representative.]

## Summary

- The game has **no ads, no analytics and no tracking** by default.
- **Online play** sends your chosen player name, your IP address and your gameplay input to the other players in
  the match.
- Settings, stats and logs are stored **on your own device**. We don't receive them unless you send them to us.
- [Steam version only:] Steam features run through Valve's Steam service. Valve's privacy policy applies to those.

## 1. Online multiplayer

When you host or join an online match, your game connects directly to the host (or, when you host, the other players
connect to you). We do not run a server in between [unless: Steam relay is used, see section 4].

| Data | Who receives it | Why |
|---|---|---|
| Player name you type in (or your Steam display name in the Steam version) | Everyone in the lobby and match | So players can see who is who |
| Your IP address and port | The host, if you join. All joining players, if you host. With LAN discovery, devices on your local network | Needed to make the network connection |
| Gameplay input and choices (movement, aiming, shooting, blocking, card picks, team choice) | The host, who sends the resulting game state to all players | To play the match |
| Game version, and custom maps used in the match | The host / other players | Version check, and so everyone has the map |

Other players can see your IP address while you're connected. Hosts who play over the internet open a port on
their router (TCP 27015 by default), and LAN discovery broadcasts on UDP 27016 on your local network. Only play
online with people you trust, or use the Steam version's relay networking, which hides your IP address from
other players.

We do not store or log any of this data on our side. The host's game keeps it in memory only for the session.
[Change this if the game ever writes connections to the log file: "The host's log file may record player names
and IP addresses of connections; it stays on the host's computer."]

## 2. Files stored on your device

The game stores these files locally, in your user data folder (for example
`%USERPROFILE%\AppData\LocalLow\Spheres\Spheres` on Windows, `~/Library/Application Support/Spheres/Spheres` on macOS,
`~/.config/unity3d/Spheres/Spheres` on Linux):

- **Settings**: graphics, audio, controls, your player name, the last IP address you joined, and match rules.
- **Profile stats**: [for example matches played, wins, cards picked]. Local only.
- **Content you make**: maps from the map editor, and content packs you install.
- **Logs and crash logs**: technical logs (`logs/` folder, Unity's `Player.log`) and crash dumps. They can contain
  your player name, IP addresses you connected to, file paths that include your OS user name, and hardware
  details (GPU, OS version).

We cannot see these files. You can delete them at any time by deleting the folder. Mods you install
(BepInEx plugins) are third-party software with their own behaviour, and this policy doesn't cover them.

## 3. Crash reports

[Pick one and delete the other.]

**A, no upload (default):** The game does not send crash reports anywhere. If you report a bug and choose to attach
your log files, we use them only to fix the problem and delete them within [period].

**B, optional upload (only if configured):** If crash reporting is turned on [describe where: an in-game prompt
or option, off by default], the game sends a crash report to [service name / our server at domain] after a crash.
It contains [the crash stack trace, game version, OS and hardware info, the recent log, and a random report ID].
It does not include [your player name / IP address, beyond what the server sees when it receives the request].
We keep reports for [period] and use them only to fix bugs. [Service name] processes them for us under
[link to its privacy policy / data processing terms]. You can turn this off at any time in [menu path].

## 4. Steam version

[Delete this section if Steam isn't used.]

If you play the Steam version, the game uses Steamworks for [lobbies and friend invites, relay networking,
achievements, rich presence, Steam Cloud]. To do this the game reads your Steam ID and display name and sends
your lobby status, achievements and "now playing" status to Steam. Relay networking routes match traffic through
Valve's servers instead of connecting you directly, so other players don't see your IP address. Valve handles this
data under the Steam Privacy Policy: <https://store.steampowered.com/privacy_agreement/>. We can see aggregated
statistics that Steam provides to developers (such as sales, achievement percentages and
[wishlist / usage statistics]), but no personal data beyond what Steam's partner tools show.

## 5. What we don't do

- No advertising, no advertising IDs, and no third-party analytics or tracking SDKs [in the default build].
- No user accounts run by us.
- We don't sell or share personal information.

## 6. Children

The game is not directed at children under [13 / 16, depending on the region]. Online play has no text or voice
chat, but player names are free text and visible to other players.

## 7. Your rights

Depending on where you live, you may have the right to access, correct or delete personal data we hold about you,
or to object to its processing. Because we don't store data from online play, this usually only covers bug reports
or crash reports you sent us. Contact [email address]. [If GDPR applies: you can also complain to your local data
protection authority. Our legal basis for processing crash reports is (consent / legitimate interest in fixing bugs).]

## 8. Changes

We'll update this page when the game's data use changes, and change the "Last updated" date. [For significant
changes we'll also mention it in the patch notes.]
