# Project Legacy — Touch Control Scheme

## Touch Zones

The screen is divided into two primary input zones:

```
┌─────────────────────┐
│   Compass Strip     │  ← Top bar (~20dp)
├─────────────────────┤
│                     │
│                     │
│     LOOK ZONE       │  ← Top ~60% of viewport
│   (camera + combat) │     Drag to look, swipe to attack
│                     │
│                     │
├─────────────────────┤
│                     │
│     MOVE ZONE       │  ← Bottom ~40% of viewport
│  (floating joystick)│     Touch and drag to move
│                     │
├─────────────────────┤
│  Vitals │ QuickSlots│  ← Bottom bar (~80dp)
└─────────────────────┘
```

- Zone split percentage is configurable in Settings
- Zones are relative to the viewport area (excluding HUD bars)
- Each finger is assigned to a zone on first touch and stays there until released

---

## Gesture Reference

### Look Zone Gestures

| Gesture | Condition | Action | Notes |
|---------|-----------|--------|-------|
| Drag | Touch + move | Camera rotation (look around) | Sensitivity configurable |
| Tap | < 200ms hold, < 10px movement | Interact / Pick up | Same as DFU's activate |
| Double-tap | Two taps within 400ms | Quick attack (default swing) | Uses current weapon |
| Swipe left | > 40px, < 300ms | Left weapon swing | Horizontal slash |
| Swipe right | > 40px, < 300ms | Right weapon swing | Horizontal slash |
| Swipe up | > 40px, < 300ms | Upward weapon swing | Overhead strike |
| Swipe down | > 40px, < 300ms | Downward weapon swing | Downward chop |
| Long-press | > 500ms hold | Examine target | Shows info tooltip |
| Two-finger tap | Both fingers < 200ms | Toggle run/walk | Movement speed toggle |

### Move Zone Gestures

| Gesture | Condition | Action | Notes |
|---------|-----------|--------|-------|
| Touch + drag | Any touch in zone | Movement (floating joystick) | Joystick spawns at touch point |
| Swipe up from edge | Start within 50px of bottom | Open quick inventory | Edge gesture |

### HUD Gestures

| Gesture | Target | Action | Notes |
|---------|--------|--------|-------|
| Tap | Quick slot | Use item/spell | Activates assigned item |
| Long-press | Quick slot | Unequip / show info | Opens item detail |
| Tap | Vitals bar | Show numeric values | Displays HP/MP/ST numbers |
| Swipe down | Top screen edge | Toggle HUD visibility | Show/hide all HUD elements |

### Lock-On Gestures

| Gesture | Condition | Action | Notes |
|---------|-----------|--------|-------|
| Tap enemy | During gameplay | Lock on to target | Reticle appears on enemy |
| Tap elsewhere | While locked on | Release lock-on | Returns to free look |
| Long-press enemy | During gameplay | Explicit lock-on | Alternative activation |

---

## Auto-Aim & Lock-On

### Auto-Aim (Soft Aim Assist)

- **Always active** by default (can be disabled in Settings)
- Detection cone: **15° half-angle** from camera center
- Behavior: Subtle nudge toward nearest enemy center-mass
- Strength: Very low — aim is guided, not snapped
- Purpose: Compensates for imprecise touch aiming on a small screen
- Does not interfere with player control — release touch and aim stops adjusting

### Lock-On Targeting

- **Activation**: Tap on a visible enemy, or long-press for explicit lock
- **Visual indicator**: Reticle/highlight on the locked target
- **Camera tracking**: Camera gently follows the locked target; player look input becomes an offset
- **Enhanced auto-aim**: While locked, aim-assist cone widens to ~30°
- **Free movement**: Player can move freely while locked on — camera maintains target tracking

**Lock-on breaks when:**
- Target dies
- Distance exceeds 15 meters
- Player taps elsewhere
- Player explicitly releases (via UI button or gesture)

### Swipe-to-Slash During Lock-On

When locked on to a target:
- **No aim required** — attacks automatically target the locked enemy
- Swipe direction determines swing direction
- Focus shifts to **timing and rhythm** rather than precision aiming
- Swipe speed influences attack speed
- Haptic feedback confirms hits

---

## Button Overlay (Optional)

For users who prefer explicit buttons over gesture-only play, an optional button overlay is available:

### Right Side (Primary Actions)
- **Attack** — triggers default weapon swing
- **Spell** — casts assigned spell
- **Use / Interact** — activates targeted object/NPC

### Left Side (Utility)
- **Menu** — opens game menu
- **Map** — opens map panel

### Bottom-Left (Movement Modifiers)
- **Jump** — jump action
- **Sneak** — toggle sneak mode

### Button Configuration

All overlay buttons are fully configurable:
- **Repositionable**: Drag to any screen position
- **Resizable**: Adjust from small to large
- **Opacity**: 0% (invisible, touch-only) to 100% (fully opaque)
- **Visibility**: Individual buttons can be shown or hidden
- **Layout presets**: Right-handed, left-handed, or custom

Buttons can be **fully hidden** for a gesture-only experience. All actions that buttons provide are also available through the gesture system.

---

## One-Handed Play

Core gameplay is designed to be playable with a single thumb:

1. **Look**: Drag in the upper portion of the screen
2. **Move**: Drag in the lower portion — joystick appears at thumb position
3. **Attack**: Swipe in the look zone
4. **Interact**: Tap in the look zone

For extended play sessions, two hands are recommended. The second hand allows simultaneous look + move and faster combat inputs.

---

## Configurable Thresholds

All gesture thresholds can be adjusted in Settings → Controls:

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Look sensitivity | 1.0 | 0.1–3.0 | Camera rotation speed multiplier |
| Zone split | 60/40 | 50/50–80/20 | Look zone / Move zone percentage |
| Tap threshold (time) | 200ms | 100–400ms | Max duration for tap gesture |
| Tap threshold (distance) | 10px | 5–20px | Max movement for tap gesture |
| Swipe threshold (distance) | 40px | 20–80px | Min movement for swipe gesture |
| Swipe threshold (time) | 300ms | 150–500ms | Max duration for swipe gesture |
| Long-press threshold | 500ms | 300–1000ms | Min hold time for long-press |
| Double-tap window | 400ms | 200–600ms | Max gap between taps for double-tap |
| Joystick max radius | 80px | 40–120px | Maximum drag distance for joystick |
| Auto-aim strength | 0.10 | 0.0–0.25 | Aim assist nudge intensity |
| Lock-on break distance | 15m | 10–25m | Distance at which lock-on releases |
