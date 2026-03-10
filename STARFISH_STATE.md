# STARFISH_STATE.md
## Source of truth for Project Starfish. Read this FIRST every session.

### Project
- **Name**: Project Starfish (working title)
- **Engine**: Phaser 3 + Vite
- **Target**: Mobile-first, portrait mode (9:16)
- **Genre**: Sci-Fi Management Action RPG / Gladiator Management
- **Art**: All canvas-generated, pixel art style
- **Version**: 0.1.0

### Game Design
**Theme**: Sci-Fi Gladiator Management. Players (Founders) run a House of Remnants
(experimental super soldiers) and fight in 1v1 arena combat.

**Core Loop**: House Management → Opponent Selection → Arena Gameplay

**Combat**: Touch-based. Invisible joystick to move, swipe to dodge, auto-attack in range.
1v1 arena fights. Portrait mode.

**Remnant Attributes**:
- Weapon Mastery: Shortbeam (1H sword), Longbeam (2H sword), Longphaser (rifle/melee), Double Phaser (pistols/melee)
- Core: Mega (low HP/high AGI), Giga (balanced), Tera (high HP/low AGI)
- Lifespan: Low (costly upkeep, great loot bonus), Medium (balanced), High (cheap upkeep, no bonus)
- Trait: Starborn, Planetborn, Slumlord, Vestige

**New Game Flow**: Name House → Choose Sigil → Pick 1 of 3 procedural Remnants → House Menu

**House Menu** (panel-based): Remnants, Storage, Manage House, Travel (placeholder), Calendar, Next Cycle, Preparation

**Cycle System**: Weeks. Manage upkeep, pick fights, progress.

### Lore
Beyond the slums of the Kos system and the reaches of the Tes System lies the Perrin Accord.
Overlords command militants to seek prospects for Remnant implantation. Remnants are thrust
into the hands of House holders from across the Scattered Systems. You push these experimental
weapons to the brink to perfect their design. The overlord commands it.

### Architecture
```
src/
  main.js              — Entry point, boots Phaser
  config/
    gameConfig.js      — Game dimensions, physics, render settings
  scenes/
    BootScene.js       — Asset loading
    GameScene.js       — Main gameplay (temp, being replaced by ArenaScene)
    ArenaScene.js      — 1v1 arena combat
  entities/            — Player, enemies, Remnant class
  systems/             — Input, combat, AI systems
public/
  assets/
    sprites/           — Canvas-generated sprite caches
    audio/             — Sound effects, music
    maps/              — Arena layouts
```

### Current State
- [x] Project scaffolded (Vite + Phaser)
- [x] Portrait mode config (360x640, FIT scaling)
- [x] BootScene with loading bar
- [x] Clean build passing
- [ ] **Arena combat prototype** ← BUILDING NOW
- [ ] Canvas pixel-art Remnant sprites
- [ ] Touch input (invisible joystick + swipe dodge)
- [ ] Auto-attack system
- [ ] Enemy AI
- [ ] HP bars / combat UI
- [ ] New Game flow
- [ ] House Menu
- [ ] Remnant procedural generation
- [ ] Cycle / resource systems

### Deploy
- **Dev**: `npm run dev` (Vite dev server, port 3000)
- **Build**: `npm run build` (outputs to dist/)
- **Repo**: github.com/Lattymoy/project-starfish

### Rules
- Read this file first every session.
- No single-file architecture. Keep modules small and focused.
- Fix root causes, not symptoms.
- No feature removal without explicit permission.
- All art is canvas-generated pixel art. No external image assets for game entities.
- ZERO emoji in rendering code.
