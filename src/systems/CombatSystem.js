// CombatSystem.js — Handles auto-attack, dodge, damage, HP
// Operates on fighter objects that have { sprite, hp, maxHp, atk, range, cooldown, speed, agility }

export default class CombatSystem {
  constructor(scene) {
    this.scene = scene;
  }

  /**
   * Create a fighter data object from stats.
   * @param {Phaser.GameObjects.Sprite|Phaser.GameObjects.Rectangle} sprite
   * @param {object} stats - { maxHp, atk, range, cooldown, speed, agility }
   * @returns {object} fighter
   */
  createFighter(sprite, stats) {
    return {
      sprite,
      hp: stats.maxHp,
      maxHp: stats.maxHp,
      atk: stats.atk,
      range: stats.range || 40,        // auto-attack range in pixels
      cooldown: stats.cooldown || 800,  // ms between attacks
      speed: stats.speed || 160,
      agility: stats.agility || 200,    // dodge impulse speed
      lastAttackTime: 0,
      isDodging: false,
      dodgeTimer: 0,
      dodgeDuration: 180, // ms
      isAlive: true,
    };
  }

  /**
   * Attempt auto-attack from attacker toward target.
   * @param {object} attacker - fighter object
   * @param {object} target - fighter object
   * @param {number} time - current scene time (ms)
   * @returns {boolean} true if attack landed
   */
  tryAutoAttack(attacker, target, time) {
    if (!attacker.isAlive || !target.isAlive) return false;
    if (target.isDodging) return false;
    if (time - attacker.lastAttackTime < attacker.cooldown) return false;

    const dist = Phaser.Math.Distance.Between(
      attacker.sprite.x, attacker.sprite.y,
      target.sprite.x, target.sprite.y
    );

    if (dist <= attacker.range) {
      attacker.lastAttackTime = time;
      this.applyDamage(target, attacker.atk);
      this._flashDamage(target);
      return true;
    }

    return false;
  }

  /**
   * Apply damage to a fighter.
   */
  applyDamage(fighter, amount) {
    if (!fighter.isAlive) return;
    fighter.hp = Math.max(0, fighter.hp - amount);
    if (fighter.hp <= 0) {
      fighter.isAlive = false;
    }
  }

  /**
   * Start a dodge for a fighter in a given direction.
   * @param {object} fighter
   * @param {object} dir - { x, y } normalized direction
   * @param {number} time - current scene time
   */
  startDodge(fighter, dir, time) {
    if (fighter.isDodging || !fighter.isAlive) return;
    fighter.isDodging = true;
    fighter.dodgeTimer = time;

    // Apply impulse
    const body = fighter.sprite.body;
    body.setVelocity(
      dir.x * fighter.agility * 2.5,
      dir.y * fighter.agility * 2.5
    );
  }

  /**
   * Update dodge state — call every frame.
   */
  updateDodge(fighter, time) {
    if (!fighter.isDodging) return;
    if (time - fighter.dodgeTimer >= fighter.dodgeDuration) {
      fighter.isDodging = false;
    }
  }

  /**
   * Visual flash on damage.
   */
  _flashDamage(fighter) {
    const sprite = fighter.sprite;
    if (!sprite || !sprite.active) return;

    sprite.setTint(0xff0000);
    this.scene.time.delayedCall(100, () => {
      if (sprite.active) sprite.clearTint();
    });
  }
}
