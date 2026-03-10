// RemnantSprite.js — Canvas-generated pixel art Remnant (side-view)
// ~160px tall character for beat-em-up perspective
// All art is code-drawn, no external images

const FRAME_W = 96;
const FRAME_H = 160;
const PX = 3; // Each "pixel" drawn as 3x3 for chunky pixel art look

/**
 * Generate a side-view Remnant sprite sheet.
 * Frames: 0=idle, 1=walk1, 2=walk2, 3=walk3 (4-frame walk cycle)
 *
 * @param {Phaser.Scene} scene
 * @param {string} key - Texture key
 * @param {object} palette - { skin, armor, armorDark, visor, visorGlow, weapon, weaponAccent, core }
 */
export function generateRemnantTexture(scene, key, palette) {
  if (scene.textures.exists(key)) return key;

  const numFrames = 4;
  const canvas = document.createElement('canvas');
  canvas.width = FRAME_W * numFrames;
  canvas.height = FRAME_H;
  const ctx = canvas.getContext('2d');

  for (let f = 0; f < numFrames; f++) {
    const ox = f * FRAME_W;
    _drawSideRemnant(ctx, ox, f, palette);
  }

  const tex = scene.textures.addCanvas(key, canvas);
  for (let f = 0; f < numFrames; f++) {
    tex.add(f, 0, f * FRAME_W, 0, FRAME_W, FRAME_H);
  }

  return key;
}

export { FRAME_W, FRAME_H };

/**
 * Draw a single frame of a side-view Remnant.
 * Character faces RIGHT by default. Phaser flipX handles facing left.
 *
 * Proportions (in pixel-art units, each unit = PX screen pixels):
 *   Head:  8w x 9h
 *   Torso: 10w x 14h
 *   Arms:  3w x 12h (each)
 *   Legs:  4w x 14h (each)
 *   Total height: ~50 units → 150 screen px with PX=3
 */
