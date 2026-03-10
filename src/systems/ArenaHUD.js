// ArenaHUD.js — HP bars and fight status overlay
// All canvas-drawn, no external assets

import { GAME_WIDTH } from '../config/gameConfig.js';

export default class ArenaHUD {
  constructor(scene) {
    this.scene = scene;
    this.container = scene.add.container(0, 0).setScrollFactor(0).setDepth(100);

    // HP bar dimensions
    this.barW = GAME_WIDTH * 0.38;
    this.barH = 10;
    this.barPad = 12;
    this.barY = 20;

    // Player HP (left side)
    this.playerBarBg = scene.add.rectangle(
      this.barPad, this.barY, this.barW, this.barH, 0x333333
    ).setOrigin(0, 0.5);

    this.playerBarFill = scene.add.rectangle(
      this.barPad, this.barY, this.barW, this.barH, 0x4ecdc4
    ).setOrigin(0, 0.5);

    this.playerLabel = scene.add.text(this.barPad, this.barY - 12, 'YOU', {
      fontSize: '8px', color: '#aaa', fontFamily: 'monospace',
    }).setOrigin(0, 0.5);

    // Enemy HP (right side)
    const enemyX = GAME_WIDTH - this.barPad;

    this.enemyBarBg = scene.add.rectangle(
      enemyX, this.barY, this.barW, this.barH, 0x333333
    ).setOrigin(1, 0.5);

    this.enemyBarFill = scene.add.rectangle(
      enemyX, this.barY, this.barW, this.barH, 0xe74c3c
    ).setOrigin(1, 0.5);

    this.enemyLabel = scene.add.text(enemyX, this.barY - 12, 'ENEMY', {
      fontSize: '8px', color: '#aaa', fontFamily: 'monospace',
    }).setOrigin(1, 0.5);

    // Fight result text (hidden by default)
    this.resultText = scene.add.text(GAME_WIDTH / 2, 200, '', {
      fontSize: '20px', color: '#fff', fontFamily: 'monospace',
      stroke: '#000', strokeThickness: 3,
    }).setOrigin(0.5).setAlpha(0);

    this.container.add([
      this.playerBarBg, this.playerBarFill, this.playerLabel,
      this.enemyBarBg, this.enemyBarFill, this.enemyLabel,
      this.resultText,
    ]);
  }

  /**
   * Update HP bar fills.
   * @param {object} playerFighter
   * @param {object} enemyFighter
   */
  update(playerFighter, enemyFighter) {
    const pRatio = Math.max(0, playerFighter.hp / playerFighter.maxHp);
    this.playerBarFill.width = this.barW * pRatio;

    const eRatio = Math.max(0, enemyFighter.hp / enemyFighter.maxHp);
    this.enemyBarFill.width = this.barW * eRatio;
  }

  /**
   * Show fight result.
   * @param {string} result - 'VICTORY' or 'DEFEAT'
   */
  showResult(result) {
    this.resultText.setText(result);
    this.resultText.setColor(result === 'VICTORY' ? '#4ecdc4' : '#e74c3c');
    this.scene.tweens.add({
      targets: this.resultText,
      alpha: 1,
      y: 180,
      duration: 600,
      ease: 'Back.easeOut',
    });
  }
}
