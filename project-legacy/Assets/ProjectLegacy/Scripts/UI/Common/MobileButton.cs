using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectLegacy.UI.Common
{
    /// <summary>
    /// A styled mobile button component with a large touch target (minimum 48dp),
    /// haptic feedback on press, visual feedback (scale + color shift), and configurable
    /// icon and label. Designed for touch-first interfaces following mobile accessibility
    /// guidelines for minimum tap target size.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
    {
        private const string LogPrefix = "[ProjectLegacy]";

        /// <summary>Minimum touch target size in dp per mobile accessibility guidelines.</summary>
        private const float MinTouchTargetDp = 48f;

        [Header("Content")]
        [SerializeField]
        [Tooltip("Optional icon displayed inside the button.")]
        private Image _icon;

        [SerializeField]
        [Tooltip("Optional text label displayed inside the button.")]
        private Text _label;

        [SerializeField]
        [Tooltip("Text to display in the label. Ignored if Label is null.")]
        private string _labelText = "";

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Normal background color of the button.")]
        private Color _normalColor = new Color(0.2f, 0.45f, 0.85f, 1f);

        [SerializeField]
        [Tooltip("Background color when the button is pressed.")]
        private Color _pressedColor = new Color(0.15f, 0.35f, 0.7f, 1f);

        [SerializeField]
        [Tooltip("Background color when the button is disabled.")]
        private Color _disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        [SerializeField]
        [Tooltip("Icon tint color in normal state.")]
        private Color _iconNormalColor = Color.white;

        [SerializeField]
        [Tooltip("Icon tint color when pressed.")]
        private Color _iconPressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Header("Visual Feedback")]
        [SerializeField]
        [Range(0.8f, 1.0f)]
        [Tooltip("Scale multiplier applied when the button is pressed.")]
        private float _pressedScale = 0.92f;

        [SerializeField]
        [Range(0.05f, 0.3f)]
        [Tooltip("Duration of the press/release animation in seconds.")]
        private float _animationDuration = 0.1f;

        [Header("Haptic Feedback")]
        [SerializeField]
        [Tooltip("Enable haptic feedback on button press.")]
        private bool _hapticEnabled = true;

        [SerializeField]
        [Tooltip("Use medium haptic intensity instead of light.")]
        private bool _hapticMedium = false;

        [Header("Touch Target")]
        [SerializeField]
        [Tooltip("Minimum touch target size in dp. Enforced in Awake. Set to 0 to disable enforcement.")]
        private float _minTouchTargetDp = MinTouchTargetDp;

        /// <summary>Fired when the button is clicked/tapped.</summary>
        public event Action OnClick;

        /// <summary>Whether the button accepts input.</summary>
        public bool Interactable
        {
            get => _interactable;
            set
            {
                _interactable = value;
                UpdateVisualState();
            }
        }

        private RectTransform _rectTransform;
        private Image _backgroundImage;
        private bool _interactable = true;
        private bool _isPressed;
        private float _currentScale = 1f;
        private float _targetScale = 1f;
        private Color _currentBgColor;
        private Color _targetBgColor;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _backgroundImage = GetComponent<Image>();

            if (_backgroundImage == null)
            {
                Debug.LogError($"{LogPrefix} MobileButton on '{gameObject.name}' requires an Image component for the background.");
                enabled = false;
                return;
            }

            EnforceMinimumTouchTarget();
            UpdateLabelText();
        }

        private void OnEnable()
        {
            _isPressed = false;
            _currentScale = 1f;
            _targetScale = 1f;
            _currentBgColor = _interactable ? _normalColor : _disabledColor;
            _targetBgColor = _currentBgColor;
            UpdateVisualState();
        }

        private void Update()
        {
            AnimateVisuals();
        }

        /// <summary>
        /// Sets the button label text at runtime.
        /// </summary>
        /// <param name="text">The text to display.</param>
        public void SetLabel(string text)
        {
            _labelText = text ?? "";
            UpdateLabelText();
        }

        /// <summary>
        /// Sets the button icon sprite at runtime.
        /// </summary>
        /// <param name="sprite">The sprite to display, or null to hide the icon.</param>
        public void SetIcon(Sprite sprite)
        {
            if (_icon == null)
                return;

            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
        }

        /// <summary>
        /// Programmatically triggers the button click, including haptic feedback and the OnClick event.
        /// </summary>
        public void SimulateClick()
        {
            if (!_interactable || !isActiveAndEnabled)
                return;

            TriggerHaptic();
            OnClick?.Invoke();
        }

        // --- IPointer handlers ---

        /// <summary>Handles pointer down for press visual feedback.</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable)
                return;

            _isPressed = true;
            _targetScale = _pressedScale;
            _targetBgColor = _pressedColor;

            if (_icon != null)
                _icon.color = _iconPressedColor;

            TriggerHaptic();
        }

        /// <summary>Handles pointer up to release visual feedback.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed)
                return;

            _isPressed = false;
            _targetScale = 1f;
            _targetBgColor = _normalColor;

            if (_icon != null)
                _icon.color = _iconNormalColor;
        }

        /// <summary>Handles the click event.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_interactable)
                return;

            OnClick?.Invoke();
        }

        /// <summary>Handles pointer exit to cancel press state if finger moves off button.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isPressed)
                return;

            _isPressed = false;
            _targetScale = 1f;
            _targetBgColor = _interactable ? _normalColor : _disabledColor;

            if (_icon != null)
                _icon.color = _iconNormalColor;
        }

        private void AnimateVisuals()
        {
            if (_backgroundImage == null)
                return;

            float lerpSpeed = _animationDuration > 0f ? Time.unscaledDeltaTime / _animationDuration : 1f;
            lerpSpeed = Mathf.Clamp01(lerpSpeed);

            // Animate scale
            _currentScale = Mathf.Lerp(_currentScale, _targetScale, lerpSpeed);
            _rectTransform.localScale = new Vector3(_currentScale, _currentScale, 1f);

            // Animate background color
            _currentBgColor = Color.Lerp(_currentBgColor, _targetBgColor, lerpSpeed);
            _backgroundImage.color = _currentBgColor;
        }

        private void UpdateVisualState()
        {
            if (_backgroundImage == null)
                return;

            Color targetColor = _interactable ? _normalColor : _disabledColor;
            _targetBgColor = targetColor;
            _backgroundImage.color = targetColor;

            if (_icon != null)
            {
                _icon.color = _interactable ? _iconNormalColor : _disabledColor;
            }

            if (_label != null)
            {
                _label.color = _interactable ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }
        }

        private void UpdateLabelText()
        {
            if (_label != null)
            {
                _label.text = _labelText;
            }
        }

        private void TriggerHaptic()
        {
            if (!_hapticEnabled)
                return;

            try
            {
                if (_hapticMedium)
                    Util.HapticFeedback.MediumTap();
                else
                    Util.HapticFeedback.LightTap();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} MobileButton haptic feedback failed: {ex.Message}");
            }
        }

        private void EnforceMinimumTouchTarget()
        {
            if (_rectTransform == null || _minTouchTargetDp <= 0f)
                return;

            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            float minPixels = _minTouchTargetDp * (dpi / 160f);

            Vector2 size = _rectTransform.sizeDelta;
            bool adjusted = false;

            if (size.x < minPixels)
            {
                size.x = minPixels;
                adjusted = true;
            }

            if (size.y < minPixels)
            {
                size.y = minPixels;
                adjusted = true;
            }

            if (adjusted)
            {
                _rectTransform.sizeDelta = size;
                Debug.Log($"{LogPrefix} MobileButton on '{gameObject.name}' touch target expanded to {size.x:F0}x{size.y:F0}px (min {_minTouchTargetDp}dp).");
            }
        }
    }
}
