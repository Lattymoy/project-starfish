// Project Starfish - Game Configuration
// Portrait mode, mobile-first 2D side-scroller

import Phaser from 'phaser';

// Target portrait resolution (9:16 aspect ratio)
// Scales to fit any device
export const GAME_WIDTH = 360;
export const GAME_HEIGHT = 640;

export const gameConfig = {
  type: Phaser.AUTO,
  parent: 'game-container',
  width: GAME_WIDTH,
  height: GAME_HEIGHT,
  backgroundColor: '#1a1a2e',
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.CENTER_BOTH,
  },
  physics: {
    default: 'arcade',
    arcade: {
      gravity: { y: 0 }, // Top-down / platformer gravity set per-scene
      debug: false,
    },
  },
  input: {
    activePointers: 2, // Multi-touch support
  },
  render: {
    pixelArt: true,       // Crisp pixel scaling
    antialias: false,
    roundPixels: true,
  },
  scene: [], // Scenes added in main.js
};
