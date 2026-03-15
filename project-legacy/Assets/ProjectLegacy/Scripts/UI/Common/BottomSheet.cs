using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLegacy.UI.Common
{
    /// <summary>
    /// A bottom sheet overlay that slides up from the bottom of the screen. Used for
    /// item details, dialogue, confirmations, and other contextual content. Features a
    /// drag handle for gesture-driven interaction, three snap points (dismissed, half-screen,
    /// full-screen), momentum-based flinging, and programmatic Show/Hide/Toggle methods.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class BottomSheet : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const string LogPrefix = "[ProjectLegacy]";

        /// <summary>Snap point states for the bottom sheet.</summary>
        public enum SheetState
        {
            /// <summary>Sheet is fully hidden below the screen.</summary>
            Dismissed,
            /// <summary>Sheet is visible at half the screen height.</summary>
            Half,
            /// <summary>Sheet is expanded to near full screen.</summary>
            Full
        }

        [Header("Snap Points")]
        [SerializeField]
        [Range(0.3f, 0.6f)]
        [Tooltip("Fraction of screen height for the half-expanded snap point.")]
        private float _halfScreenFraction = 0.45f;

        [SerializeField]
        [Range(0.7f, 1.0f)]
        [Tooltip("Fraction of screen height for the full-expanded snap point.")]
        private float _fullScreenFraction = 0.92f;

        [SerializeField]
        [Tooltip("Top margin in pixels to prevent the sheet from covering the entire screen.")]
        private float _topMargin = 40f;

        [Header("Spring Physics")]
        [SerializeField]
        [Range(80f, 600f)]
        [Tooltip("Spring stiffness for snap animations.")]
        private float _springStiffness = 250f;

        [SerializeField]
        [Range(8f, 50f)]
        [Tooltip("Damping coefficient for the spring animation.")]
        private float _springDamping = 22f;

        [Header("Gesture")]
        [SerializeField]
        [Range(200f, 2000f)]
        [Tooltip("Minimum fling velocity (px/sec) to advance to the next snap point.")]
        private float _flingVelocityThreshold = 500f;

        [SerializeField]
        [Range(0.05f, 0.3f)]
        [Tooltip("Fraction of distance between snap points required to snap forward.")]
        private float _snapThreshold = 0.15f;

        [Header("Drag Handle")]
        [SerializeField]
        [Tooltip("Optional drag handle RectTransform displayed at the top of the sheet.")]
        private RectTransform _dragHandle;

        [SerializeField]
        [Tooltip("Width of the drag handle indicator in pixels.")]
        private float _handleWidth = 40f;

        [SerializeField]
        [Tooltip("Height of the drag handle indicator in pixels.")]
        private float _handleHeight = 4f;

        [Header("Visual Feedback")]
        [SerializeField]
        [Tooltip("Fade alpha of sheet when dismissed.")]
        private bool _fadeOnDismiss = true;

        [SerializeField]
        [Tooltip("Optional scrim overlay that darkens behind the sheet.")]
        private CanvasGroup _scrim;

        [SerializeField]
        [Range(0f, 0.8f)]
        [Tooltip("Maximum scrim alpha when the sheet is fully expanded.")]
        private float _scrimMaxAlpha = 0.5f;

        [Header("Haptic Feedback")]
        [SerializeField]
        [Tooltip("Enable haptic feedback when snapping to a new state.")]
        private bool _hapticOnSnap = true;

        /// <summary>Fired when the sheet reaches the dismissed state.</summary>
        public event Action OnDismissed;

        /// <summary>Fired when the sheet reaches the half-expanded state.</summary>
        public event Action OnHalfExpanded;

        /// <summary>Fired when the sheet reaches the full-expanded state.</summary>
        public event Action OnFullExpanded;

        /// <summary>Fired whenever the sheet state changes. Parameter is the new state.</summary>
        public event Action<SheetState> OnStateChanged;

        /// <summary>Current snap state of the bottom sheet.</summary>
        public SheetState CurrentState { get; private set; } = SheetState.Dismissed;

        /// <summary>Whether a drag gesture is in progress.</summary>
        public bool IsDragging { get; private set; }

        /// <summary>Normalized expansion progress (0 = dismissed, 1 = full).</summary>
        public float ExpansionProgress { get; private set; }

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private RectTransform _parentRectTransform;

        // Vertical positions (in anchoredPosition.y) for each snap point
        private float _dismissedY;
        private float _halfY;
        private float _fullY;

        // Spring state
        private float _currentY;
        private float _targetY;
        private float _springVelocity;
        private bool _isAnimating;

        // Drag tracking
        private float _dragStartY;
        private float _dragStartSheetY;
        private float _lastDragVelocity;
        private float _lastDragTime;
        private float _previousDragY;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_rectTransform == null || _canvasGroup == null)
            {
                Debug.LogError($"{LogPrefix} BottomSheet on '{gameObject.name}' requires RectTransform and CanvasGroup.");
                enabled = false;
                return;
            }

            Transform parentTransform = _rectTransform.parent;
            if (parentTransform != null)
            {
                _parentRectTransform = parentTransform as RectTransform;
            }

            // Ensure anchors are set for bottom-anchored layout
            _rectTransform.anchorMin = new Vector2(0f, 0f);
            _rectTransform.anchorMax = new Vector2(1f, 0f);
            _rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        private void OnEnable()
        {
            RecalculateSnapPoints();
            _currentY = _dismissedY;
            _targetY = _dismissedY;
            _springVelocity = 0f;
            _isAnimating = false;
            CurrentState = SheetState.Dismissed;

            ApplyPosition(_currentY);
            UpdateVisuals();
        }

        private void Update()
        {
            if (_isAnimating && !IsDragging)
            {
                StepSpringPhysics();
            }
        }

        /// <summary>
        /// Shows the bottom sheet, expanding to the half-screen snap point.
        /// </summary>
        public void Show()
        {
            Show(SheetState.Half);
        }

        /// <summary>
        /// Shows the bottom sheet at the specified snap point.
        /// </summary>
        /// <param name="targetState">Target state (Half or Full). Dismissed is ignored.</param>
        public void Show(SheetState targetState)
        {
            if (targetState == SheetState.Dismissed)
            {
                Debug.LogWarning($"{LogPrefix} BottomSheet.Show() called with Dismissed state. Use Hide() instead.");
                return;
            }

            gameObject.SetActive(true);
            RecalculateSnapPoints();
            SetTargetState(targetState);

            Debug.Log($"{LogPrefix} BottomSheet '{gameObject.name}' showing to {targetState}.");
        }

        /// <summary>
        /// Hides the bottom sheet by animating it to the dismissed state.
        /// </summary>
        public void Hide()
        {
            RecalculateSnapPoints();
            SetTargetState(SheetState.Dismissed);

            Debug.Log($"{LogPrefix} BottomSheet '{gameObject.name}' hiding.");
        }

        /// <summary>
        /// Toggles the bottom sheet between dismissed and half-expanded.
        /// </summary>
        public void Toggle()
        {
            if (CurrentState == SheetState.Dismissed)
                Show(SheetState.Half);
            else
                Hide();
        }

        /// <summary>
        /// Toggles the bottom sheet between the specified shown state and dismissed.
        /// </summary>
        /// <param name="shownState">The state to show when toggling on.</param>
        public void Toggle(SheetState shownState)
        {
            if (CurrentState == SheetState.Dismissed)
                Show(shownState);
            else
                Hide();
        }

        // --- IDrag handlers ---

        /// <summary>Begins tracking a vertical drag on the sheet.</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            IsDragging = true;
            _isAnimating = false;
            _springVelocity = 0f;
            _dragStartSheetY = _currentY;
            _previousDragY = eventData.position.y;
            _lastDragTime = Time.unscaledTime;
            _lastDragVelocity = 0f;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRectTransform != null ? _parentRectTransform : _rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint
            );
            _dragStartY = localPoint.y;
        }

        /// <summary>Applies vertical offset during drag, with rubber banding at extremes.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRectTransform != null ? _parentRectTransform : _rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint
            );

            float deltaY = localPoint.y - _dragStartY;
            float proposedY = _dragStartSheetY + deltaY;

            // Rubber band effect when dragging beyond full-expanded
            if (proposedY > _fullY)
            {
                float overshoot = proposedY - _fullY;
                proposedY = _fullY + overshoot * 0.2f;
            }

            // Rubber band effect when dragging below dismissed
            if (proposedY < _dismissedY)
            {
                float overshoot = _dismissedY - proposedY;
                proposedY = _dismissedY - overshoot * 0.2f;
            }

            _currentY = proposedY;
            ApplyPosition(_currentY);
            UpdateVisuals();

            // Track velocity for fling detection
            float now = Time.unscaledTime;
            float dt = now - _lastDragTime;
            if (dt > 0.001f)
            {
                float screenDeltaY = eventData.position.y - _previousDragY;
                _lastDragVelocity = screenDeltaY / dt;
                _lastDragTime = now;
                _previousDragY = eventData.position.y;
            }
        }

        /// <summary>Determines the target snap point based on position and fling velocity.</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging)
                return;

            IsDragging = false;

            SheetState targetState = DetermineTargetState(_lastDragVelocity);
            SetTargetState(targetState);
        }

        private SheetState DetermineTargetState(float velocity)
        {
            // If flung hard enough, advance in that direction
            if (Mathf.Abs(velocity) >= _flingVelocityThreshold)
            {
                if (velocity > 0f) // Flung upward
                {
                    switch (CurrentState)
                    {
                        case SheetState.Dismissed: return SheetState.Half;
                        case SheetState.Half: return SheetState.Full;
                        case SheetState.Full: return SheetState.Full;
                    }
                }
                else // Flung downward
                {
                    switch (CurrentState)
                    {
                        case SheetState.Full: return SheetState.Half;
                        case SheetState.Half: return SheetState.Dismissed;
                        case SheetState.Dismissed: return SheetState.Dismissed;
                    }
                }
            }

            // Otherwise, snap to the nearest snap point
            return FindNearestSnapState(_currentY);
        }

        private SheetState FindNearestSnapState(float y)
        {
            float distDismissed = Mathf.Abs(y - _dismissedY);
            float distHalf = Mathf.Abs(y - _halfY);
            float distFull = Mathf.Abs(y - _fullY);

            if (distDismissed <= distHalf && distDismissed <= distFull)
                return SheetState.Dismissed;
            if (distHalf <= distFull)
                return SheetState.Half;
            return SheetState.Full;
        }

        private void SetTargetState(SheetState state)
        {
            SheetState previousState = CurrentState;
            CurrentState = state;

            _targetY = GetYForState(state);
            _isAnimating = true;

            if (state != previousState)
            {
                TriggerHaptic();
                OnStateChanged?.Invoke(state);
            }
        }

        private float GetYForState(SheetState state)
        {
            switch (state)
            {
                case SheetState.Dismissed: return _dismissedY;
                case SheetState.Half: return _halfY;
                case SheetState.Full: return _fullY;
                default: return _dismissedY;
            }
        }

        private void StepSpringPhysics()
        {
            float displacement = _currentY - _targetY;
            float springForce = -_springStiffness * displacement;
            float dampingForce = -_springDamping * _springVelocity;
            float acceleration = springForce + dampingForce;

            float dt = Time.unscaledDeltaTime;
            _springVelocity += acceleration * dt;
            _currentY += _springVelocity * dt;

            ApplyPosition(_currentY);
            UpdateVisuals();

            // Check if spring has settled
            if (Mathf.Abs(displacement) < 0.5f && Mathf.Abs(_springVelocity) < 1f)
            {
                _currentY = _targetY;
                _springVelocity = 0f;
                _isAnimating = false;
                ApplyPosition(_currentY);
                UpdateVisuals();

                switch (CurrentState)
                {
                    case SheetState.Dismissed:
                        OnDismissed?.Invoke();
                        gameObject.SetActive(false);
                        break;
                    case SheetState.Half:
                        OnHalfExpanded?.Invoke();
                        break;
                    case SheetState.Full:
                        OnFullExpanded?.Invoke();
                        break;
                }
            }
        }

        private void ApplyPosition(float y)
        {
            if (_rectTransform == null)
                return;

            Vector2 pos = _rectTransform.anchoredPosition;
            pos.y = y;
            _rectTransform.anchoredPosition = pos;
        }

        private void UpdateVisuals()
        {
            float totalRange = _fullY - _dismissedY;
            if (totalRange > 0.001f)
            {
                ExpansionProgress = Mathf.Clamp01((_currentY - _dismissedY) / totalRange);
            }
            else
            {
                ExpansionProgress = 0f;
            }

            // Fade sheet alpha when near dismissed
            if (_fadeOnDismiss && _canvasGroup != null)
            {
                // Start fading only in the bottom 20% of travel
                float fadeProgress = Mathf.Clamp01(ExpansionProgress / 0.2f);
                _canvasGroup.alpha = fadeProgress;
            }

            // Scrim follows expansion progress
            if (_scrim != null)
            {
                _scrim.alpha = ExpansionProgress * _scrimMaxAlpha;
                _scrim.blocksRaycasts = ExpansionProgress > 0.01f;
            }
        }

        private void RecalculateSnapPoints()
        {
            float parentHeight = GetParentHeight();
            float sheetHeight = _rectTransform.rect.height;

            // With pivot at bottom and anchor at parent bottom:
            //   anchoredPosition.y = 0  => sheet bottom at parent bottom (fully visible if sheet fits)
            //   anchoredPosition.y = -sheetHeight => sheet entirely below parent (dismissed)
            //
            // The "visible height" at a given Y is: sheetHeight + Y (clamped to [0, sheetHeight]).
            // We want visible height to match each target fraction of parentHeight.

            // Dismissed: fully off-screen
            _dismissedY = -sheetHeight;

            // Half: visible portion equals halfScreenFraction * parentHeight
            float halfVisible = Mathf.Min(sheetHeight, parentHeight * _halfScreenFraction);
            _halfY = halfVisible - sheetHeight;

            // Full: visible portion equals fullScreenFraction * parentHeight minus top margin
            float fullVisible = Mathf.Min(sheetHeight, parentHeight * _fullScreenFraction - _topMargin);
            _fullY = fullVisible - sheetHeight;

            // Clamp full so we never exceed y=0 (sheet fully on-screen at its natural size)
            _fullY = Mathf.Min(_fullY, 0f);

            // Ensure ordering: dismissed <= half <= full
            _halfY = Mathf.Clamp(_halfY, _dismissedY, _fullY);

            // If sheet is shorter than the half-screen target, half collapses to full
            if (sheetHeight <= parentHeight * _halfScreenFraction)
            {
                _halfY = _fullY;
            }
        }

        private float GetParentHeight()
        {
            if (_parentRectTransform != null)
            {
                return _parentRectTransform.rect.height;
            }

            return Screen.height;
        }

        private void TriggerHaptic()
        {
            if (!_hapticOnSnap)
                return;

            try
            {
                Util.HapticFeedback.LightTap();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} BottomSheet haptic feedback failed: {ex.Message}");
            }
        }
    }
}
