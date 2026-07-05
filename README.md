# MistalCrysMap

`MistalCrysMap` is a BepInEx / Harmony map overlay mod for **Casualties Unknown Demo**.

Current scope: single-player only. Multiplayer and teammate display are intentionally left for a later version.

## Features

- Shows a minimap in the top-right corner by default.
- Press `M` by default to show or hide the minimap.
- Drag the minimap with left mouse button to move it.
- Scroll while hovering the minimap to zoom it.
- Right-click the minimap to open the large map.
- Press the configured map toggle key to close the large map.
- Scroll over the large map to zoom.
- Hold right mouse button on the large map to pan around.
- Full-map reveal is enabled by default.
- Optional `Exploration Map` mode hides terrain until the player explores it.
- Exploration mode reveals a 40-block-radius circle around the player.
- Hazard markers are hidden in unexplored fog.
- Interactable objects and containers are shown as brighter gray-blue squares.
- Traders are shown as green `$` markers.
- Crystal enemies and animal enemies are shown as small red squares and their marker position follows movement.
- Thornback Elder is shown as its own boss marker and can be toggled separately on the large map.
- Dropped items are shown as persistent yellow dots.
- The large map header can toggle interactables, traps, traders, enemies, Thornback Elder, item drops, and sprite icons independently.
- Markers can be drawn with in-game sprites when a suitable `SpriteRenderer` is available, with a simple marker fallback.
- Traders use a dedicated map icon in sprite mode because their scene sprite can resolve to a blank frame.
- Large-map sprite icon size can be adjusted from the large map header.
- The minimap uses a local non-allocating physics scan for nearby markers; the large map uses the fuller world scan.
- Minimap shape can be changed between rectangle, square, and circle.
- Sprite icons are available as an opt-in large-map toggle and are disabled by default.
- Minimap size defaults to 1.25x and can be scaled from 0.5x to 3x.

## Settings

Settings location: `Settings -> Game`

| Setting | Default | Description |
| --- | --- | --- |
| `MistalCrysMap` | Enabled | Master switch for this map overlay. |
| `Map Toggle Key` | `M` | Shows or hides the minimap. |
| `Exploration Map` | Disabled | Hides terrain until explored. Hazard markers remain visible inside revealed areas. |
| `Minimap Shape` | `Square` | Changes the minimap frame shape. |
| `Minimap Size` | `1.25x` | Scales the minimap frame size from 0.5x to 3x. |

## Controls

| Control | Action |
| --- | --- |
| `M` | Toggle minimap. Closes the large map while it is open. |
| Left-drag minimap | Move minimap position. |
| Mouse wheel over minimap | Zoom minimap. |
| Right-click minimap | Open large map. |
| Map toggle key on large map | Close large map. |
| Mouse wheel over large map | Zoom large map. |
| Right-drag large map | Pan large map. |

## Build

```powershell
dotnet build MistalCrysMap.csproj -c Release /p:GameManagedDir="E:\SteamLibrary\steamapps\common\Casualties Unknown Demo\CasualtiesUnknown_Data\Managed" /p:BepInExCoreDir="E:\SteamLibrary\steamapps\common\Casualties Unknown Demo\BepInEx\core"
```

Output:

```text
bin/Release/MistalCrysMap.dll
```

Place the DLL into:

```text
BepInEx/plugins/
```

## Notes

- The terrain map reads `WorldGeneration` data from the running game.
- Terrain texture refreshes are throttled during rapid block updates such as earthquakes.
- Opening the large map or changing a marker filter now triggers an immediate one-shot marker refresh; normal background refresh remains staggered.
- The first version does not depend on KrokMP.
- Minimap position and zoom are stored locally with Unity `PlayerPrefs`; shape and size live in the game settings page.
