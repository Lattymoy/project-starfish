# Project Legacy — Technical Architecture

## Design Principles

1. **Portrait-first**: Every system assumes a 9:16+ aspect ratio. Landscape mode is not supported. All UI layout, touch zones, and camera framing are optimized for a tall, narrow viewport.

2. **Overlay architecture**: Project Legacy code lives entirely under `Assets/ProjectLegacy/` and hooks into DFU via its existing mod/event system where possible, or minimal patches to core DFU scripts where necessary. This keeps upstream merges manageable and allows vanilla DFU behavior to remain intact when Legacy systems are not active.

3. **Minimal clutter**: The HUD shows only what's needed. No button you don't use. Information appears contextually — numeric health values show on tap, spell details expand on selection, item info appears in a bottom sheet.

4. **One-hand friendly**: Core gameplay (walk, look, fight) is possible with one thumb. Two hands are better but not required. All critical controls are within thumb reach in the lower portion of the screen.

5. **Mobile-native feel**: Gestures, haptics, bottom sheets, carousels — patterns Android users already expect. No desktop UI paradigms forced onto a touchscreen.

---

## System Architecture

### Core Systems

#### LegacyBootstrapper

`ProjectLegacy.Core.LegacyBootstrapper`

MonoBehaviour singleton that initializes on scene load with `[DefaultExecutionOrder(-100)]`. This is the **single entry point** for all Project Legacy systems — if this doesn't run, vanilla DFU behavior is preserved.

Responsibilities:
- Detects if running on Android (`Application.platform == RuntimePlatform.Android`)
- Activates portrait mode via `PortraitModeManager`
- Initializes the input system (`TouchZoneManager`, `FloatingJoystick`, `LookHandler`, `GestureRecognizer`)
- Replaces the DFU HUD with `PortraitHUD`
- Registers mobile UI panel replacements
- Provides a static `Instance` reference for other systems
- Handles `OnDestroy` cleanup to restore vanilla state

#### PortraitModeManager

`ProjectLegacy.Core.PortraitModeManager`

Manages orientation lock and camera configuration for portrait display.

Responsibilities:
- Locks screen orientation to `ScreenOrientation.Portrait`
- Adjusts `Camera.main.fieldOfView` to 55° (configurable, range 45°–65°)
- Caches and restores original FOV on disable
- Adjusts weapon FPS rendering offset so the weapon doesn't obscure the narrow viewport
- Handles orientation change events (defensive, since portrait is locked)
- Triggers HUD layout swap when activated

#### LegacySettings

`ProjectLegacy.Core.LegacySettings`

ScriptableObject + PlayerPrefs wrapper that persists all user preferences.

Stored settings:
- Control sensitivity (look, move)
- Button positions, sizes, and opacity
- Auto-combat preferences (auto-aim strength, lock-on behavior)
- HUD toggle states (which elements are visible)
- Haptic feedback on/off and intensity
- Quick slot assignments
- Graphics quality overrides

Exposes a settings UI panel for runtime configuration.

#### ScreenLayoutResolver

`ProjectLegacy.Core.ScreenLayoutResolver`

Reads device screen properties and provides safe layout information to all UI systems.

Responsibilities:
- Reads `Screen.safeArea` to detect notch/cutout insets
- Detects gesture navigation bar height
- Provides `Rect` values for UI positioning that respect all insets
- Handles aspect ratios from 16:9 to 21:9+ (foldables, ultra-tall phones)
- Recalculates on screen geometry changes

---

### Input Systems

#### TouchZoneManager

`ProjectLegacy.Input.TouchZoneManager`

Divides the screen into functional touch zones and routes input to the appropriate handler.

Zone layout:
- **Look Zone**: Top ~60% of the viewport area (above the HUD)
- **Move Zone**: Bottom ~40% of the viewport area
- Zone split percentage is configurable

Responsibilities:
- Each frame, iterates `Input.touches`
- Routes each touch to `LookHandler` or `FloatingJoystick` based on `touch.position.y` relative to the zone boundary
- Tracks finger ID → zone assignment (once a finger starts in a zone, it stays in that zone)
- Supports simultaneous look + move (two fingers in different zones)

#### FloatingJoystick

