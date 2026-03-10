// ArenaScene.js — 1v1 Arena Combat (side-view beat-em-up)
// Horizontal stage, shallow depth lane, portrait mode
import Phaser from 'phaser';
import { GAME_WIDTH, GAME_HEIGHT } from '../config/gameConfig.js';
import { generateRemnantTexture, PALETTES, FRAME_W, FRAME_H } from '../entities/RemnantSprite.js';
import TouchInput from '../systems/TouchInput.js';
import CombatSystem from '../systems/CombatSystem.js';
import EnemyAI from '../systems/EnemyAI.js';
import ArenaHUD from '../systems/ArenaHUD.js';

// Arena: wide horizontal stage, shallow depth (beat-em-up style)
// The arena is wider than the screen — camera follows player horizontally
const ARENA_W = 600;          // Wide stage for horizontal movement
const ARENA_DEPTH = 120;      // Shallow vertical depth lane
const ARENA_FLOOR_Y = 380;    // Where the "ground" sits on screen (bottom portion)
const ARENA_TOP_Y = ARENA_FLOOR_Y - ARENA_DEPTH; // Top of depth lane

export default class ArenaScene extends Phaser.Scene {
  constructor() {
    super({ key: 'ArenaScene' });
  }

  init() {
    // Null all refs for clean restarts
    this.playerFighter = null;
    this.enemyFighter = null;
    this.touchInput = null;
    this.combat = null;
    this.enemyAI = null;
    this.hud = null;
    this.fightOver = false;
  }

  create() {
    // -- Generate textures --
    generateRemnantTexture(this, 'remnant_player', PALETTES.player);
    generateRemnantTexture(this, 'remnant_enemy', PALETTES.enemy);

    // -- World bounds --
    // Full world is arena width, but height is just the screen
    // Physics bounds constrain fighters to the depth lane
    this.physics.world.setBounds(0, ARENA_TOP_Y, ARENA_W, ARENA_DEPTH);

    // -- Draw arena --
    this._drawArena();

    // -- Player sprite (left side of stage) --
    const playerSprite = this.add.sprite(
      120, ARENA_FLOOR_Y - ARENA_DEPTH / 2, 'remnant_player', 0
    );
    this.physics.add.existing(playerSprite);
    playerSprite.body.setCollideWorldBounds(true);
    // Hitbox smaller than visual — centered on lower body
    playerSprite.body.setSize(FRAME_W * 0.4, FRAME_H * 0.3);
    playerSprite.body.setOffset(FRAME_W * 0.3, FRAME_H * 0.6);
    playerSprite.setOrigin(0.5, 1); // Anchor at feet
    playerSprite.setDepth(10);

    // -- Enemy sprite (right side of stage) --
    const enemySprite = this.add.sprite(
      ARENA_W - 120, ARENA_FLOOR_Y - ARENA_DEPTH / 2, 'remnant_enemy', 0
    );
    this.physics.add.existing(enemySprite);
    enemySprite.body.setCollideWorldBounds(true);
    enemySprite.body.setSize(FRAME_W * 0.4, FRAME_H * 0.3);
    enemySprite.body.setOffset(FRAME_W * 0.3, FRAME_H * 0.6);
    enemySprite.setOrigin(0.5, 1);
    enemySprite.setDepth(10);
    enemySprite.setFlipX(true); // Face left toward player

    // -- Combat system --
    this.combat = new CombatSystem(this);

    this.playerFighter = this.combat.createFighter(playerSprite, {
      maxHp: 100,
      atk: 12,
      range: 70,       // Larger range for bigger sprites
      cooldown: 700,
      speed: 180,
      agility: 250,
    });

    this.enemyFighter = this.combat.createFighter(enemySprite, {
      maxHp: 100,
      atk: 10,
      range: 65,
      cooldown: 900,
      speed: 150,
      agility: 200,
    });

    // -- Enemy AI --
    this.enemyAI = new EnemyAI(this, this.enemyFighter, this.combat);

    // -- Input --
    this.touchInput = new TouchInput(this);
    this.cursors = this.input.keyboard.createCursorKeys();
    this.spaceKey = this.input.keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.SPACE);

