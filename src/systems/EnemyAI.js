// EnemyAI.js — Basic arena opponent AI
// Approaches player, attacks when in range, occasionally strafes

export default class EnemyAI {
  constructor(scene, fighter, combatSystem) {
    this.scene = scene;
    this.fighter = fighter;
    this.combat = combatSystem;

    // AI state
    this.state = 'approach'; // approach | attack | strafe | dodge
    this.strafeTimer = 0;
    this.strafeDir = 1; // 1 or -1
    this.strafeDuration = 600 + Math.random() * 800;
    this.nextStateChange = 0;

    // Tuning
    this.approachRange = fighter.range + 20; // get slightly closer than attack range
    this.strafeChance = 0.3; // chance to strafe after attacking
    this.dodgeChance = 0.15; // chance to dodge when hit (placeholder)
  }

  /**
   * Update AI each frame.
   * @param {object} target - player fighter object
   * @param {number} time - scene time ms
   * @param {number} delta - frame delta ms
   */
  update(target, time, delta) {
    const f = this.fighter;
    if (!f.isAlive || !target.isAlive) {
      f.sprite.body.setVelocity(0);
      return;
    }

    const dist = Phaser.Math.Distance.Between(
      f.sprite.x, f.sprite.y,
      target.sprite.x, target.sprite.y
    );

    switch (this.state) {
      case 'approach':
        this._approach(target, dist);
        if (dist <= f.range) {
          this.state = 'attack';
        }
        break;

      case 'attack':
        f.sprite.body.setVelocity(0);
        this.combat.tryAutoAttack(f, target, time);

        // After attack cooldown, maybe strafe
        if (time - f.lastAttackTime > f.cooldown * 0.5) {
          if (Math.random() < this.strafeChance) {
            this.state = 'strafe';
            this.strafeTimer = time;
            this.strafeDir = Math.random() < 0.5 ? 1 : -1;
            this.strafeDuration = 400 + Math.random() * 600;
          }
        }

        // If player moved out of range, re-approach
        if (dist > this.approachRange) {
          this.state = 'approach';
        }
        break;

      case 'strafe':
        this._strafe(target, time);
        if (time - this.strafeTimer > this.strafeDuration) {
          this.state = dist > this.approachRange ? 'approach' : 'attack';
        }
        break;
    }
  }

  _approach(target, dist) {
    const f = this.fighter;
    const angle = Phaser.Math.Angle.Between(
      f.sprite.x, f.sprite.y,
      target.sprite.x, target.sprite.y
    );
    const spd = f.speed * 0.7; // AI moves slightly slower than player
    f.sprite.body.setVelocity(
      Math.cos(angle) * spd,
      Math.sin(angle) * spd
    );
  }

  _strafe(target, time) {
    const f = this.fighter;
    // Move perpendicular to the line between fighter and target
    const angle = Phaser.Math.Angle.Between(
      f.sprite.x, f.sprite.y,
      target.sprite.x, target.sprite.y
    );
    const perpAngle = angle + (Math.PI / 2) * this.strafeDir;
    const spd = f.speed * 0.5;
    f.sprite.body.setVelocity(
      Math.cos(perpAngle) * spd,
      Math.sin(perpAngle) * spd
    );
  }
}
