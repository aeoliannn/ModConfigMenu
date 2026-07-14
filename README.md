# Mod Config Menu

## Description

Mod Config Menu adds an in-game UI to access and edit mod configs. This only works for mods using the BepInEx config format.

## Main Features

* Adds a **"Mods"** button to the Main Menu and Pause Menu screen, allowing configs to be edited while in-game.
* The default keybind is **F5**.
* Should work with any mod that uses the default BepInEx config format.
* Applies config changes retroactively. Whether changes require a restart depends entirely on the mod being changed.
* Supports:

  * Input Boxes
  * Sliders
  * Toggles
  * Arrow Selection
  * Keybinds

### Control Type Detection

| `SettingType` | Condition | Control |
|---|---|---|
| `bool` | - | Toggle (SpriteSwap) |
| `int` | `AcceptableValueRange<int>` present | Slider |
| `int` | No range | Text input (IntegerNumber) |
| `float` | `AcceptableValueRange<float>` present | Slider |
| `float` | No range | Text input (DecimalNumber) |
| `string` | `AcceptableValueList<string>` present | Dropdown (arrow cycler) |
| `string` | No list | Text input (Standard) |
| `KeyboardShortcut` / `KeyCode` | - | Keybind (click-to-capture) |

## Compatibility

* **BepInEx:** 5.4.x
* **Travellers Rest:** v0.7.5.3.0

## Credits

* **Framework:** BepInEx by bbepis
* **Inspiration:** Generic Mod Config Menu by spacechase0
