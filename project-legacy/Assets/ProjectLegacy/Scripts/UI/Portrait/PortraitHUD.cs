using UnityEngine;
using DaggerfallWorkshop.Game;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.Portrait
{
    /// <summary>
    /// Root controller for the in-game portrait HUD. Manages child components
    /// (VitalsBar, CompassStrip, QuickSlotStrip, ActionButtonPanel) and
    /// provides HUD visibility toggling via swipe-down gesture.
    /// </summary>
    public class PortraitHUD : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Child Components")]
        [SerializeField]
        [Tooltip("Vitals bar (HP/MP/ST display).")]
        private VitalsBar _vitalsBar;

        [SerializeField]
        [Tooltip("Compass strip at the top of the viewport.")]
        private CompassStrip _compassStrip;

        [SerializeField]
        [Tooltip("Quick slot strip for item access.")]
        private QuickSlotStrip _quickSlotStrip;

        [SerializeField]
        [Tooltip("Optional floating action buttons.")]
        private ActionButtonPanel _actionButtonPanel;

        [Header("Visibility")]
        [SerializeField]
        [Tooltip("Whether the HUD is currently visible.")]
        private bool _isVisible = true;

        [SerializeField]
        [Range(0.1f, 0.5f)]
        [Tooltip("Fade animation duration in seconds.")]
        private float _fadeDuration = 0.25f;

        /// <summary>Whether the HUD is currently visible.</summary>
        public bool IsVisible => _isVisible;

        /// <summary>The vitals bar component.</summary>
        public VitalsBar VitalsBar => _vitalsBar;

        /// <summary>The compass strip component.</summary>
        public CompassStrip CompassStrip => _compassStrip;

        /// <summary>The quick slot strip component.</summary>
        public QuickSlotStrip QuickSlotStrip => _quickSlotStrip;

        /// <summary>The action button panel component.</summary>
        public ActionButtonPanel ActionButtons => _actionButtonPanel;

        private CanvasGroup _canvasGroup;
        private float _targetAlpha = 1f;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Create child components if not assigned
            EnsureChildComponents();
        }

        private void Start()
        {
            // Disable DFU's default HUD
            DisableDfuHud();

            // Apply initial visibility
            _canvasGroup.alpha = _isVisible ? 1f : 0f;
            _targetAlpha = _canvasGroup.alpha;
        }

        private void Update()
        {
            // Animate fade
            if (!Mathf.Approximately(_canvasGroup.alpha, _targetAlpha))
            {
                _canvasGroup.alpha = Mathf.MoveTowards(
                    _canvasGroup.alpha,
                    _targetAlpha,
                    Time.deltaTime / _fadeDuration
                );

                _canvasGroup.interactable = _canvasGroup.alpha > 0.5f;
                _canvasGroup.blocksRaycasts = _canvasGroup.alpha > 0.5f;
            }
        }

        /// <summary>
        /// Toggles HUD visibility with a fade animation.
        /// </summary>
        public void ToggleVisibility()
        {
            _isVisible = !_isVisible;
            _targetAlpha = _isVisible ? 1f : 0f;
            Debug.Log($"{LogPrefix} HUD visibility: {(_isVisible ? "shown" : "hidden")}");
        }

        /// <summary>
        /// Shows the HUD.
        /// </summary>
        public void Show()
        {
            _isVisible = true;
            _targetAlpha = 1f;
        }

        /// <summary>
        /// Hides the HUD.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _targetAlpha = 0f;
        }

        private void DisableDfuHud()
        {
            // Disable DFU's built-in HUD when portrait mode is active
            if (DaggerfallUI.Instance != null)
            {
                var dfuHud = DaggerfallUI.Instance.DaggerfallHUD;
                if (dfuHud != null)
                {
                    dfuHud.Enabled = false;
                    Debug.Log($"{LogPrefix} DFU default HUD disabled");
                }
            }
        }

        private void EnsureChildComponents()
        {
            if (_vitalsBar == null)
            {
                var obj = new GameObject("VitalsBar");
                obj.transform.SetParent(transform);
                _vitalsBar = obj.AddComponent<VitalsBar>();
            }

            if (_compassStrip == null)
            {
                var obj = new GameObject("CompassStrip");
                obj.transform.SetParent(transform);
                _compassStrip = obj.AddComponent<CompassStrip>();
            }

            if (_quickSlotStrip == null)
            {
                var obj = new GameObject("QuickSlotStrip");
                obj.transform.SetParent(transform);
                _quickSlotStrip = obj.AddComponent<QuickSlotStrip>();
            }

            if (_actionButtonPanel == null)
            {
                var settings = LegacyBootstrapper.Instance != null
                    ? LegacyBootstrapper.Instance.Settings
                    : null;

                bool showButtons = settings != null ? settings.ShowActionButtons : true;
                if (showButtons)
                {
                    var obj = new GameObject("ActionButtonPanel");
                    obj.transform.SetParent(transform);
                    _actionButtonPanel = obj.AddComponent<ActionButtonPanel>();
                }
            }
        }
    }
}
