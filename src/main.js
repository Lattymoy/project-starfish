// Project Starfish - Entry Point
import Phaser from 'phaser';
import { gameConfig } from './config/gameConfig.js';
import BootScene from './scenes/BootScene.js';
import GameScene from './scenes/GameScene.js';

const config = {
  ...gameConfig,
  scene: [BootScene, GameScene],
};

const game = new Phaser.Game(config);
