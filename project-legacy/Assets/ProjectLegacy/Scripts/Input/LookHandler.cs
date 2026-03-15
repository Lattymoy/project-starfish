using UnityEngine;
using DaggerfallWorkshop.Game;
using ProjectLegacy.Core;

namespace ProjectLegacy.Input
{
    /// <summary>
    /// Converts touch movement in the Look Zone to camera pitch/yaw rotation.
    /// Feeds into DFU's mouse-look system by overriding InputManager mouse
    /// delta values.
    /// </summary>
    public class LookHandler : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Sensitivity")]
        [SerializeField]
        [Range(0.1f, 3.0f)]
        [Tooltip("Base sensitivity multiplier for camera rotation.")]
        private float _sensitivity = 1.0f;

        [SerializeField]
        [Tooltip("Sensitivity curve mapping touch speed to rotation speed. X = normalized touch speed, Y = rotation multiplier.")]
        private AnimationCurve _sensitivityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Behavior")]
        [SerializeField]
        [Tooltip("Invert the Y-axis (swipe up looks down).")]
        private bool _invertY = false;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("Smoothing factor. 0 = no smoothing (immediate), higher = smoother but laggier.")]
        private float _smoothing = 0.05f;

        [SerializeField]
        [Range(0f, 5f)]
        [Tooltip("Dead zone in pixels. Touch movement below this threshold is ignored.")]
        private float _deadZone = 2f;

        /// <summary>Accumulated look delta this frame (unsmoothed).</summary>
        public Vector2 RawDelta { get; private set; }

        /// <summary>Smoothed look delta applied to the camera.</summary>
        public Vector2 SmoothedDelta { get; private set; }

        /// <summary>Whether a look touch is currently active.</summary>
        public bool IsLooking { get; private set; }

        private int _activeFingerId = -1;
        private Vector2 _previousPosition;
        private Vector2 _smoothedDelta;
        private bool _hasInitialPosition;

        private void Start()
        {
            LoadSettings();
        }

        private void LateUpdate()
        {
            ApplyLookToCamera();

            // Decay smoothed delta when not actively looking
            if (!IsLooking)
            {
                _smoothedDelta = Vector2.Lerp(_smoothedDelta, Vector2.zero, 10f * Time.deltaTime);
                SmoothedDelta = _smoothedDelta;
            }
        }

        /// <summary>
        /// Processes a touch event routed from TouchZoneManager.
        /// </summary>
        /// <param name="touch">The touch to process.</param>
        public void ProcessTouch(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (!IsLooking)
                    {
                        _activeFingerId = touch.fingerId;
                        _previousPosition = touch.position;
                        _hasInitialPosition = true;
                        IsLooking = true;
                        RawDelta = Vector2.zero;
                    }
                    break;

                case TouchPhase.Moved:
                    if (IsLooking && touch.fingerId == _activeFingerId && _hasInitialPosition)
                    {
                        Vector2 delta = touch.position - _previousPosition;
                        _previousPosition = touch.position;

                        // Apply dead zone
                        if (delta.magnitude < _deadZone)
                        {
                            RawDelta = Vector2.zero;
                            return;
                        }

                        // Apply sensitivity curve
                        float normalizedSpeed = Mathf.Clamp01(delta.magnitude / 100f);
                        float curveMultiplier = _sensitivityCurve.Evaluate(normalizedSpeed);

                        Vector2 processed = delta * curveMultiplier * _sensitivity;

                        // Invert Y if configured
                        if (_invertY)
                        {
                            processed.y = -processed.y;
                        }

                        RawDelta = processed;

                        // Smooth the delta
                        if (_smoothing > 0f)
                        {
                            _smoothedDelta = Vector2.Lerp(_smoothedDelta, processed, 1f - _smoothing);
                        }
                        else
                        {
                            _smoothedDelta = processed;
                        }

                        SmoothedDelta = _smoothedDelta;
                    }
                    break;

                case TouchPhase.Stationary:
                    if (IsLooking && touch.fingerId == _activeFingerId)
                    {
                        RawDelta = Vector2.zero;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touch.fingerId == _activeFingerId)
                    {
                        IsLooking = false;
                        _activeFingerId = -1;
                        _hasInitialPosition = false;
                        RawDelta = Vector2.zero;
                    }
                    break;
            }
        }

        private void ApplyLookToCamera()
        {
            if (GameManager.Instance == null)
                return;

            var mouseLook = GameManager.Instance.PlayerMouseLook;
            if (mouseLook == null)
                return;

            Vector2 delta = SmoothedDelta;
            if (delta.sqrMagnitude < 0.001f)
                return;

            // Apply touch delta as mouse-look input
            // DFU's PlayerMouseLook uses Yaw (horizontal) and Pitch (vertical)
            mouseLook.Yaw += delta.x;
            mouseLook.Pitch -= delta.y; // Pitch is inverted relative to screen Y
        }

        private void LoadSettings()
        {
            if (LegacyBootstrapper.Instance == null || LegacyBootstrapper.Instance.Settings == null)
                return;

            var settings = LegacyBootstrapper.Instance.Settings;
            _sensitivity = settings.LookSensitivity;
            _invertY = settings.InvertYAxis;
        }
    }
}
