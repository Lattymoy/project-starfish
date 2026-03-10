// RemnantSprite.js — Canvas-generated pixel art Remnant
// Draws a humanoid fighter sprite to a Phaser texture
// All art is code-drawn, no external images

/**
 * Generate a Remnant sprite sheet texture on canvas.
 * Creates idle + walk frames for a humanoid pixel-art character.
 *
 * @param {Phaser.Scene} scene
 * @param {string} key - Texture key to register
 * @param {object} palette - { body, accent, visor, weapon }
 * @param {number} frameW - Frame width in pixels (default 24)
 * @param {number} frameH - Frame height in pixels (default 32)
 */
export function generateRemnantTexture(scene, key, palette, frameW = 24, frameH = 32) {
  const frames = 4; // idle, walk1, idle-mirror, walk2
  const canvas = document.createElement('canvas');
  canvas.width = frameW * frames;
  canvas.height = frameH;
  const ctx = canvas.getContext('2d');

  // Pixel scale — each "pixel" is 2x2 actual pixels for chunky look
  const px = 2;

  for (let f = 0; f < frames; f++) {
    const ox = f * frameW; // frame x offset
    const isWalk = f === 1 || f === 3;
    const walkDir = f === 1 ? 1 : -1;

    _drawRemnant(ctx, ox, frameW, frameH, px, palette, isWalk, walkDir);
  }

  // Register as sprite sheet in Phaser
  const tex = scene.textures.addCanvas(key, canvas);
  tex.add(0, 0, 0, 0, frameW, frameH);       // idle
  tex.add(1, 0, frameW, 0, frameW, frameH);   // walk1
  tex.add(2, 0, frameW * 2, 0, frameW, frameH); // idle2
  tex.add(3, 0, frameW * 3, 0, frameW, frameH); // walk2

  return key;
}

function _drawRemnant(ctx, ox, fw, fh, px, pal, isWalk, walkDir) {
  const cx = ox + Math.floor(fw / 2); // center x of frame
  const baseY = fh - 2; // bottom with 1px padding

  // Helper: draw a filled pixel-art rect
  const rect = (x, y, w, h, color) => {
    ctx.fillStyle = color;
    ctx.fillRect(x, y, w * px, h * px);
  };

  // -- Head (4px wide, 4px tall) --
  const headW = 4;
  const headH = 4;
  const headX = cx - (headW * px) / 2;
  const headY = baseY - 16 * px;
  rect(headX, headY, headW, headH, pal.body);

  // Visor (2px wide, 1px tall, centered on head)
  rect(headX + px, headY + px, 2, 1, pal.visor);

  // -- Body / Torso (4px wide, 5px tall) --
  const bodyX = cx - (4 * px) / 2;
  const bodyY = headY + headH * px;
  rect(bodyX, bodyY, 4, 5, pal.body);

  // Accent stripe on chest
  rect(bodyX + px, bodyY + px, 2, 1, pal.accent);

  // -- Arms (1px wide, 4px tall, on each side of torso) --
  const armOffY = isWalk ? walkDir * px : 0;
  // Left arm
  rect(bodyX - px, bodyY + px + armOffY, 1, 4, pal.body);
  // Right arm (weapon hand)
  rect(bodyX + 4 * px, bodyY + px - armOffY, 1, 4, pal.body);

  // -- Weapon (on right hand) --
  const weapX = bodyX + 4 * px;
  const weapY = bodyY + px - armOffY + 3 * px;
  rect(weapX + px, weapY - 2 * px, 1, 4, pal.weapon);

  // -- Legs (2 legs, each 1px wide, 3px tall) --
  const legY = bodyY + 5 * px;
  const legSpread = isWalk ? walkDir * px : 0;
  // Left leg
  rect(bodyX + px + legSpread, legY, 1, 3, pal.body);
  // Right leg
  rect(bodyX + 2 * px - legSpread, legY, 1, 3, pal.body);

  // -- Core glow (1px dot on chest center) --
  rect(bodyX + 1.5 * px, bodyY + 2 * px, 1, 1, pal.accent);
}

/**
 * Preset palettes for quick use
 */
export const PALETTES = {
  player: {
    body: '#4ecdc4',
    accent: '#45b7aa',
    visor: '#ffffff',
    weapon: '#f7dc6f',
  },
  enemy: {
    body: '#e74c3c',
    accent: '#c0392b',
    visor: '#ffffff',
    weapon: '#f39c12',
  },
  neutral: {
    body: '#95a5a6',
    accent: '#7f8c8d',
    visor: '#ecf0f1',
    weapon: '#bdc3c7',
  },
};
