# Project Legacy — Development Roadmap

## Phase 1 — Foundation (Current)

Core systems and basic input. Goal: walk around and look at the world in portrait mode.

- [ ] Repository setup and architecture documentation
- [ ] LegacyBootstrapper — system initialization and lifecycle management
- [ ] PortraitModeManager — orientation lock, FOV adjustment, camera framing
- [ ] ScreenLayoutResolver — safe area detection and aspect ratio handling
- [ ] LegacySettings — preferences persistence layer
- [ ] TouchZoneManager — screen zone splitting and touch routing
- [ ] FloatingJoystick — movement input with visual feedback
- [ ] LookHandler — camera control from touch input
- [ ] Basic gesture recognition (tap to interact, drag to look)

**Milestone**: Player can move and look around in portrait mode using touch controls.

---

## Phase 2 — Combat & Core HUD

Swipe combat, aim assist, and the in-game HUD. Goal: engage in combat with touch gestures.

- [ ] GestureRecognizer — full gesture state machine (tap, double-tap, swipe, long-press)
- [ ] SwipeDirectionMapper — gesture to weapon swing direction mapping
- [ ] SwipeCombatHandler — bridge gestures to DFU WeaponManager
- [ ] AutoAimController — soft aim assist for touch combat
- [ ] LockOnSystem — target lock with visual indicator and camera tracking
- [ ] PortraitHUD — root HUD controller with visibility toggle
- [ ] VitalsBar — compact HP/MP/ST display with animations
- [ ] CompassStrip — top compass bar with heading display
- [ ] QuickSlotStrip — 4-slot quick access bar
- [ ] ActionButtonPanel — optional floating action buttons

**Milestone**: Player can fight enemies using swipe gestures with aim assist and see health/mana/stamina.

---

## Phase 3 — Startup & Settings

Mobile-native startup experience and full settings UI. Goal: polished first-run and configuration.

- [ ] MobileStartupController — new main menu replacing DFU's startup screens
- [ ] SetupWizard — first-run experience (data setup, control tutorial, preferences)
- [ ] SaveSlotCarousel — horizontal save file browser with previews
- [ ] SettingsPanel — comprehensive settings organized by category
- [ ] LegacySettings integration — persist and load all preferences
- [ ] Button layout editor — drag to reposition, resize, adjust opacity

**Milestone**: Clean startup flow, save management, and full settings control.

---

## Phase 4 — UI Panel Rework

All game panels redesigned for portrait scrolling and touch interaction.

- [ ] MobileInventoryPanel — grid layout, category tabs, bottom sheet details
- [ ] MobileCharacterSheet — scrollable single-column stats and skills
- [ ] MobileMapPanel — full-screen pinch-to-zoom map
- [ ] MobileDialoguePanel — bottom sheet dialogue with large response buttons
- [ ] MobileSpellbookPanel — scrollable list with quick slot assignment
- [ ] MobileTravelMap — tap-to-travel with confirmation bottom sheet
- [ ] BottomSheet component — reusable sliding panel with snap points
- [ ] SwipePanel component — swipe-to-dismiss panel behavior

**Milestone**: All game UI is usable and comfortable in portrait mode.

---

## Phase 5 — Polish

Performance, accessibility, and device compatibility. Goal: release-quality experience.

- [ ] HapticFeedback integration — tactile feedback on combat, UI, interactions
- [ ] DeviceProfile — adaptive defaults based on device capabilities
- [ ] Performance optimization pass — target 30fps stable on mid-range devices
- [ ] Aspect ratio testing across 16:9, 18:9, 19.5:9, 20:9, 21:9+
- [ ] Notch / punch-hole / camera cutout testing across device models
- [ ] Gesture navigation bar compatibility testing
- [ ] One-handed reachability audit — ensure all critical controls are thumb-accessible
- [ ] Accessibility pass:
  - [ ] Text sizes meet minimum readability standards
  - [ ] Color contrast ratios meet WCAG AA
  - [ ] All touch targets ≥ 48dp
  - [ ] Screen reader compatibility where feasible
- [ ] Battery usage optimization
- [ ] Memory footprint audit
- [ ] Loading time optimization

**Milestone**: Stable, polished, accessible portrait Daggerfall experience on Android.

---

## Future Considerations

These items are not currently planned but may be explored after Phase 5:

- **Bluetooth controller support** — map gamepad input to DFU controls
- **Tablet layout** — alternative HUD arrangement for larger screens
- **Mod compatibility layer** — ensure popular DFU mods work with Legacy UI
- **Cloud save sync** — backup saves to cloud storage
- **Streaming support** — touch overlay for game streaming services
- **iOS port** — if demand exists (requires platform-specific input/haptic work)
