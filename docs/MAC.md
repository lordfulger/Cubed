# Spheres on Mac

Spheres runs on macOS 11 or later, on both Intel and Apple Silicon Macs. The build is a universal app.

## Making a Mac build (you can do this from Windows)

1. Open **Unity Hub > Installs**. Click the gear next to Unity 6000.0.85f2, choose **Add modules**, and tick **Mac Build Support (Mono)**. Install it, then reopen the project.
2. In Unity, choose **Spheres > Build > macOS**. You get `Builds/macOS/Spheres.app`.
3. Or choose **Spheres > Publish Update (Windows + Mac)**. It builds both and makes `Spheres-Windows-vX.zip` and `Spheres-macOS-vX.zip`. Upload **both** zips to the GitHub release. The auto-updater on each computer picks the zip for its own platform.

The Mac zip is made so the app stays runnable even though it was zipped on Windows.

## Friends opening it for the first time

The app isn't signed by Apple, so macOS warns about it the first time:

- Unzip it and drag **Spheres.app** into Applications.
- **Right-click > Open**, then click **Open** in the warning. You only need to do this once.
- If macOS says the app "is damaged", run this in Terminal:
  `xattr -dr com.apple.quarantine /Applications/Spheres.app`

To get rid of the warning completely, you need an Apple Developer account ($99/year). Then you sign and notarize the app.

## Updates

On a Mac, the auto-updater downloads the macOS zip. It replaces Spheres.app where it is, clears the quarantine flag and restarts the game. If Spheres is in /Applications and your account isn't an admin, the update can fail. If it does, download the zip again by hand.

## Notes

- **Online play:** Mac and Windows players can play together. The network protocol is the same.
- **Gamepads:** Xbox, PlayStation and Switch Pro controllers have Mac button layouts.
- **Steam:** add the macOS `steam_api.bundle` from the Steamworks SDK next to the Windows DLL. See STEAM.md.
- **Mods (BepInEx):** use the macOS BepInEx 5 build and start the game with `run_bepinex.sh`. r2modman handles this for you.
- **Saves, bug reports and logs** are in `~/Library/Application Support/<company>/Spheres/`.