function _drawSideRemnant(ctx, ox, frameIdx, pal) {
  const p = PX;

  // Helper: draw pixel-art block (coordinates in art-units from frame origin)
  const blk = (ux, uy, uw, uh, color) => {
    ctx.fillStyle = color;
    ctx.fillRect(ox + ux * p, uy * p, uw * p, uh * p);
  };

  // Walk cycle offsets
  // Frame 0: idle, 1: right leg forward, 2: passing, 3: left leg forward
  const walkCycles = [
    { frontLegX: 0, frontLegY: 0, backLegX: 0, backLegY: 0, bodyBob: 0, armSwing: 0 },
    { frontLegX: 3, frontLegY: -1, backLegX: -2, backLegY: 0, bodyBob: -1, armSwing: 2 },
    { frontLegX: 1, frontLegY: 0, backLegX: -1, backLegY: 0, bodyBob: 0, armSwing: 0 },
    { frontLegX: -2, frontLegY: 0, backLegX: 3, backLegY: -1, bodyBob: -1, armSwing: -2 },
  ];
  const wc = walkCycles[frameIdx];

  // Base anchor — character centered in frame, feet near bottom
  const cx = 16; // center x in art units (frame is 32 units wide at PX=3)
  const groundY = 51;

  // ============ BACK ARM (behind body) ============
  const backArmX = cx - 6 + wc.armSwing;
  const backArmY = 14 + wc.bodyBob;
  blk(backArmX, backArmY, 3, 6, pal.armorDark);
  blk(backArmX, backArmY + 6, 3, 5, pal.armorDark);
  blk(backArmX, backArmY + 11, 3, 2, _darken(pal.skin, 0.7));

  // ============ BACK LEG (behind body) ============
  const backLegX = cx - 3 + wc.backLegX;
  const backLegY = 36 + wc.bodyBob + wc.backLegY;
  blk(backLegX, backLegY, 4, 7, pal.armorDark);
  blk(backLegX, backLegY + 7, 4, 5, _darken(pal.armorDark, 0.8));
  blk(backLegX - 1, backLegY + 12, 5, 3, _darken(pal.armor, 0.5));

  // ============ TORSO ============
  const torsoX = cx - 5;
  const torsoY = 13 + wc.bodyBob;

  // Main torso
  blk(torsoX, torsoY, 10, 14, pal.armor);
  // Chest plate
  blk(torsoX + 1, torsoY + 1, 8, 5, pal.armorDark);
  // Core implant glow
  blk(torsoX + 4, torsoY + 3, 2, 2, pal.core);
  // Core highlight
  blk(torsoX + 4, torsoY + 3, 1, 1, pal.visorGlow);

  // Belt
  blk(torsoX, torsoY + 12, 10, 2, _darken(pal.armor, 0.6));
  blk(torsoX + 4, torsoY + 12, 2, 2, pal.visor);

  // Shoulder pad (front-facing side)
  blk(torsoX + 8, torsoY - 1, 4, 4, pal.armor);
  blk(torsoX + 9, torsoY - 1, 2, 1, pal.armorDark);

  // Neck
  blk(cx - 2, torsoY - 2, 4, 3, pal.skin);

  // ============ HEAD ============
  const headX = cx - 4;
  const headY = 2 + wc.bodyBob;

  blk(headX, headY, 8, 9, pal.armor);
  // Helmet ridge
  blk(headX + 1, headY, 6, 1, pal.armorDark);
  // Visor
  blk(headX + 4, headY + 3, 4, 3, pal.visor);
  // Visor glow
  blk(headX + 4, headY + 4, 4, 1, pal.visorGlow);
  // Helmet side
  blk(headX, headY + 2, 1, 5, pal.armorDark);
  // Jaw
  blk(headX + 1, headY + 7, 6, 2, _darken(pal.armor, 0.85));

  // ============ FRONT LEG ============
  const frontLegX = cx + wc.frontLegX;
  const frontLegY = 36 + wc.bodyBob + wc.frontLegY;
  blk(frontLegX, frontLegY, 4, 7, pal.armor);
  // Knee guard
  blk(frontLegX - 1, frontLegY + 5, 5, 2, pal.armorDark);
  blk(frontLegX, frontLegY + 7, 4, 5, pal.armor);
  // Boot
  blk(frontLegX - 1, frontLegY + 12, 6, 3, _darken(pal.armor, 0.6));
  blk(frontLegX - 1, frontLegY + 14, 6, 1, _darken(pal.armor, 0.3));

  // ============ FRONT ARM + WEAPON ============
  const frontArmX = cx + 4 - wc.armSwing;
  const frontArmY = 14 + wc.bodyBob;
  blk(frontArmX, frontArmY, 3, 6, pal.armor);
  blk(frontArmX, frontArmY + 6, 3, 5, pal.armor);
  // Gauntlet
  blk(frontArmX - 1, frontArmY + 9, 4, 3, pal.armorDark);
  // Hand
  blk(frontArmX, frontArmY + 11, 3, 2, pal.skin);

  // Weapon — beam sword extending upward from hand
  const weapX = frontArmX + 2;
  const weapY = frontArmY + 2;
  blk(weapX, weapY - 10, 2, 12, pal.weapon);
  blk(weapX, weapY - 10, 1, 12, pal.weaponAccent);
  // Hilt
  blk(weapX - 1, weapY + 2, 4, 2, pal.armorDark);
  blk(weapX, weapY + 4, 2, 1, pal.armor);
}

/**
 * Darken a hex color by a factor (0-1).
 */
function _darken(hex, factor) {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  const dr = Math.floor(r * factor);
  const dg = Math.floor(g * factor);
  const db = Math.floor(b * factor);
  return `#${dr.toString(16).padStart(2, '0')}${dg.toString(16).padStart(2, '0')}${db.toString(16).padStart(2, '0')}`;
}

/**
 * Palettes for side-view Remnants.
 */
export const PALETTES = {
  player: {
    skin: '#c4a882',
    armor: '#4ecdc4',
    armorDark: '#36a89f',
    visor: '#1a1a2e',
    visorGlow: '#7fefef',
    weapon: '#f7dc6f',
    weaponAccent: '#ffe9a0',
    core: '#7fefef',
  },
  enemy: {
    skin: '#c4a882',
    armor: '#e74c3c',
    armorDark: '#b83a2e',
    visor: '#1a1a2e',
    visorGlow: '#ff6b6b',
    weapon: '#f39c12',
    weaponAccent: '#f5b041',
    core: '#ff6b6b',
  },
  neutral: {
    skin: '#b8a080',
    armor: '#95a5a6',
    armorDark: '#707b7c',
    visor: '#1a1a2e',
    visorGlow: '#d5d8dc',
    weapon: '#bdc3c7',
    weaponAccent: '#d5d8dc',
    core: '#d5d8dc',
  },
};
