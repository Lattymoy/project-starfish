# Project Legacy

**Daggerfall in your pocket — a portrait-first mobile experience.**

Project Legacy is a fork of [dfu-android](https://github.com/Vwing/daggerfall-unity-android) that reworks the entire UX for portrait phone use. It's not a port — it's a reimagining of how *The Elder Scrolls II: Daggerfall* should feel on a touchscreen, built on top of [Daggerfall Unity](https://github.com/Interkarma/daggerfall-unity).

## Key Features (In Development)

- **Portrait-first orientation** with optimized FOV and camera framing
- **Zone-based touch controls** — look zone (top) and move zone (bottom) with a floating joystick
- **Swipe-to-slash combat** with auto-aim and optional lock-on targeting
- **Redesigned startup menu** built for mobile — large touch targets, animated transitions, setup wizard
- **Compact HUD** with vitals bars, compass strip, and quick-access item slots
- **All UI panels reworked** for portrait scrolling and touch — inventory, map, dialogue, spellbook, and more
- **Safe area / notch / gesture nav support** — works on every modern Android device
- **Haptic feedback** — feel your hits, interactions, and UI actions
- **One-hand friendly** — core gameplay is possible with a single thumb

## Status

**Early development — not playable yet.**

The project is in the foundation phase. Core systems are being built and the architecture is being validated. See [ROADMAP.md](docs/ROADMAP.md) for the development plan.

## Based On

- [Daggerfall Unity](https://github.com/Interkarma/daggerfall-unity) by Interkarma — the open-source recreation of Daggerfall
- [dfu-android](https://github.com/Vwing/daggerfall-unity-android) by Vwing — Android port of DFU with on-screen controls

## Requirements

- **Android device** running Android 8.0 (API 26) or higher
- **Free copy of DOS Daggerfall** — available from [Steam](https://store.steampowered.com/app/1812390/The_Elder_Scrolls_II_Daggerfall/) or [Bethesda.net](https://elderscrolls.bethesda.net/en/daggerfall)
- The `arena2` data folder from the DOS install

## Building from Source

See [SETUP.md](docs/SETUP.md) for full developer environment setup instructions.

**Quick version:**

1. Clone the dfu-android fork
2. Clone Project Legacy into `Assets/ProjectLegacy/`
3. Open in Unity **2022.3.30f1** with Android Build Support
4. Place your `arena2` data folder
5. Build and run

## Project Structure

```
Assets/ProjectLegacy/
├── Scripts/
│   ├── Core/        # Bootstrapping, portrait mode, settings, screen layout
│   ├── Input/       # Touch zones, joystick, look, gestures, swipe mapping
│   ├── Combat/      # Auto-aim, lock-on, swipe combat
│   ├── UI/
│   │   ├── Portrait/    # HUD: vitals, compass, quick slots, action buttons
│   │   ├── StartupMenu/ # Mobile main menu, setup wizard, save carousel
│   │   ├── Panels/      # Reworked inventory, map, dialogue, spellbook panels
│   │   └── Common/      # Safe area, buttons, swipe panels, bottom sheets
│   └── Util/        # Haptics, device profiling
├── Prefabs/         # UI prefabs (built in Unity editor)
└── Resources/       # ScriptableObject defaults
```

All Project Legacy code lives under the `ProjectLegacy` namespace and is isolated from core DFU code, making upstream merges manageable.

## Architecture

Project Legacy uses an **overlay architecture** — it hooks into DFU's existing systems (InputManager, UIManager, WeaponManager) rather than modifying them directly. The single entry point is `LegacyBootstrapper`, which detects Android at runtime and activates all portrait-mode systems. If it doesn't run, vanilla DFU behavior is preserved.

See [ARCHITECTURE.md](docs/ARCHITECTURE.md) for full technical details.

## License

[MIT](LICENSE) — matching Daggerfall Unity's license.

## Contributing

Contributions are welcome! Please read:

- [ARCHITECTURE.md](docs/ARCHITECTURE.md) — understand the technical design
- [ROADMAP.md](docs/ROADMAP.md) — see what's planned and where help is needed
- [CONTROLS.md](docs/CONTROLS.md) — understand the touch control scheme

Open an issue to discuss before starting large changes.
