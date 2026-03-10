# STARFISH_STATE.md
## Source of truth for Project Starfish. Read this FIRST every session.

### Project
- **Name**: Project Starfish (working title)
- **Engine**: Phaser 3 + Vite
- **Target**: Mobile-first, portrait mode (9:16)
- **Genre**: 2D side-scroller, 4-directional movement
- **Version**: 0.1.0

### Architecture
```
src/
  main.js              — Entry point, boots Phaser
  config/
    gameConfig.js      — Game dimensions, physics, render settings
  scenes/
    BootScene.js       — Asset loading
    GameScene.js       — Main gameplay
  entities/            — Player, enemies, NPCs (future)
  systems/             — Input, camera, collision systems (future)
public/
  assets/
    sprites/           — Sprite sheets
    audio/             — Sound effects, music
    maps/              — Level data
```

### Current State
- [x] Project scaffolded (Vite + Phaser)
- [x] Portrait mode config (360x640, FIT scaling)
- [x] BootScene with loading bar
- [x] GameScene with placeholder player, 4-dir movement
- [x] Keyboard + touch input
- [x] Camera follow + world bounds
- [x] Dev grid overlay
- [ ] Sprite art for player
- [ ] Level design / tilemap
- [ ] Enemies / NPCs
- [ ] UI / HUD

### Deploy
- **Dev**: `npm run dev` (Vite dev server, port 3000)
- **Build**: `npm run build` (outputs to dist/)
- **Repo**: github.com/Lattymoy/project-starfish

### Rules
- Read this file first every session.
- No single-file architecture. Keep modules small and focused.
- Fix root causes, not symptoms.
- No feature removal without explicit permission.
