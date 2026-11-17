# Online accounts (sign in + cloud saves)

Spheres has optional player accounts through **Unity Gaming Services** (UGS).
- Players create an account with a username and password.
- Their profile follows them to any PC: characters, hats, trails, unlocks, achievements and stats.
- It talks to UGS over its web API, so no extra Unity packages are needed.
- The free UGS tier is plenty for a game this size.

Until you finish the setup below, the **Sign in** window says "Online accounts aren't set up in this build yet" and everything
else works as before.

## One-time setup (about 5 minutes)

1. **Link the Unity project.** In Unity:
   - Go to **Edit > Project Settings > Services**.
   - Sign in if asked, pick your organization, and choose **Create project ID** (or link an existing one).
   - The game reads this ID automatically (`Application.cloudProjectId`), in the editor and in builds.
2. **Turn on username sign-in.** Open https://cloud.unity.com and select the project.
   - Go to **Products > Authentication** (search for "Authentication" if it isn't listed).
   - Under **Identity providers**, click **Add**, choose **Username & Password**, and save.
3. **Turn on Cloud Save:** in the same dashboard, open **Products > Cloud Save** and click **Get started** / **Enable**.
4. Press Play, click **Sign in** (bottom right of the main menu), and choose **Create account**.

If you can't link the project in step 1, put the project ID (from the dashboard's project settings) in a text file at
`Assets/Spheres/Resources/SpheresOnline.txt` instead.

## How it works

- **Sign in / create account:** username 3-20 characters (letters, numbers, `. - @ _`). Password 8-30 characters with an
  upper-case letter, a lower-case letter, a number and a symbol.
- **Staying signed in:** the game keeps a session token in PlayerPrefs and signs back in at launch. Passwords are never stored.
- **Syncing:** the newest profile wins. On sign-in, the cloud copy loads if it's newer than the one on this PC. Otherwise this PC's
  copy is uploaded. After that, every save is backed up a few seconds later. **Sync now** forces it.
- **Signing out:** the profile stays on the PC as a guest profile.
- **Online name:** your username fills in your online name unless you've already set one.

Data stored: the account (username, hashed password) in UGS Authentication, and the profile JSON in UGS Cloud Save under
the key `profile`. See docs/PRIVACY.md.
