using System;
using UnityEngine;
using ProjectLegacy.Core;

namespace ProjectLegacy.Input
{
    /// <summary>
    /// Finite state machine that classifies touch sequences in the Look Zone
    /// into discrete gestures: tap, double-tap, swipe, and long-press.
    /// Emits events that other systems subscribe to.
    /// </summary>
    public class GestureRecognizer : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Tap Settings")]
        [SerializeField]
        [Range(0.1f, 0.4f)]
        [Tooltip("Maximum duration in seconds for a tap.")]
        private float _tapMaxDuration = 0.2f;

        [SerializeField]
        [Range(5f, 20f)]
        [Tooltip("Maximum movement in pixels for a tap.")]
        private float _tapMaxDistance = 10f;

        [Header("Double-Tap Settings")]
        [SerializeField]
        [Range(0.2f, 0.6f)]
        [Tooltip("Maximum gap in seconds between taps for a double-tap.")]
        private float _doubleTapWindow = 0.4f;

        [Header("Swipe Settings")]
        [SerializeField]
        [Range(20f, 80f)]
        [Tooltip("Minimum movement in pixels for a swipe.")]
        private float _swipeMinDistance = 40f;

        [SerializeField]
        [Range(0.15f, 0.5f)]
        [Tooltip("Maximum duration in seconds for a swipe.")]
        private float _swipeMaxDuration = 0.3f;

        [Header("Long-Press Settings")]
        [SerializeField]
        [Range(0.3f, 1.0f)]
        [Tooltip("Minimum hold duration in seconds for a long-press.")]
        private float _longPressMinDuration = 0.5f;

        /// <summary>Fired on a single tap. Parameter: screen position.</summary>
        public event Action<Vector2> OnTap;

        /// <summary>Fired on a double-tap. Parameter: screen position.</summary>
        public event Action<Vector2> OnDoubleTap;

        /// <summary>Fired on a swipe. Parameters: direction vector, speed (px/sec).</summary>
        public event Action<Vector2, float> OnSwipe;

        /// <summary>Fired on a long-press. Parameter: screen position.</summary>
        public event Action<Vector2> OnLongPress;

        /// <summary>Fired on a two-finger tap.</summary>
        public event Action OnTwoFingerTap;

        /// <summary>Current gesture state.</summary>
        public GestureState State { get; private set; } = GestureState.Idle;

        // Touch tracking
        private int _trackingFingerId = -1;
        private Vector2 _touchStartPosition;
        private float _touchStartTime;
        private float _totalMovement;

        // Double-tap tracking
        private float _lastTapTime;
        private Vector2 _lastTapPosition;
        private bool _waitingForDoubleTap;

        // Two-finger tracking
        private int _activeTouchCount;

        // Long-press tracking
        private bool _longPressFired;

        private void Start()
        {
            LoadSettings();
        }

        private void Update()
        {
            // Check for long-press timeout
            if (State == GestureState.TouchBegan && !_longPressFired)
            {
                float holdDuration = Time.time - _touchStartTime;
                if (holdDuration >= _longPressMinDuration && _totalMovement < _tapMaxDistance)
                {
                    _longPressFired = true;
                    State = GestureState.LongPressing;
                    OnLongPress?.Invoke(_touchStartPosition);
                }
            }

            // Check for double-tap timeout
            if (_waitingForDoubleTap && Time.time - _lastTapTime > _doubleTapWindow)
            {
                _waitingForDoubleTap = false;
                // Fire the single tap that was deferred
                OnTap?.Invoke(_lastTapPosition);
            }
        }