`ProjectLegacy.Input.FloatingJoystick`

Virtual joystick that appears at the user's touch point in the Move Zone.

Behavior:
- On touch begin: record `origin = touch.position` — this is the joystick center
- On touch move: calculate `delta = current - origin`, clamp to `maxRadius` (default 80px)
- Output: normalized direction vector and magnitude (0–1)
- Feeds into DFU's `InputManager.Horizontal` / `InputManager.Vertical` axes
- Renders a subtle visual (two concentric circles) at the touch point
- On touch end: zero output, hide visual

#### LookHandler

`ProjectLegacy.Input.LookHandler`

Converts touch movement in the Look Zone to camera rotation.

Behavior:
- Tracks touch delta (frame-to-frame position change)
- Converts delta to pitch/yaw rotation
- Applies configurable sensitivity curve (linear, accelerated, or custom AnimationCurve)
- Feeds into DFU's mouse-look system by overriding `InputManager` mouse delta values
- Respects inverted Y-axis preference

#### GestureRecognizer

`ProjectLegacy.Input.GestureRecognizer`

Finite state machine that classifies touch sequences in the Look Zone into discrete gestures.

States: `Idle` → `TouchBegan` → `PossibleTap` / `Swiping` / `LongPressing`

Recognized gestures:
| Gesture | Condition | Action |
|---------|-----------|--------|
| Tap | < 200ms hold, < 10px movement | Interact / pick up |
| Double-tap | Two taps within 400ms | Quick attack (default swing) |
| Swipe | > 40px movement in < 300ms | Directional weapon swing |
| Long-press | > 500ms hold | Examine / info mode |
| Two-finger tap | Two fingers tap simultaneously | Toggle run/walk |

All thresholds are configurable. Emits events: `OnTap`, `OnDoubleTap`, `OnSwipe(Vector2, float)`, `OnLongPress(Vector2)`.

#### SwipeDirectionMapper

`ProjectLegacy.Input.SwipeDirectionMapper`

Converts swipe gesture vectors to DFU weapon swing directions.

Mapping:
- Swipe up → Upward swing (`WeaponSwingMode.Up`)
- Swipe down → Downward swing (`WeaponSwingMode.Down`)
- Swipe left → Left swing (`WeaponSwingMode.Left`)
- Swipe right → Right swing (`WeaponSwingMode.Right`)
- Forward thrust → Thrust (`WeaponSwingMode.Forward`)

DFU already supports directional swings — this maps touch gestures to those existing inputs.

---

### Combat Systems

#### AutoAimController

`ProjectLegacy.Combat.AutoAimController`

Subtle aim assistance that nudges the player's aim toward nearby enemies.

Behavior:
- When enabled and no lock-on is active, finds the nearest `DaggerfallEnemy` within a configurable cone (default 15° half-angle)
- Applies a soft-aim approach: `Quaternion.Slerp(current, towardEnemy, aimStrength * Time.deltaTime)`
- `aimStrength` is intentionally low (0.05–0.15) for a gentle nudge, not a snap
- Lerps toward target center-mass
- Can be toggled off entirely for purist play
- Runs in `LateUpdate` to apply after player input

#### LockOnSystem

`ProjectLegacy.Combat.LockOnSystem`

Target locking system activated by tapping an enemy.

Behavior:
- Activated by tap on an enemy or long-press for explicit lock
- Uses `Physics.SphereCast` from camera to find lockable targets
- Displays a subtle lock-on indicator (screen-space reticle on the enemy)
- While locked:
  - `AutoAimController` cone widens to ~30°
  - Camera gently tracks the locked target via `Slerp` in `LateUpdate`
  - Player look input becomes an offset from the target tracking
- Lock breaks on: target death, distance > 15m threshold, or player taps elsewhere
- Exposes `CurrentTarget`, `LockOn(target)`, `Release()`, `ToggleLock()` API

#### SwipeCombatHandler

`ProjectLegacy.Combat.SwipeCombatHandler`

Bridges gesture input with DFU's weapon system.