    // -- Camera: follow player horizontally, fixed vertically --
    const cam = this.cameras.main;
    cam.setBounds(0, 0, ARENA_W, GAME_HEIGHT);
    cam.startFollow(playerSprite, false, 0.1, 0); // Follow X only, no Y follow
    cam.setFollowOffset(0, 0);
    // Lock camera Y so the stage doesn't shift vertically
    cam.scrollY = 0;

    // -- HUD --
    this.hud = new ArenaHUD(this);

    // -- Version --
    this.add.text(8, GAME_HEIGHT - 14, 'Project Starfish v0.2.0', {
      fontSize: '8px', color: '#333',
    }).setScrollFactor(0).setDepth(999);
  }

  update(time, delta) {
    if (this.fightOver) return;

    const pf = this.playerFighter;
    const ef = this.enemyFighter;

    // -- Check fight end --
    if (!pf.isAlive) { this._endFight('DEFEAT'); return; }
    if (!ef.isAlive) { this._endFight('VICTORY'); return; }

    // -- Player movement --
    this._updatePlayerMovement(pf, time);

    // -- Update dodges --
    this.combat.updateDodge(pf, time);
    this.combat.updateDodge(ef, time);

    // -- Player auto-attack --
    if (!pf.isDodging) {
      this.combat.tryAutoAttack(pf, ef, time);
    }

    // -- Enemy AI --
    this.enemyAI.update(pf, time, delta);

    // -- Animate walk frames --
    this._animateWalk(pf);
    this._animateWalk(ef);

    // -- Face opponent --
    this._faceTarget(pf.sprite, ef.sprite);
    this._faceTarget(ef.sprite, pf.sprite);

    // -- Depth sorting: lower Y = further back = lower depth --
    this._depthSort(pf.sprite);
    this._depthSort(ef.sprite);

    // -- HUD --
    this.hud.update(pf, ef);
  }

  _updatePlayerMovement(pf, time) {
    if (pf.isDodging) return;

    const body = pf.sprite.body;
    let dx = 0;
    let dy = 0;

    // Keyboard
    if (this.cursors.left.isDown) dx -= 1;
    if (this.cursors.right.isDown) dx += 1;
    if (this.cursors.up.isDown) dy -= 1;
    if (this.cursors.down.isDown) dy += 1;

    // Touch overrides keyboard
    if (this.touchInput.isMoving) {
      dx = this.touchInput.direction.x;
      dy = this.touchInput.direction.y;
    }

    // Apply movement — horizontal is full speed, vertical (depth) is slower
    if (dx !== 0 || dy !== 0) {
      const len = Math.sqrt(dx * dx + dy * dy);
      body.setVelocity(
        (dx / len) * pf.speed,
        (dy / len) * pf.speed * 0.5 // Depth movement is slower (perspective)
      );
    } else {
      body.setVelocity(0);
    }

    // Dodge
    const swipe = this.touchInput.consumeDodge();
    if (swipe) {
      this.combat.startDodge(pf, swipe, time);
    } else if (Phaser.Input.Keyboard.JustDown(this.spaceKey)) {
      const dir = (dx !== 0 || dy !== 0)
        ? { x: dx, y: dy }
        : { x: -1, y: 0 }; // Default dodge: backstep left
      const len = Math.sqrt(dir.x * dir.x + dir.y * dir.y);
      this.combat.startDodge(pf, { x: dir.x / len, y: dir.y / len }, time);
    }
  }

  _animateWalk(fighter) {
    const body = fighter.sprite.body;
    const moving = Math.abs(body.velocity.x) > 10 || Math.abs(body.velocity.y) > 10;

    if (moving) {
      const frame = Math.floor(this.time.now / 150) % 4;
      fighter.sprite.setFrame(frame);
    } else {
      fighter.sprite.setFrame(0);
    }
  }

  _faceTarget(sprite, target) {
    // Face right if target is to our right, left if to our left
    sprite.setFlipX(target.x < sprite.x);
  }

  _depthSort(sprite) {
    // Characters closer to bottom of depth lane (higher Y) appear in front
    sprite.setDepth(10 + sprite.y);
  }

  _endFight(result) {
    this.fightOver = true;
    this.playerFighter.sprite.body.setVelocity(0);
    this.enemyFighter.sprite.body.setVelocity(0);
    this.hud.showResult(result);

    this.time.delayedCall(1500, () => {
      const restartText = this.add.text(GAME_WIDTH / 2, 300, 'TAP TO CONTINUE', {
        fontSize: '10px', color: '#777', fontFamily: 'monospace',
      }).setOrigin(0.5).setScrollFactor(0).setDepth(9999);

      this.tweens.add({
        targets: restartText,
        alpha: 0.3,
        duration: 500,
        yoyo: true,
        repeat: -1,
      });

      this.input.once('pointerdown', () => {
        this.scene.restart();
      });
    });
  }

  _drawArena() {
    const gfx = this.add.graphics();

    // -- Sky / background gradient (dark sci-fi) --
    // Top of screen is darkest, gets slightly lighter toward horizon
    for (let y = 0; y < ARENA_TOP_Y; y += 4) {
      const t = y / ARENA_TOP_Y;
      const r = Math.floor(8 + t * 12);
      const g = Math.floor(8 + t * 16);
      const b = Math.floor(20 + t * 30);
      gfx.fillStyle(Phaser.Display.Color.GetColor(r, g, b));
      gfx.fillRect(0, y, ARENA_W, 4);
    }

    // -- Background wall / barrier (behind the depth lane) --
    gfx.fillStyle(0x1a1a2e);
    gfx.fillRect(0, ARENA_TOP_Y - 60, ARENA_W, 60);
    // Wall detail: horizontal lines
    gfx.lineStyle(1, 0x252540);
    for (let y = ARENA_TOP_Y - 55; y < ARENA_TOP_Y; y += 8) {
      gfx.moveTo(0, y);
      gfx.lineTo(ARENA_W, y);
    }
    gfx.strokePath();
    // Wall top edge glow
    gfx.lineStyle(2, 0x3d5a99);
    gfx.moveTo(0, ARENA_TOP_Y - 60);
    gfx.lineTo(ARENA_W, ARENA_TOP_Y - 60);
    gfx.strokePath();

    // -- Arena floor (the depth lane) --
    // Slight gradient: darker at top (far), lighter at bottom (near)
    for (let y = ARENA_TOP_Y; y < ARENA_FLOOR_Y; y += 2) {
      const t = (y - ARENA_TOP_Y) / ARENA_DEPTH;
      const r = Math.floor(18 + t * 14);
      const g = Math.floor(25 + t * 18);
      const b = Math.floor(50 + t * 20);
      gfx.fillStyle(Phaser.Display.Color.GetColor(r, g, b));
      gfx.fillRect(0, y, ARENA_W, 2);
    }

    // Floor grid lines (perspective — wider spacing at bottom)
    gfx.lineStyle(1, 0x2a3a5a, 0.3);
    // Horizontal lines across depth
    for (let i = 0; i <= 6; i++) {
      const t = i / 6;
      const y = ARENA_TOP_Y + t * ARENA_DEPTH;
      gfx.moveTo(0, y);
      gfx.lineTo(ARENA_W, y);
    }
    // Vertical lines along stage width
    for (let x = 0; x <= ARENA_W; x += 60) {
      gfx.moveTo(x, ARENA_TOP_Y);
      gfx.lineTo(x, ARENA_FLOOR_Y);
    }
    gfx.strokePath();

    // -- Arena border (floor edge glow) --
    gfx.lineStyle(2, 0x4a6fa5);
    gfx.moveTo(0, ARENA_FLOOR_Y);
    gfx.lineTo(ARENA_W, ARENA_FLOOR_Y);
    gfx.strokePath();

    // -- Below floor (dark pit / void) --
    gfx.fillStyle(0x050510);
    gfx.fillRect(0, ARENA_FLOOR_Y, ARENA_W, GAME_HEIGHT - ARENA_FLOOR_Y + 100);

    // Side pillars / arena boundary markers
    const pillarW = 16;
    const pillarH = ARENA_DEPTH + 60;
    const pillarY = ARENA_TOP_Y - 60;
    // Left pillar
    gfx.fillStyle(0x1c2040);
    gfx.fillRect(0, pillarY, pillarW, pillarH);
    gfx.lineStyle(1, 0x3d5a99);
    gfx.strokeRect(0, pillarY, pillarW, pillarH);
    // Right pillar
    gfx.fillStyle(0x1c2040);
    gfx.fillRect(ARENA_W - pillarW, pillarY, pillarW, pillarH);
    gfx.lineStyle(1, 0x3d5a99);
    gfx.strokeRect(ARENA_W - pillarW, pillarY, pillarW, pillarH);

    gfx.setDepth(0);
  }
}
