using System;
using UnityEngine;
using DaggerfallWorkshop.Game;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.Portrait
{
    /// <summary>
    /// Optional floating action buttons on the right edge of the viewport.
    /// Three primary buttons: Attack, Spell, Use/Interact. Positioned for
    /// right-thumb reach. Fully configurable positions, sizes, and opacity.
    /// </summary>
    public class ActionButtonPanel : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Button Appearance")]
        [SerializeField]
        [Range(40f, 80f)]
        [Tooltip("Diameter of each action button in pixels.")]
        private float _buttonSize = 56f;

        [SerializeField]
        [Range(0.3f, 1.0f)]
        [Tooltip("Opacity of the buttons.")]
        private float _opacity = 0.7f;

        [SerializeField]
        [Tooltip("Color for the Attack button.")]
        private Color _attackColor = new Color(0.85f, 0.2f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Color for the Spell button.")]
        private Color _spellColor = new Color(0.3f, 0.4f, 0.9f, 1f);

        [SerializeField]
        [Tooltip("Color for the Use/Interact button.")]
        private Color _useColor = new Color(0.3f, 0.75f, 0.3f, 1f);

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Right margin from screen edge in pixels.")]
        private float _rightMargin = 16f;

        [SerializeField]
        [Tooltip("Vertical gap between buttons in pixels.")]
        private float _verticalGap = 12f;

        [SerializeField]
        [Tooltip("Vertical center offset from middle of viewport.")]
        private float _verticalOffset = 0f;

        /// <summary>Fired when the Attack button is pressed.</summary>
        public event Action OnAttackPressed;

        /// <summary>Fired when the Spell button is pressed.</summary>
        public event Action OnSpellPressed;

        /// <summary>Fired when the Use/Interact button is pressed.</summary>
        public event Action OnUsePressed;

        private Texture2D _circleTexture;
        private GUIStyle _buttonLabelStyle;
        private bool _stylesInitialized;

        // Press state
        private bool _attackPressed;
        private bool _spellPressed;
        private bool _usePressed;

        private void Start()
        {
            _circleTexture = CreateCircleTexture(64, Color.white);

            // Load opacity from settings
            if (LegacyBootstrapper.Instance != null && LegacyBootstrapper.Instance.Settings != null)
            {
                _opacity = LegacyBootstrapper.Instance.Settings.ButtonOpacity;
            }
        }

        private void OnDestroy()
        {
            if (_circleTexture != null) Destroy(_circleTexture);
        }

        private void OnGUI()
        {
            if (_circleTexture == null)
                return;

            InitStyles();

            float rightEdge = Screen.width - _rightMargin - _buttonSize;
            float centerY = Screen.height / 2f + _verticalOffset;
            float totalHeight = _buttonSize * 3 + _verticalGap * 2;
            float startY = centerY - totalHeight / 2f;

            // Attack button (top)
            _attackPressed = DrawActionButton(
                rightEdge, startY,
                "ATK", _attackColor, _attackPressed
            );
            if (_attackPressed)
            {
                OnAttackPressed?.Invoke();
                TriggerAttack();
            }

            // Spell button (middle)
            _spellPressed = DrawActionButton(
                rightEdge, startY + _buttonSize + _verticalGap,
                "SPL", _spellColor, _spellPressed
            );
            if (_spellPressed)
            {
                OnSpellPressed?.Invoke();
                TriggerSpell();
            }

            // Use button (bottom)
            _usePressed = DrawActionButton(
                rightEdge, startY + (_buttonSize + _verticalGap) * 2,
                "USE", _useColor, _usePressed
            );
            if (_usePressed)
            {
                OnUsePressed?.Invoke();
                TriggerUse();
            }

            // Reset press states at end of frame
            _attackPressed = false;
            _spellPressed = false;
            _usePressed = false;
        }

        private bool DrawActionButton(float x, float y, string label, Color color, bool wasPressed)
        {
            Rect buttonRect = new Rect(x, y, _buttonSize, _buttonSize);
            bool pressed = false;

            // Check touch input
            for (int t = 0; t < UnityEngine.Input.touchCount; t++)
            {
                Touch touch = UnityEngine.Input.GetTouch(t);
                Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);

                if (buttonRect.Contains(guiPos) && touch.phase == TouchPhase.Began)
                {
                    pressed = true;
                    Util.HapticFeedback.LightTap();
                }
            }

            // Draw button
            float scale = pressed ? 0.9f : 1f;
            float scaledSize = _buttonSize * scale;
            float offset = (_buttonSize - scaledSize) / 2f;

            Rect drawRect = new Rect(x + offset, y + offset, scaledSize, scaledSize);

            GUI.color = new Color(color.r, color.g, color.b, _opacity);
            GUI.DrawTexture(drawRect, _circleTexture);

            // Label
            GUI.color = new Color(1f, 1f, 1f, _opacity);
            GUI.Label(drawRect, label, _buttonLabelStyle);

            GUI.color = Color.white;
            return pressed;
        }

        private void TriggerAttack()
        {
            if (GameManager.Instance == null || GameManager.Instance.WeaponManager == null)
                return;

            var screenWeapon = GameManager.Instance.WeaponManager.ScreenWeapon;
            if (screenWeapon != null && !screenWeapon.IsAttacking())
            {
                screenWeapon.OnAttackDirection(MouseDirections.Down);
            }
        }

        private void TriggerSpell()
        {
            // REQUIRES DFU PATCH: SpellManager may need a public method to cast
            // the currently readied spell programmatically.
            Debug.Log($"{LogPrefix} Spell button pressed — spell casting integration pending");
        }

        private void TriggerUse()
        {
            if (GameManager.Instance == null || GameManager.Instance.PlayerActivate == null)
                return;

            GameManager.Instance.PlayerActivate.FireRayFromScreenPoint(
                new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)
            );
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
                        float edgeFade = Mathf.Clamp01(1f - (dist - radius + 2f) / 2f);
                        texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * edgeFade));
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

        private void InitStyles()
        {
            if (_stylesInitialized)
                return;

            _buttonLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _stylesInitialized = true;
        }
    }
}