Behavior:
- Listens to `GestureRecognizer` swipe events
- Uses `SwipeDirectionMapper` to convert swipe direction to DFU `WeaponSwingMode`
- Swipe speed maps to attack timing
- During lock-on: swipes always register as attacks on the locked target (no aim required)
- Triggers haptic feedback on hit confirmation
- Calls DFU's `WeaponManager.ScreenWeapon.OnAttackDirection(dir)`

---

### UI — Portrait HUD

#### PortraitHUD

`ProjectLegacy.UI.Portrait.PortraitHUD`

Root controller for the in-game HUD, optimized for portrait display.

Layout:
- **Top**: CompassStrip (thin bar, ~20dp)
- **Bottom**: VitalsBar + QuickSlotStrip + ActionButtonPanel (~80dp)
- **Right edge**: Floating action buttons (optional)

Features:
- Swipe down from top to toggle HUD visibility
- Manages child component lifecycle and visibility
- Total HUD footprint target: ≤ 100dp combined (top + bottom)

#### VitalsBar

`ProjectLegacy.UI.Portrait.VitalsBar`

Compact health/mana/stamina display.

- Three horizontal progress bars in a single row: HP (red), MP (blue), ST (green)
- Compact height: 6dp each
- Tap to show numeric values (current/max)
- Flashes/pulses when values drop below critical thresholds (25%)
- Smooth animated transitions on value changes

#### CompassStrip

`ProjectLegacy.UI.Portrait.CompassStrip`

Directional compass at the top of the viewport.

- Thin bar showing cardinal and intercardinal directions (N, NE, E, SE, S, SW, W, NW)
- Highlights current facing direction
- Reads heading data from DFU's existing compass system
- Scrolls horizontally as player rotates

#### QuickSlotStrip

`ProjectLegacy.UI.Portrait.QuickSlotStrip`

Quick-access item/spell slots in the bottom bar.

- 4 configurable slots
- Drag-to-assign from inventory
- Shows item icon + remaining count
- Tap to use, long-press to unequip/view info
- Visual cooldown indicator for spells

#### ActionButtonPanel

`ProjectLegacy.UI.Portrait.ActionButtonPanel`

Optional floating action buttons for users who prefer explicit buttons over gestures.

- Three primary buttons: Attack, Spell, Use/Interact
- Positioned on the right edge of the viewport for right-thumb reach
- Configurable: positions, sizes, opacity
- Semi-transparent (default 70% opacity)
- All actions are also available via gestures — these are supplementary

---

### UI — Startup Menu

#### MobileStartupController

`ProjectLegacy.UI.StartupMenu.MobileStartupController`

Replaces DFU's default startup screens entirely when portrait mode is active.

Layout (vertical flow):
1. Animated Daggerfall logo
2. "Continue" button (only if a save exists)
3. "New Game" button
4. "Load Game" button
5. "Settings" button
6. "Credits" button

Features:
- Large touch targets (minimum 48dp)
- Animated transitions between screens
- Dark theme matching Daggerfall's aesthetic
- Checks DFU's save system for existing saves

#### SetupWizard

`ProjectLegacy.UI.StartupMenu.SetupWizard`

First-run experience for new users.

Steps:
1. Locate `arena2` data folder
2. Choose graphics quality preset
3. Configure controls (with interactive tutorial)
4. Set preferences (haptics, auto-aim, etc.)

Features:
- Step-by-step vertical cards
- Progress indicator at top
- Can be revisited from Settings

#### SaveSlotCarousel

`ProjectLegacy.UI.StartupMenu.SaveSlotCarousel`

Horizontal swipe carousel for browsing save files.

Each card shows:
- Character name and level
- Current location
- Playtime
- Screenshot thumbnail
- Last-played date

Interactions:
- Swipe to browse saves
- Tap to load
- Long-press for delete/export options

#### SettingsPanel

`ProjectLegacy.UI.StartupMenu.SettingsPanel`

Comprehensive settings UI.

Sections:
- **Display**: FOV, render scale, quality preset
- **Controls**: Sensitivity, zone split, button layout, gesture thresholds
- **Combat**: Auto-aim toggle/strength, lock-on behavior
- **Audio**: Volume sliders (master, music, SFX)
- **Advanced**: Performance tier override, debug options

