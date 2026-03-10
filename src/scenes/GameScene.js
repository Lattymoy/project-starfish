// GameScene - Main gameplay scene
// 2D side-scroller, 4-directional movement, portrait mode
import Phaser from 'phaser';
import { GAME_WIDTH, GAME_HEIGHT } from '../config/gameConfig.js';

export default class GameScene extends Phaser.Scene {
  constructor() {
    super({ key: 'GameScene' });
  }

  init() {
    // Reset all refs here on scene restart
    this.player = null;
    this.touchInput = null;
  }

  create() {
    // -- World bounds (wider than screen for scrolling) --
    const worldW = GAME_WIDTH * 4;
    const worldH = GAME_HEIGHT * 2;
    this.physics.world.setBounds(0, 0, worldW, worldH);

    // -- Placeholder ground / background --
    this.add.rectangle(worldW / 2, worldH / 2, worldW, worldH, 0x16213e).setOrigin(0.5);

    // Grid lines for spatial reference during dev
    const gridGfx = this.add.graphics();
    gridGfx.lineStyle(1, 0x1a1a3e, 0.3);
    for (let x = 0; x < worldW; x += 64) {
      gridGfx.moveTo(x, 0);
      gridGfx.lineTo(x, worldH);
    }
    for (let y = 0; y < worldH; y += 64) {
      gridGfx.moveTo(0, y);
      gridGfx.lineTo(worldW, y);
    }
    gridGfx.strokePath();

    // -- Player (placeholder rectangle until we have sprites) --
    this.player = this.add.rectangle(GAME_WIDTH / 2, GAME_HEIGHT / 2, 24, 32, 0x4ecdc4);
    this.physics.add.existing(this.player);
    this.player.body.setCollideWorldBounds(true);

    // -- Camera --
    this.cameras.main.startFollow(this.player, true, 0.08, 0.08);
    this.cameras.main.setBounds(0, 0, worldW, worldH);

    // -- Input: keyboard (dev) + touch (mobile) --
    this.cursors = this.input.keyboard.createCursorKeys();
    this._setupTouchInput();

    // -- Version watermark (dev only) --
    this.add.text(8, 8, 'Project Starfish v0.1.0', {
      fontSize: '10px',
      color: '#555',
    }).setScrollFactor(0).setDepth(999);
  }

  update() {
    const speed = 160;
    const body = this.player.body;
    body.setVelocity(0);

    // Keyboard input
    let dx = 0;
    let dy = 0;

    if (this.cursors.left.isDown) dx -= 1;
    if (this.cursors.right.isDown) dx += 1;
    if (this.cursors.up.isDown) dy -= 1;
    if (this.cursors.down.isDown) dy += 1;

    // Touch input override
    if (this.touchInput) {
      dx = this.touchInput.dx;
      dy = this.touchInput.dy;
    }

    // Normalize diagonal movement
    if (dx !== 0 || dy !== 0) {
      const len = Math.sqrt(dx * dx + dy * dy);
      body.setVelocity((dx / len) * speed, (dy / len) * speed);
    }
  }

  _setupTouchInput() {
    // Virtual joystick via touch drag
    // Left half of screen = movement, right half = action (future)
    let touchStartX = 0;
    let touchStartY = 0;
    const deadZone = 10;

    this.input.on('pointerdown', (pointer) => {
      if (pointer.x < GAME_WIDTH / 2) {
        touchStartX = pointer.x;
        touchStartY = pointer.y;
        this.touchInput = { dx: 0, dy: 0 };
      }
    });

    this.input.on('pointermove', (pointer) => {
      if (this.touchInput && pointer.isDown && pointer.x < GAME_WIDTH * 0.7) {
        const diffX = pointer.x - touchStartX;
        const diffY = pointer.y - touchStartY;

        this.touchInput.dx = Math.abs(diffX) > deadZone ? Math.sign(diffX) : 0;
        this.touchInput.dy = Math.abs(diffY) > deadZone ? Math.sign(diffY) : 0;
      }
    });

    this.input.on('pointerup', () => {
      this.touchInput = null;
    });
  }
}
