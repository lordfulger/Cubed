# Example Data Pack

A Cubed mod made only of JSON files - no DLL, no programming.

* `cards/example.cubedcards.json` - three cards built from stat modifiers and built-in effects.
* `blocks/example.cubedblocks.json` - a "Sludge" block (slow, mildly harmful) and a "Super Spring".
* `maps/sludge_pit.cubedmap.json` - a small arena that uses both.

Cubed scans every folder under `BepInEx/plugins`, so this pack works when installed with a mod manager or
extracted by hand. It also works without BepInEx when copied to the game's user `Content` folder
(see the Mods screen in game for the exact path).