        /// <summary>
        /// Processes a touch event routed from TouchZoneManager.
        /// </summary>
        /// <param name="touch">The touch to process.</param>
        public void ProcessTouch(Touch touch)
        {
            // Track active touch count for two-finger gestures
            _activeTouchCount = UnityEngine.Input.touchCount;

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    HandleTouchBegan(touch);
                    break;

                case TouchPhase.Moved:
                    HandleTouchMoved(touch);
                    break;

                case TouchPhase.Stationary:
                    // No state change needed — long-press is handled in Update
                    break;

                case TouchPhase.Ended:
                    HandleTouchEnded(touch);
                    break;

                case TouchPhase.Canceled:
                    ResetState();
                    break;
            }
        }

        /// <summary>
        /// Resets the recognizer to idle state.
        /// </summary>
        public void ResetState()
        {
            State = GestureState.Idle;
            _trackingFingerId = -1;
            _totalMovement = 0f;
            _longPressFired = false;
        }

        private void HandleTouchBegan(Touch touch)
        {
            if (State != GestureState.Idle)
                return;

            // Check for two-finger tap
            if (_activeTouchCount >= 2)
            {
                OnTwoFingerTap?.Invoke();
                ResetState();
                return;
            }

            _trackingFingerId = touch.fingerId;
            _touchStartPosition = touch.position;
            _touchStartTime = Time.time;
            _totalMovement = 0f;
            _longPressFired = false;
            State = GestureState.TouchBegan;
        }

        private void HandleTouchMoved(Touch touch)
        {
            if (touch.fingerId != _trackingFingerId)
                return;

            _totalMovement += touch.deltaPosition.magnitude;

            // If moved enough, transition to swiping
            if (State == GestureState.TouchBegan && _totalMovement > _tapMaxDistance)
            {
                State = GestureState.Swiping;
            }
        }

        private void HandleTouchEnded(Touch touch)
        {
            if (touch.fingerId != _trackingFingerId)
                return;

            float duration = Time.time - _touchStartTime;
            Vector2 endPosition = touch.position;
            Vector2 totalDelta = endPosition - _touchStartPosition;
            float distance = totalDelta.magnitude;

            if (_longPressFired)
            {
                // Long-press already handled
                ResetState();
                return;
            }

            // Check for swipe
            if (distance >= _swipeMinDistance && duration <= _swipeMaxDuration)
            {
                float speed = distance / Mathf.Max(duration, 0.001f);
                OnSwipe?.Invoke(totalDelta.normalized, speed);
                ResetState();
                _waitingForDoubleTap = false;
                return;
            }

            // Check for tap
            if (duration <= _tapMaxDuration && _totalMovement <= _tapMaxDistance)
            {
                if (_waitingForDoubleTap && Time.time - _lastTapTime <= _doubleTapWindow)
                {
                    // Double-tap detected
                    _waitingForDoubleTap = false;
                    OnDoubleTap?.Invoke(endPosition);
                }
                else
                {
                    // Defer tap — wait to see if a double-tap follows
                    _waitingForDoubleTap = true;
                    _lastTapTime = Time.time;
                    _lastTapPosition = endPosition;
                }

                ResetState();
                return;
            }

            ResetState();
        }

        private void LoadSettings()
        {
            if (LegacyBootstrapper.Instance == null || LegacyBootstrapper.Instance.Settings == null)
                return;

            var s = LegacyBootstrapper.Instance.Settings;
            _tapMaxDuration = s.TapMaxDuration;
            _tapMaxDistance = s.TapMaxDistance;
            _swipeMinDistance = s.SwipeMinDistance;
            _swipeMaxDuration = s.SwipeMaxDuration;
            _longPressMinDuration = s.LongPressMinDuration;
            _doubleTapWindow = s.DoubleTapWindow;
        }
    }

    /// <summary>
    /// States of the gesture recognition state machine.
    /// </summary>
    public enum GestureState
    {
        /// <summary>No active gesture.</summary>
        Idle,
        /// <summary>Touch has begun, classifying gesture type.</summary>
        TouchBegan,
        /// <summary>Touch has moved enough to be a swipe.</summary>
        Swiping,
        /// <summary>Touch held long enough for long-press.</summary>
        LongPressing
    }
}
