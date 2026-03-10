// TouchInput.js — Mobile touch input system
// Invisible joystick (drag) + swipe dodge detection

export default class TouchInput {
  constructor(scene) {
    this.scene = scene;

    // Movement state
    this.direction = { x: 0, y: 0 };
    this.isMoving = false;

    // Dodge state
    this.dodgeRequested = false;
    this.dodgeDirection = { x: 0, y: 0 };

    // Internal tracking
    this._touchStart = null;
    this._touchStartTime = 0;
    this._activePointerId = null;

    // Config
    this.moveDeadZone = 12;    // pixels before movement registers
    this.swipeThreshold = 40;  // min distance for a swipe
    this.swipeMaxTime = 250;   // ms — must be fast to count as swipe

    this._bind(scene);
  }

  _bind(scene) {
    scene.input.on('pointerdown', this._onDown, this);
    scene.input.on('pointermove', this._onMove, this);
    scene.input.on('pointerup', this._onUp, this);
  }

  _onDown(pointer) {
    // Only track one pointer for movement
    if (this._activePointerId !== null) return;

    this._activePointerId = pointer.id;
    this._touchStart = { x: pointer.x, y: pointer.y };
    this._touchStartTime = pointer.time;
    this.isMoving = false;
    this.direction.x = 0;
    this.direction.y = 0;
  }

  _onMove(pointer) {
    if (pointer.id !== this._activePointerId) return;
    if (!this._touchStart || !pointer.isDown) return;

    const dx = pointer.x - this._touchStart.x;
    const dy = pointer.y - this._touchStart.y;
    const dist = Math.sqrt(dx * dx + dy * dy);

    if (dist > this.moveDeadZone) {
      this.isMoving = true;
      // Normalize to -1..1
      this.direction.x = dx / dist;
      this.direction.y = dy / dist;
    } else {
      this.isMoving = false;
      this.direction.x = 0;
      this.direction.y = 0;
    }
  }

  _onUp(pointer) {
    if (pointer.id !== this._activePointerId) return;

    const dx = pointer.x - this._touchStart.x;
    const dy = pointer.y - this._touchStart.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    const elapsed = pointer.time - this._touchStartTime;

    // Detect swipe: fast + far enough
    if (dist >= this.swipeThreshold && elapsed <= this.swipeMaxTime) {
      this.dodgeRequested = true;
      this.dodgeDirection.x = dx / dist;
      this.dodgeDirection.y = dy / dist;
    }

    // Reset
    this._activePointerId = null;
    this._touchStart = null;
    this.isMoving = false;
    this.direction.x = 0;
    this.direction.y = 0;
  }

  /**
   * Call once per frame after processing dodge.
   * Clears the dodge flag so it only fires once.
   */
  consumeDodge() {
    if (this.dodgeRequested) {
      this.dodgeRequested = false;
      return { ...this.dodgeDirection };
    }
    return null;
  }

  destroy() {
    this.scene.input.off('pointerdown', this._onDown, this);
    this.scene.input.off('pointermove', this._onMove, this);
    this.scene.input.off('pointerup', this._onUp, this);
  }
}