Features:
- Scrollable single-column layout
- Clear labels with current values
- Appropriate controls (slider, toggle, dropdown)
- No nested menus deeper than 1 level

---

### UI — Game Panels (Reworked for Portrait)

#### MobileInventoryPanel

Full-screen overlay. Items in a scrollable grid (3 columns). Category tabs at top (swipeable). Item detail on tap (bottom sheet). Drag to equip or assign to quick slot. Weight and gold bar at bottom.

#### MobileCharacterSheet

Scrollable single-column layout. Stats, skills, level info, active effects. Collapsible sections for organization.

#### MobileMapPanel

Full-screen. Pinch to zoom, pan to scroll. Toggle between local/automap and travel map. Location markers are tap targets. HUD hidden during map view.

#### MobileDialoguePanel

Bottom sheet style. NPC text scrolls at top. Response options as large buttons at bottom. NPC portrait and name at top of the sheet.

#### MobileSpellbookPanel

Scrollable list of known spells. Tap to select, swipe right to assign to quick slot. Spell detail shown as expandable card.

#### MobileTravelMap

Full-screen map. Tap location to set destination. Confirmation bottom sheet with travel time, travel options (cautious/reckless), and rest options (inn/camp).

---

### UI — Common Components

#### SafeAreaAdapter

Attach to any `RectTransform`. Automatically adjusts padding to respect `Screen.safeArea`. Handles notch/cutout on any edge. Updates on screen geometry changes.

#### MobileButton

Styled button component with mobile-appropriate touch handling.
- Minimum 48dp touch target
- Haptic feedback on press
- Visual feedback: scale animation + color shift
- Configurable icon + label

#### SwipePanel

Panel that can be swiped away to dismiss (left, right, or down). Used for inventory, character sheet, and similar full-screen panels.

#### BottomSheet

Slides up from the bottom of the screen. Used for item details, dialogue, confirmations, and contextual actions.
- Drag handle at top
- Snap points: half-screen, full-screen, dismissed
- Gesture-driven with momentum

---

### Utility Systems

#### HapticFeedback

Wraps Android's `Vibrator` API via a Java plugin bridge or Unity's `Handheld.Vibrate()`.

Patterns:
- Light tap: UI interaction
- Medium tap: combat hit
- Heavy tap: critical hit / block
- Success: quest complete, level up
- Error: failed action

Respects the user's haptic preference setting (on/off/intensity).

#### DeviceProfile

Detects device capabilities at startup and adjusts defaults accordingly.

Detected properties:
- Screen size category: small / medium / large / tablet
- Performance tier: low / mid / high (based on `SystemInfo`)
- Available RAM
- GPU capabilities

Adjusted defaults:
- Button sizes (larger on small screens)
- Render resolution scale (lower on low-tier devices)
- LOD distances
- Effect quality

---

## Integration Strategy with DFU

Project Legacy hooks into the existing DFU codebase at these specific points:

### 1. InputManager Override

`LegacyBootstrapper` replaces DFU's input polling with `TouchZoneManager` output. DFU's `InputManager` reads axis values — we write to those same values from touch input. This is the cleanest integration point since DFU already abstracts input through its `InputManager`.

### 2. HUD Replacement

Disable DFU's `DaggerfallHUD` GameObject. Instantiate the `PortraitHUD` prefab. Our HUD reads the same game state (health, mana, etc.) from `GameManager.Instance.PlayerEntity`.

### 3. UI Panel Hooks

DFU uses `DaggerfallUI.UIManager` to push/pop UI windows. We register our mobile panels as replacements for the default ones using DFU's mod event system where possible.

### 4. Startup Override

Replace DFU's `StartGameBehaviour` flow with `MobileStartupController` when portrait mode is active.

### 5. Camera FOV

Direct modification of `Camera.main.fieldOfView` on startup, with a hook into DFU's camera rig to prevent it from resetting the value.

### 6. WeaponManager

`SwipeCombatHandler` calls the same methods that Vwing's touch input uses — we map different gesture inputs to those existing methods.

### Guiding Principle

**Minimal changes to core DFU files.** Where changes to DFU source are absolutely necessary, they are documented with `// REQUIRES DFU PATCH:` comments and kept as small as possible for merge compatibility.
