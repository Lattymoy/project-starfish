// ArenaHUD.js — HP bars and fight status overlay
// Responsive — reads live screen dimensions
// All canvas-drawn, no external assets

import { getWidth, getHeight } from '../config/gameConfig.js';

export default class ArenaHUD {
  constructor(scene) {
    this.scene = scene;
    this.container = scene.add.container(0, 0).setScrollFactor(0).setDepth(100);

    const w = getWidth();
    const h = getHeight();

    // HP bar dimensions — proportional to screen width
    this.barW = w * 0.38;
    this.barH = Math.max(8, Math.floor(h * 0.015));
    this.barPad = Math.floor(w * 0.033);
    this.barY = Math.floor(h * 0.04);

    // Player HP (left side)
    this.playerBarBg = scene.add.rectangle(
      this.barPad, this.barY, this.barW, this.barH, 0x333333
    ).setOrigin(0, 0.5);

    this.playerBarFill = scene.add.rectangle(
      this.barPad, this.barY, this.barW, this.barH, 0x4ecdc4
    ).setOrigin(0, 0.5);

    const labelSize = Math.max(7, Math.floor(h * 0.013)) + 'px';

    this.playerLabel = scene.add.text(this.barPad, this.barY - this.barH, 'YOU', {
      fontSize: labelSize, color: '#aaa', fontFamily: 'monospace',
    }).setOrigin(0, 0.5);

    // Enemy HP (right side)
    const enemyX = w - this.barPad;

    this.enemyBarBg = scene.add.rectangle(
      enemyX, this.barY, this.barW, this.barH, 0x333333
    ).setOrigin(1, 0.5);

    this.enemyBarFill = scene.add.rectangle(
      enemyX, this.barY, this.barW, this.barH, 0xe74c3c
    ).setOrigin(1, 0.5);

    this.enemyLabel = scene.add.text(enemyX, this.barY - this.barH, 'ENEMY', {
      fontSize: labelSize, color: '#aaa', fontFamily: 'monospace',
    }).setOrigin(1, 0.5);

    // Fight result text (hidden by default)
    const resultSize = Math.max(16, Math.floor(h * 0.035)) + 'px';
    this.resultText = scene.add.text(w / 2, h * 0.32, '', {
      fontSize: resultSize, color: '#fff', fontFamily: 'monospace',
      stroke: '#000', strokeThickness: 3,
    }).setOrigin(0.5).setAlpha(0);

    this.container.add([
      this.playerBarBg, this.playerBarFill, this.playerLabel,
      this.enemyBarBg, this.enemyBarFill, this.enemyLabel,
      this.resultText,
    ]);
  }

  update(playerFighter, enemyFighter) {
    const pRatio = Math.max(0, playerFighter.hp / playerFighter.maxHp);
    this.playerBarFill.width = this.barW * pRatio;

    const eRatio = Math.max(0, enemyFighter.hp / enemyFighter.maxHp);
    this.enemyBarFill.width = this.barW * eRatio;
  }

  showResult(result) {
    const h = getHeight();
    this.resultText.setText(result);
    this.resultText.setColor(result === 'VICTORY' ? '#4ecdc4' : '#e74c3c');
    this.scene.tweens.add({
      targets: this.resultText,
      alpha: 1,
      y: h * 0.28,
      duration: 600,
      ease: 'Back.easeOut',
    });
  }
}
