// Project Starfish - Game Configuration
// Portrait mode, mobile-first, adapts to any screen size
// Uses RESIZE mode — canvas matches device viewport exactly (no black bars)

import Phaser from 'phaser';

// Design baseline — used as minimum dimensions and for ratio calculations
// The game is designed around this, but renders at the device's actual size
export const BASE_WIDTH = 360;
export const BASE_HEIGHT = 640;

// Live dimension accessors — use these instead of constants
// After the game boots, these return the actual canvas size
let _game = null;

export function setGameRef(game) {
  _game = game;
}

/** Current game canvas width (changes on resize) */
export function getWidth() {
  if (_game) return _game.scale.width;
  return BASE_WIDTH;
}

/** Current game canvas height (changes on resize) */
export function getHeight() {
  if (_game) return _game.scale.height;
  return BASE_HEIGHT;
}

/**
 * Scale a value designed for BASE_WIDTH to current width.
 * Use for horizontal positions and widths.
 */
export function scaleX(val) {
  return val * (getWidth() / BASE_WIDTH);
}

/**
 * Scale a value designed for BASE_HEIGHT to current height.
 * Use for vertical positions and heights.
 */
export function scaleY(val) {
  return val * (getHeight() / BASE_HEIGHT);
}

export const gameConfig = {
  type: Phaser.AUTO,
  parent: 'game-container',
  backgroundColor: '#1a1a2e',
  scale: {
    mode: Phaser.Scale.RESIZE,   // Canvas matches viewport — no black bars
    autoCenter: Phaser.Scale.CENTER_BOTH,
    width: BASE_WIDTH,
    height: BASE_HEIGHT,
    min: {
      width: 280,
      height: 480,
    },
    max: {
      width: 540,   // Cap width so it doesn't get absurdly wide on tablets
      height: 960,
    },
  },
  physics: {
    default: 'arcade',
    arcade: {
      gravity: { y: 0 },
      debug: false,
    },
  },
  input: {
    activePointers: 2,
  },
  render: {
    pixelArt: true,
    antialias: false,
    roundPixels: true,
  },
  scene: [],
};
