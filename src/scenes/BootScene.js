// BootScene - First scene, handles core asset loading
import Phaser from 'phaser';

export default class BootScene extends Phaser.Scene {
  constructor() {
    super({ key: 'BootScene' });
  }

  preload() {
    // Loading bar
    const { width, height } = this.scale;
    const barW = width * 0.6;
    const barH = 8;
    const barX = (width - barW) / 2;
    const barY = height / 2;

    const bg = this.add.rectangle(width / 2, barY, barW, barH, 0x333333).setOrigin(0.5);
    const fill = this.add.rectangle(barX, barY, 0, barH, 0x4ecdc4).setOrigin(0, 0.5);

    this.load.on('progress', (val) => {
      fill.width = barW * val;
    });

    this.load.on('complete', () => {
      bg.destroy();
      fill.destroy();
    });

    // TODO: Load core assets here
    // this.load.spritesheet('player', 'assets/sprites/player.png', { frameWidth: 32, frameHeight: 32 });
  }

  create() {
    this.scene.start('GameScene');
  }
}
