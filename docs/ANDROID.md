# Spheres on Android

Spheres runs on Android 8.0 or later. It needs a 64-bit phone or tablet, which covers anything from the last several years.

## Making the APK (on your PC)

1. Open **Unity Hub > Installs**. Click the gear next to 6000.0.85f2, choose **Add modules**, and tick **Android Build Support**. Also tick the **OpenJDK** and **Android SDK & NDK Tools** boxes under it. Install, then reopen the project.
2. In Unity, choose **Spheres > Build > Android (APK)**. You get `Builds/Android/Spheres-vX.apk`.
   - The first Android build is slow, around 10–20 minutes, because Unity converts the game to native code (IL2CPP). Later builds are faster.
   - Switching between Android and PC builds re-imports the project, which also takes a while.

## Putting it on a phone

- Send the .apk to the phone (Google Drive, Discord, USB, and so on) and tap it.
- Android asks you to allow installing apps from that source. Allow it, then tap **Install**.
- Google Play Protect may warn about an unknown developer. Tap **More details > Install anyway**.

## Updates

Upload the new .apk to the same GitHub release as the PC zips. The app notices the new version and offers **Update**. That downloads the APK in the browser. Tap it to install over the old version, and saves are kept.

The version number must go up for Android to accept an update. Spheres > Publish Update bumps the version, so build the APK after publishing.

## Controls

- **Left half of the screen:** a floating joystick that appears wherever your thumb lands.
- **Right half:** drag to look around.
- **FIRE:** hold to shoot. You can drag on it to aim while you fire.
- **JUMP, BLOCK and R (reload):** next to FIRE.
- **TAB:** scoreboard and stats, at the top.
- **II:** pause, at the top. The phone's Back button pauses too.
- **Menus:** tap to press buttons, and drag with a finger to scroll lists.
- **Controllers:** Bluetooth gamepads work too, and Bluetooth keyboards work like on a PC.

## Differences from PC

- **Code mods:** BepInEx mods don't work on Android. JSON card and map mods still do.
- **Steam and Workshop:** not available, since Steam is PC-only.
- **Online play:** works with PC players. Join by IP like on PC. Phone data usually can't host, so let a PC or a phone on Wi-Fi host.
- **Map editor:** it opens, but it's built for mouse and keyboard.
- **Graphics:** phones start on Medium graphics and 60 fps. You can change both in Options.

## Play Store

To release on Google Play:

- Untick `EditorUserBuildSettings.buildAppBundle = false` in `ConfigureAndroid` so Unity builds an .aab.
- Create a signing keystore under Project Settings > Player > Publishing Settings.
- Register a Google Play developer account ($25, one time).
