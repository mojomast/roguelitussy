# Visual Validation And Art Direction

## Real Runtime Captures

Stub tests validate simulation and explicit UI geometry, not font shaping, minimum sizes, inherited tint, or actual draw order. Use Godot 4.5.2 Mono with a real renderer to inspect those behaviors. The headless dummy renderer cannot produce representative screenshots.

`Scenes/Tests/VisualCapture.tscn` is an opt-in development fixture, not the main scene. It loads the playable shell, starts seed 1337, opens inventory and conversations, and visits depths 1, 4, and 7. NPC dialogs are reached through normal `F` routing after test-only positioning; the fixture does not invoke paid treatments. It saves viewport PNGs after layout and drawing settle, plus JSON containing actual label rectangles and minimum sizes. `--assert-layout` fails when a visible label extends outside its parent. This does not prove arbitrary sibling panels never overlap; the compact gameplay tests separately cover the HUD/log allocation.

Example on Linux with Xvfb and the .NET SDK available:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export GODOT_BIN="/path/to/Godot_v4.5.2-stable_mono_linux.x86_64"
dotnet build godotussy.csproj -p:UseGodotStubs=false -p:RoguelitussyWarningsAsErrors=true
"$GODOT_BIN" --headless --editor --path . --quit
XDG_DATA_HOME=/tmp/roguelitussy-visual-user xvfb-run -a -s "-screen 0 1280x720x24" \
  "$GODOT_BIN" --path . --rendering-method gl_compatibility --audio-driver Dummy \
  --resolution 1280x720 res://Scenes/Tests/VisualCapture.tscn -- \
  --capture-output="$PWD/bin/visual-validation/1280x720" --capture-size=1280x720 --assert-layout
```

Run the same fixture at 640x360 and 1920x1080 by changing the Xvfb screen, resolution, capture size, and output directory. On a desktop, omit `xvfb-run` and retain the real renderer. Isolate user data as shown so test runs do not use normal meta/daily/save files. Captures reveal the map deliberately for art inspection; they are not fog-of-war coverage. The fixture's C# source is excluded from stub compilation because it exercises real viewport readback and font-layout APIs.

The game normally uses a 1920x1080 logical canvas with `canvas_items` stretch and a fixed aspect ratio. Physical-window resizing alone is not the same as testing a smaller logical viewport. `--capture-size` explicitly sets the logical canvas for compact-layout testing; it does not alter `project.godot` defaults.

The capture fixture also records an owned-technique palette and a visible ground-item pile. Review `abilities.png`, `ground-items.png`, and `inventory.png` for exact authored texture paths, pile counts, wall-cover ordering, palette readability, and icon/badge overlap. The fixture uses a generated Vanguard/Human run, so it demonstrates Shield Bash plus War Cry; class/race matrix selection remains covered by simulation/UI tests. Player world bodies should retain the source portrait palette, while character-creation preview continues to show identity-specific visual detail.

## Current Art Provenance

- Active pack: [0x72 DungeonTileset II](https://0x72.itch.io/dungeontileset-ii), by Robert Norenberg / 0x72, CC0. Local provenance is `Assets/0x72_source.txt`.
- The committed subset came through [the recorded GDevelop mirror](https://github.com/4ian/GDevelop/issues/2849). Its exact upstream version/checksum is not recorded; do not describe it as the current v1.7 release.
- The current renderer uses individually named PNGs rather than the newer upstream autotile atlas. Its 30 terrain sprites include clean/cracked floors, walls/corners, ladder, doors, and frames.
- Historical [Kenney Tiny Dungeon](https://kenney.nl/assets/tiny-dungeon) provenance remains in the repository, but the old terrain crops were removed. It is not a second installed biome pack.

## Floor Identity Without Alignment Rework

The first visual pass deliberately retains the installed art and its geometry:

- Depths 0-3: slate/rust prison stone.
- Depths 4-6: cooler stone, bone edges, and teal crypt fascia.
- Depths 7+: warm ash, red stone, and ember edges.
- Existing crack variants vary deterministically by coordinate and depth without consuming gameplay RNG.

These are terrain palettes, not three newly imported tilesets. Apply tint to individual terrain sprites and matching cover pieces, never shared cached textures or the entire world node. Actors, labels, targeting, and combat effects must keep their own colors.

Keep the current 16-pixel source to 40-unit cell scale, centered tile sprites, wall-base-under-cap layering, north fascia dimensions, corner-strip region `(0, 12, 16, 4)`, and existing Z ordering. Contextual wall tiles are structural edges, not random cosmetic alternatives.

## Candidate Extensions

The author's [DungeonTileset II Sewers](https://0x72.itch.io/16x16-dungeontileset-ii-sewers) is the closest coherent new-biome candidate. Its page offers tall/short wall variants, terrain, and animated characters, and the author confirms CC0/commercial use in the discussion. Download access currently costs $2 or more. It has not been purchased or imported by this change; use a legitimately obtained archive before integration.

The base pack also links community extensions, including [DungeonTileset II Extended](https://nijikokun.itch.io/dungeontileset-ii-extended). Treat these as candidates, not pre-approved drop-ins: verify the specific archive's license and source dimensions before adding files.

The author discusses the same wall-alignment difficulty in [this Godot/autotile discussion](https://itch.io/post/11557009): newer autotiles put some vertical walls in the middle of cells and require half-floor pieces. Replacing the existing renderer with that atlas would reopen the alignment problem. For a future extension, map each new sprite to the current semantic floor/wall/edge/door roles in a small test scene first. Compare corners, narrow corridors, doors, and actors against north walls at each target resolution before changing gameplay defaults.

## UI Practices Used

- [Godot 4.5 Label](https://docs.godotengine.org/en/4.5/classes/class_label.html): explicit fonts, `ClipText`, and `TextOverrunBehavior.TrimEllipsis` for bounded single-line controls instead of guessing character widths.
- [Godot 4.5 RichTextLabel](https://docs.godotengine.org/en/4.5/classes/class_richtextlabel.html): bounded, wrapping/scrollable prose with `FitContent=false`; choices and footers have separate space.
- [Godot 4.5 TextureRect](https://docs.godotengine.org/en/4.5/classes/class_texturerect.html): `ExpandMode.IgnoreSize` prevents 32px status sources from enlarging an 18px badge slot.
- Use `SelfModulate` for panel-only color. `Modulate` propagates to children and previously tinted parchment text and HP bars unintentionally.
- Window choices using the final card height and actual row height; the selected option must exist as a visible row, not merely in a text snapshot.

Real capture runs currently report CanvasItem/ObjectDB leaks during shutdown. Their source remains a follow-up; successful screenshots and layout assertions do not establish leak-free lifecycle handling or Windows playtesting.
