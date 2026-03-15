using UnityEngine;
using DaggerfallWorkshop.Game;
using ProjectLegacy.Core;

namespace ProjectLegacy.Input
{
    /// <summary>
    /// Virtual joystick that spawns at the user's touch point in the Move Zone.
    /// Outputs normalized direction and magnitude for movement, and feeds
    /// into DFU's InputManager axes.
    /// </summary>
    public class FloatingJoystick : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Joystick Settings")]
        [SerializeField]
        [Range(40f, 120f)]
        [Tooltip("Maximum drag radius in pixels.")]
        private float _maxRadius = 80f;

        [SerializeField]
        [Range(0.05f, 0.3f)]
        [Tooltip("Dead zone as a fraction of max radius. Prevents drift from minor touch movement.")]
        private float _deadZone = 0.1f;

        [Header("Visuals")]
        [SerializeField]
        [Tooltip("Enable visual joystick rendering (concentric circles).")]
        private bool _showVisual = true;

        [SerializeField]
        [Range(0.2f, 0.8f)]
        [Tooltip("Opacity of the joystick visual.")]
        private float _visualOpacity = 0.4f;

        [SerializeField]
        [Tooltip("Outer ring radius in pixels.")]
        private float _outerRingRadius = 80f;

        [SerializeField]
        [Tooltip("Inner knob radius in pixels.")]
        private float _innerKnobRadius = 30f;

        /// <summary>Normalized movement direction (-1 to 1 on each axis).</summary>
        public Vector2 Direction { get; private set; }

        /// <summary>Movement magnitude (0 to 1).</summary>
        public float Magnitude { get; private set; }

        /// <summary>Whether the joystick is currently being touched.</summary>
        public bool IsActive { get; private set; }

        private int _activeFingerId = -1;
        private Vector2 _origin;
        private Vector2 _currentPosition;
        private Texture2D _ringTexture;
        private Texture2D _knobTexture;

        private void Start()
        {
            // Load max radius from settings if available
            if (LegacyBootstrapper.Instance != null && LegacyBootstrapper.Instance.Settings != null)
            {
                _maxRadius = LegacyBootstrapper.Instance.Settings.JoystickMaxRadius;
            }

            CreateVisualTextures();
        }

        private void LateUpdate()
        {
            ApplyToInputManager();
        }

        private void OnDestroy()
        {
            if (_ringTexture != null) Destroy(_ringTexture);
            if (_knobTexture != null) Destroy(_knobTexture);
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
                    if (!IsActive)
                    {
                        _activeFingerId = touch.fingerId;
                        _origin = touch.position;
                        _currentPosition = touch.position;
                        IsActive = true;
                        Direction = Vector2.zero;
                        Magnitude = 0f;
                    }
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (IsActive && touch.fingerId == _activeFingerId)
                    {
                        _currentPosition = touch.position;
                        CalculateOutput();
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touch.fingerId == _activeFingerId)
                    {
                        Reset();
                    }
                    break;
            }
        }

        /// <summary>
        /// Resets the joystick to idle state.
        /// </summary>
        public void Reset()
        {
            IsActive = false;
            _activeFingerId = -1;
            Direction = Vector2.zero;
            Magnitude = 0f;
        }

        private void CalculateOutput()
        {
            Vector2 delta = _currentPosition - _origin;
            float distance = delta.magnitude;

            // Clamp to max radius
            if (distance > _maxRadius)
            {
                delta = delta.normalized * _maxRadius;
                distance = _maxRadius;
            }

            // Apply dead zone
            float normalizedDistance = distance / _maxRadius;
            if (normalizedDistance < _deadZone)
            {
                Direction = Vector2.zero;
                Magnitude = 0f;
                return;
            }

            // Remap from dead zone to 1.0
            float remapped = (normalizedDistance - _deadZone) / (1f - _deadZone);
            Direction = delta.normalized;
            Magnitude = Mathf.Clamp01(remapped);
        }

        private void ApplyToInputManager()
        {
            if (InputManager.Instance == null)
                return;

            // Feed joystick output into DFU's input axes
            // REQUIRES DFU PATCH: InputManager may need public setters for
            // Horizontal/Vertical override values, or we inject via reflection.
            // For now, we use the existing touch input path that Vwing's port established.
            if (IsActive)
            {
                float sensitivity = 1f;
                if (LegacyBootstrapper.Instance != null && LegacyBootstrapper.Instance.Settings != null)
                {
                    sensitivity = LegacyBootstrapper.Instance.Settings.MoveSensitivity;
                }

                InputManager.Instance.ApplyHorizontalForce(Direction.x * Magnitude * sensitivity);
                InputManager.Instance.ApplyVerticalForce(Direction.y * Magnitude * sensitivity);
            }
        }

        private void CreateVisualTextures()
        {
            // Create simple circle textures for the joystick visual
            _ringTexture = CreateCircleTexture(64, new Color(1f, 1f, 1f, 0.3f));
            _knobTexture = CreateCircleTexture(32, new Color(1f, 1f, 1f, 0.6f));
        }

        private Texture2D CreateCircleTexture(int size, Color color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size / 2f;
            Vector2 center = new Vector2(radius, radius);

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float alpha = color.a * Mathf.Clamp01(1f - (dist / radius - 0.8f) / 0.2f);
                        texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            if (!_showVisual || !IsActive)
                return;

            // Convert screen coordinates to GUI coordinates (Y is flipped)
            float guiOriginY = Screen.height - _origin.y;
            float guiCurrentY = Screen.height - _currentPosition.y;

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _visualOpacity);

            // Draw outer ring at origin
            if (_ringTexture != null)
            {
                Rect outerRect = new Rect(
                    _origin.x - _outerRingRadius,
                    guiOriginY - _outerRingRadius,
                    _outerRingRadius * 2,
                    _outerRingRadius * 2
                );
                GUI.DrawTexture(outerRect, _ringTexture);
            }

            // Draw inner knob at current position (clamped)
            if (_knobTexture != null)
            {
                Vector2 clampedDelta = _currentPosition - _origin;
                if (clampedDelta.magnitude > _maxRadius)
                {
                    clampedDelta = clampedDelta.normalized * _maxRadius;
                }

                Vector2 knobPos = _origin + clampedDelta;
                float guiKnobY = Screen.height - knobPos.y;

                Rect knobRect = new Rect(
                    knobPos.x - _innerKnobRadius,
                    guiKnobY - _innerKnobRadius,
                    _innerKnobRadius * 2,
                    _innerKnobRadius * 2
                );
                GUI.DrawTexture(knobRect, _knobTexture);
            }

            GUI.color = prevColor;
        }
    }
}
