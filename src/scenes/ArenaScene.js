// ArenaScene.js — 1v1 Arena Combat
// Portrait mode, touch controls, auto-attack, canvas pixel art
import Phaser from 'phaser';
import { GAME_WIDTH, GAME_HEIGHT } from '../config/gameConfig.js';
import { generateRemnantTexture, PALETTES } from '../entities/RemnantSprite.js';
import TouchInput from '../systems/TouchInput.js';
import CombatSystem from '../systems/CombatSystem.js';
import EnemyAI from '../systems/EnemyAI.js';
import ArenaHUD from '../systems/ArenaHUD.js';

// Arena dimensions (game world, not screen)
const ARENA_W = 320;
const ARENA_H = 400;
// Center arena in a slightly larger world so camera has room
const WORLD_PAD = 40;

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
    if (!this.textures.exists('remnant_player')) {
      generateRemnantTexture(this, 'remnant_player', PALETTES.player);
    }
    if (!this.textures.exists('remnant_enemy')) {
      generateRemnantTexture(this, 'remnant_enemy', PALETTES.enemy);
    }

    // -- Arena bounds --
    const worldW = ARENA_W + WORLD_PAD * 2;
    const worldH = ARENA_H + WORLD_PAD * 2;
    this.physics.world.setBounds(WORLD_PAD, WORLD_PAD, ARENA_W, ARENA_H);

    // -- Arena floor --
    this._drawArenaFloor(worldW, worldH);

    // -- Player sprite --
    const playerSprite = this.add.sprite(
      worldW / 2, worldH / 2 + 80, 'remnant_player', 0
    );
    this.physics.add.existing(playerSprite);
    playerSprite.body.setCollideWorldBounds(true);
    playerSprite.body.setSize(16, 24);
    playerSprite.setDepth(10);

    // -- Enemy sprite --
    const enemySprite = this.add.sprite(
      worldW / 2, worldH / 2 - 80, 'remnant_enemy', 0
    );
    this.physics.add.existing(enemySprite);
    enemySprite.body.setCollideWorldBounds(true);
    enemySprite.body.setSize(16, 24);
    enemySprite.setDepth(10);

    // -- Combat system --
    this.combat = new CombatSystem(this);

    this.playerFighter = this.combat.createFighter(playerSprite, {
      maxHp: 100,
      atk: 12,
      range: 40,
      cooldown: 700,
      speed: 160,
      agility: 220,
    });

    this.enemyFighter = this.combat.createFighter(enemySprite, {
      maxHp: 100,
      atk: 10,
      range: 38,
      cooldown: 900,
      speed: 140,
      agility: 180,
    });

    // -- Enemy AI --
    this.enemyAI = new EnemyAI(this, this.enemyFighter, this.combat);

    // -- Input --
    this.touchInput = new TouchInput(this);
    this.cursors = this.input.keyboard.createCursorKeys();

    // -- Camera --
    const cam = this.cameras.main;
    cam.setBounds(0, 0, worldW, worldH);
    // Center camera on arena
    cam.centerOn(worldW / 2, worldH / 2);

    // -- HUD (after camera so scrollFactor(0) works) --
    this.hud = new ArenaHUD(this);

    // -- Keyboard dodge (spacebar for dev) --
    this.spaceKey = this.input.keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.SPACE);

    // -- Version --
    this.add.text(8, GAME_HEIGHT - 14, 'Project Starfish v0.1.0', {
      fontSize: '8px', color: '#333',
    }).setScrollFactor(0).setDepth(999);
  }

  update(time, delta) {
    if (this.fightOver) return;

    const pf = this.playerFighter;
    const ef = this.enemyFighter;

    // -- Check fight end --
    if (!pf.isAlive) {
      this._endFight('DEFEAT');
      return;
    }
    if (!ef.isAlive) {
      this._endFight('VICTORY');
      return;
    }

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

    // Apply movement
    if (dx !== 0 || dy !== 0) {
      const len = Math.sqrt(dx * dx + dy * dy);
      body.setVelocity(
        (dx / len) * pf.speed,
        (dy / len) * pf.speed
      );
    } else {
      body.setVelocity(0);
    }

    // Dodge: touch swipe or spacebar
    const swipe = this.touchInput.consumeDodge();
    if (swipe) {
      this.combat.startDodge(pf, swipe, time);
    } else if (Phaser.Input.Keyboard.JustDown(this.spaceKey)) {
      // Keyboard dodge: dodge in movement direction or backward
      const dir = (dx !== 0 || dy !== 0)
        ? { x: dx, y: dy }
        : { x: 0, y: 1 }; // default dodge backward (down)
      const len = Math.sqrt(dir.x * dir.x + dir.y * dir.y);
      this.combat.startDodge(pf, { x: dir.x / len, y: dir.y / len }, time);
    }
  }

  _animateWalk(fighter) {
    const body = fighter.sprite.body;
    const moving = Math.abs(body.velocity.x) > 10 || Math.abs(body.velocity.y) > 10;

    if (moving) {
      // Cycle walk frames based on time
      const frame = Math.floor(this.time.now / 150) % 2 === 0 ? 1 : 3;
      fighter.sprite.setFrame(frame);
    } else {
      fighter.sprite.setFrame(0);
    }
  }

  _faceTarget(sprite, target) {
    // Simple flip: face left or right toward target
    sprite.setFlipX(target.x < sprite.x);
  }

  _endFight(result) {
    this.fightOver = true;
    this.playerFighter.sprite.body.setVelocity(0);
    this.enemyFighter.sprite.body.setVelocity(0);
    this.hud.showResult(result);

    // Tap to restart after delay
    this.time.delayedCall(1500, () => {
      const restartText = this.add.text(GAME_WIDTH / 2, 260, 'TAP TO CONTINUE', {
        fontSize: '10px', color: '#777', fontFamily: 'monospace',
      }).setOrigin(0.5).setScrollFactor(0).setDepth(100);

      // Blink
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

  _drawArenaFloor(worldW, worldH) {
    const gfx = this.add.graphics();

    // Dark background
    gfx.fillStyle(0x0a0a1a);
    gfx.fillRect(0, 0, worldW, worldH);

    // Arena floor
    gfx.fillStyle(0x16213e);
    gfx.fillRect(WORLD_PAD, WORLD_PAD, ARENA_W, ARENA_H);

    // Arena border
    gfx.lineStyle(2, 0x2c3e6b);
    gfx.strokeRect(WORLD_PAD, WORLD_PAD, ARENA_W, ARENA_H);

    // Center line
    gfx.lineStyle(1, 0x1a2a4a);
    const cx = worldW / 2;
    gfx.moveTo(WORLD_PAD, worldH / 2);
    gfx.lineTo(WORLD_PAD + ARENA_W, worldH / 2);
    gfx.strokePath();

    // Center circle
    gfx.lineStyle(1, 0x1a2a4a);
    gfx.strokeCircle(cx, worldH / 2, 40);

    // Corner marks
    const cornerSize = 12;
    const corners = [
      [WORLD_PAD, WORLD_PAD],
      [WORLD_PAD + ARENA_W, WORLD_PAD],
      [WORLD_PAD, WORLD_PAD + ARENA_H],
      [WORLD_PAD + ARENA_W, WORLD_PAD + ARENA_H],
    ];
    gfx.lineStyle(1, 0x3d5a99);
    for (const [cx, cy] of corners) {
      const dx = cx === WORLD_PAD ? 1 : -1;
      const dy = cy === WORLD_PAD ? 1 : -1;
      gfx.moveTo(cx, cy + dy * cornerSize);
      gfx.lineTo(cx, cy);
      gfx.lineTo(cx + dx * cornerSize, cy);
    }
    gfx.strokePath();

    gfx.setDepth(0);
  }
}
