// ArenaScene.js — 1v1 Arena Combat (side-view beat-em-up)
// Responsive layout — all positions derived from current screen size
import Phaser from 'phaser';
import { getWidth, getHeight } from '../config/gameConfig.js';
import { generateRemnantTexture, PALETTES, FRAME_W, FRAME_H } from '../entities/RemnantSprite.js';
import TouchInput from '../systems/TouchInput.js';
import CombatSystem from '../systems/CombatSystem.js';
import EnemyAI from '../systems/EnemyAI.js';
import ArenaHUD from '../systems/ArenaHUD.js';

// Arena width is wider than screen for horizontal scrolling
// These are world-space constants, not screen-relative
const ARENA_W = 600;

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
    this.arenaGfx = null;
  }

  create() {
    // -- Compute layout from current screen size --
    // Arena floor sits at ~60% of screen height
    // Depth lane is ~18% of screen height
    const h = getHeight();
    const w = getWidth();

    this.arenaFloorY = Math.floor(h * 0.60);
    this.arenaDepth = Math.floor(h * 0.18);
    this.arenaTopY = this.arenaFloorY - this.arenaDepth;

    // -- Generate textures --
    generateRemnantTexture(this, 'remnant_player', PALETTES.player);
    generateRemnantTexture(this, 'remnant_enemy', PALETTES.enemy);

    // -- World bounds --
    this.physics.world.setBounds(0, this.arenaTopY, ARENA_W, this.arenaDepth);

    // -- Draw arena --
    this._drawArena();

    // -- Player sprite (left side of stage) --
    const playerSprite = this.add.sprite(
      120, this.arenaFloorY - this.arenaDepth / 2, 'remnant_player', 0
    );
    this.physics.add.existing(playerSprite);
    playerSprite.body.setCollideWorldBounds(true);
    playerSprite.body.setSize(FRAME_W * 0.4, FRAME_H * 0.3);
    playerSprite.body.setOffset(FRAME_W * 0.3, FRAME_H * 0.55);
    playerSprite.setOrigin(0.5, 1);
    playerSprite.setDepth(10);

    // -- Enemy sprite (right side of stage) --
    const enemySprite = this.add.sprite(
      ARENA_W - 120, this.arenaFloorY - this.arenaDepth / 2, 'remnant_enemy', 0
    );
    this.physics.add.existing(enemySprite);
    enemySprite.body.setCollideWorldBounds(true);
    enemySprite.body.setSize(FRAME_W * 0.4, FRAME_H * 0.3);
    enemySprite.body.setOffset(FRAME_W * 0.3, FRAME_H * 0.55);
    enemySprite.setOrigin(0.5, 1);
    enemySprite.setDepth(10);
    enemySprite.setFlipX(true);

    // -- Combat system --
    this.combat = new CombatSystem(this);

    this.playerFighter = this.combat.createFighter(playerSprite, {
      maxHp: 100,
      atk: 12,
      range: 50,
      cooldown: 700,
      speed: 180,
      agility: 250,
    });

    this.enemyFighter = this.combat.createFighter(enemySprite, {
      maxHp: 100,
      atk: 10,
      range: 48,
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
    cam.setBounds(0, 0, ARENA_W, h);
    cam.startFollow(playerSprite, false, 0.1, 0);
    cam.setFollowOffset(0, 0);
    cam.scrollY = 0;

    // -- HUD --
    this.hud = new ArenaHUD(this);

    // -- Version --
    this.add.text(8, h - 14, 'Project Starfish v0.3.0', {
      fontSize: '8px', color: '#333',
    }).setScrollFactor(0).setDepth(999);

    // -- Handle resize events --
    this.scale.on('resize', this._onResize, this);
  }

  shutdown() {
    // Clean up resize listener when scene stops
    this.scale.off('resize', this._onResize, this);
  }

  _onResize(gameSize) {
    // On device rotation or viewport change, restart the scene
    // This rebuilds all layout from the new dimensions
    // Debounce to avoid rapid-fire restarts
    if (this._resizeTimer) clearTimeout(this._resizeTimer);
    this._resizeTimer = setTimeout(() => {
      if (this.scene.isActive('ArenaScene')) {
        this.scene.restart();
      }
    }, 200);
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

    // -- Depth sorting --
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

    if (this.cursors.left.isDown) dx -= 1;
    if (this.cursors.right.isDown) dx += 1;
    if (this.cursors.up.isDown) dy -= 1;
    if (this.cursors.down.isDown) dy += 1;

    if (this.touchInput.isMoving) {
      dx = this.touchInput.direction.x;
      dy = this.touchInput.direction.y;
    }

    if (dx !== 0 || dy !== 0) {
      const len = Math.sqrt(dx * dx + dy * dy);
      body.setVelocity(
        (dx / len) * pf.speed,
        (dy / len) * pf.speed * 0.5
      );
    } else {
      body.setVelocity(0);
    }

    const swipe = this.touchInput.consumeDodge();
    if (swipe) {
      this.combat.startDodge(pf, swipe, time);
    } else if (Phaser.Input.Keyboard.JustDown(this.spaceKey)) {
      const dir = (dx !== 0 || dy !== 0)
        ? { x: dx, y: dy }
        : { x: -1, y: 0 };
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
    sprite.setFlipX(target.x < sprite.x);
  }

  _depthSort(sprite) {
    sprite.setDepth(10 + sprite.y);
  }

  _endFight(result) {
    this.fightOver = true;
    this.playerFighter.sprite.body.setVelocity(0);
    this.enemyFighter.sprite.body.setVelocity(0);
    this.hud.showResult(result);

    const h = getHeight();
    const w = getWidth();

    this.time.delayedCall(1500, () => {
      const restartText = this.add.text(w / 2, h * 0.47, 'TAP TO CONTINUE', {
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
    const h = getHeight();
    const floorY = this.arenaFloorY;
    const topY = this.arenaTopY;
    const depth = this.arenaDepth;

    // -- Sky / background gradient --
    for (let y = 0; y < topY; y += 4) {
      const t = y / topY;
      const r = Math.floor(8 + t * 12);
      const g = Math.floor(8 + t * 16);
      const b = Math.floor(20 + t * 30);
      gfx.fillStyle(Phaser.Display.Color.GetColor(r, g, b));
      gfx.fillRect(0, y, ARENA_W, 4);
    }

    // -- Background wall --
    const wallH = 60;
    gfx.fillStyle(0x1a1a2e);
    gfx.fillRect(0, topY - wallH, ARENA_W, wallH);
    gfx.lineStyle(1, 0x252540);
    for (let y = topY - wallH + 5; y < topY; y += 8) {
      gfx.moveTo(0, y);
      gfx.lineTo(ARENA_W, y);
    }
    gfx.strokePath();
    gfx.lineStyle(2, 0x3d5a99);
    gfx.moveTo(0, topY - wallH);
    gfx.lineTo(ARENA_W, topY - wallH);
    gfx.strokePath();

    // -- Arena floor (depth lane) --
    for (let y = topY; y < floorY; y += 2) {
      const t = (y - topY) / depth;
      const r = Math.floor(18 + t * 14);
      const g = Math.floor(25 + t * 18);
      const b = Math.floor(50 + t * 20);
      gfx.fillStyle(Phaser.Display.Color.GetColor(r, g, b));
      gfx.fillRect(0, y, ARENA_W, 2);
    }

    // Grid lines
    gfx.lineStyle(1, 0x2a3a5a, 0.3);
    for (let i = 0; i <= 6; i++) {
      const t = i / 6;
      const y = topY + t * depth;
      gfx.moveTo(0, y);
      gfx.lineTo(ARENA_W, y);
    }
    for (let x = 0; x <= ARENA_W; x += 60) {
      gfx.moveTo(x, topY);
      gfx.lineTo(x, floorY);
    }
    gfx.strokePath();

    // -- Floor edge glow --
    gfx.lineStyle(2, 0x4a6fa5);
    gfx.moveTo(0, floorY);
    gfx.lineTo(ARENA_W, floorY);
    gfx.strokePath();

    // -- Below floor (void) --
    gfx.fillStyle(0x050510);
    gfx.fillRect(0, floorY, ARENA_W, h - floorY + 100);

    // -- Side pillars --
    const pillarW = 16;
    const pillarH = depth + wallH;
    const pillarY = topY - wallH;
    gfx.fillStyle(0x1c2040);
    gfx.fillRect(0, pillarY, pillarW, pillarH);
    gfx.lineStyle(1, 0x3d5a99);
    gfx.strokeRect(0, pillarY, pillarW, pillarH);
    gfx.fillStyle(0x1c2040);
    gfx.fillRect(ARENA_W - pillarW, pillarY, pillarW, pillarH);
    gfx.lineStyle(1, 0x3d5a99);
    gfx.strokeRect(ARENA_W - pillarW, pillarY, pillarW, pillarH);

    gfx.setDepth(0);
  }
}
