using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLegacy.UI.Common
{
    /// <summary>
    /// A panel that can be swiped away in a configurable direction (left, right, or down)
    /// to dismiss. Used for inventory, character sheet, and other overlay panels. Tracks
    /// touch drag input, applies a position offset in real time, and snaps to either the
    /// dismissed or shown state using spring physics for a natural feel.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SwipePanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const string LogPrefix = "[ProjectLegacy]";

        /// <summary>Direction in which the panel can be swiped to dismiss.</summary>
        public enum DismissDirection
        {
            /// <summary>Swipe left to dismiss.</summary>
            Left,
            /// <summary>Swipe right to dismiss.</summary>
            Right,
            /// <summary>Swipe down to dismiss.</summary>
            Down
        }

        [Header("Dismiss Settings")]
        [SerializeField]
        [Tooltip("Direction the panel must be swiped to dismiss.")]
        private DismissDirection _dismissDirection = DismissDirection.Right;

        [SerializeField]
        [Range(0.1f, 0.5f)]
        [Tooltip("Fraction of panel dimension that must be dragged to trigger dismiss.")]
        private float _dismissThreshold = 0.3f;

        [SerializeField]
        [Range(100f, 2000f)]
        [Tooltip("Minimum fling velocity (px/sec) that triggers dismiss regardless of distance.")]
        private float _flingVelocityThreshold = 600f;

        [Header("Spring Physics")]
        [SerializeField]
        [Range(50f, 500f)]
        [Tooltip("Spring stiffness for the snap animation.")]
        private float _springStiffness = 200f;

        [SerializeField]
        [Range(5f, 40f)]
        [Tooltip("Damping coefficient for the spring animation.")]
        private float _springDamping = 18f;

        [Header("Visual Feedback")]
        [SerializeField]
        [Tooltip("Fade the panel's alpha as it is dragged away.")]
        private bool _fadeOnDrag = true;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Minimum alpha when fully dragged to dismiss position.")]
        private float _minAlpha = 0.2f;

        [SerializeField]
        [Tooltip("Optional scrim (dark overlay behind the panel) that fades with the panel.")]
        private CanvasGroup _scrim;

        /// <summary>Fired when the panel has been dismissed via swipe.</summary>
        public event Action OnDismissed;

        /// <summary>Fired when the panel has snapped back to the shown position.</summary>
        public event Action OnShown;

        /// <summary>Whether the panel is currently in the shown state.</summary>
        public bool IsShown { get; private set; } = true;

        /// <summary>Whether a drag is currently in progress.</summary>
        public bool IsDragging { get; private set; }

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Canvas _parentCanvas;

        // Spring state
        private Vector2 _shownPosition;
        private Vector2 _dismissedPosition;
        private Vector2 _currentPosition;
        private Vector2 _springVelocity;
        private Vector2 _targetPosition;
        private bool _isAnimating;

        // Drag tracking
        private Vector2 _dragStartPosition;
        private Vector2 _dragStartPanelPosition;
        private Vector2 _lastDragDelta;
        private float _dragStartTime;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_rectTransform == null || _canvasGroup == null)
            {
                Debug.LogError($"{LogPrefix} SwipePanel on '{gameObject.name}' requires RectTransform and CanvasGroup.");
                enabled = false;
                return;
            }

            _parentCanvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            _shownPosition = _rectTransform.anchoredPosition;
            _currentPosition = _shownPosition;
            _dismissedPosition = CalculateDismissedPosition();
            _targetPosition = _shownPosition;
            _springVelocity = Vector2.zero;
            _isAnimating = false;
            IsShown = true;

            UpdateVisuals(0f);
        }

        private void Update()
        {
            if (_isAnimating && !IsDragging)
            {
                StepSpringPhysics();
            }
        }

        /// <summary>
        /// Programmatically shows the panel with a spring animation.
        /// </summary>
        public void Show()
        {
            if (_rectTransform == null)
                return;

            _dismissedPosition = CalculateDismissedPosition();
            _targetPosition = _shownPosition;
            _isAnimating = true;
            IsShown = true;
            gameObject.SetActive(true);

            Debug.Log($"{LogPrefix} SwipePanel '{gameObject.name}' showing.");
        }

        /// <summary>
        /// Programmatically dismisses the panel with a spring animation.
        /// </summary>
        public void Dismiss()
        {
            if (_rectTransform == null)
                return;

            _dismissedPosition = CalculateDismissedPosition();
            _targetPosition = _dismissedPosition;
            _isAnimating = true;
            IsShown = false;

            Debug.Log($"{LogPrefix} SwipePanel '{gameObject.name}' dismissing.");
        }

        /// <summary>
        /// Toggles between shown and dismissed states.
        /// </summary>
        public void Toggle()
        {
            if (IsShown)
                Dismiss();
            else
                Show();
        }

        // --- IDrag handlers ---

        /// <summary>Begins tracking a drag gesture on the panel.</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsShown && !_isAnimating)
                return;

            IsDragging = true;
            _isAnimating = false;
            _springVelocity = Vector2.zero;
            _dragStartPanelPosition = _currentPosition;
            _dragStartTime = Time.unscaledTime;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out _dragStartPosition
            );
        }

        /// <summary>Updates the panel position during a drag, constrained to the dismiss axis.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint
            );

            Vector2 rawDelta = localPoint - _dragStartPosition;
            Vector2 constrainedDelta = ConstrainToDismissAxis(rawDelta);

            // Apply resistance when dragging opposite to dismiss direction
            float projection = ProjectOnDismissAxis(constrainedDelta);
            if (projection < 0f)
            {
                constrainedDelta *= 0.3f; // Rubber band effect
            }

            _currentPosition = _dragStartPanelPosition + constrainedDelta;
            _lastDragDelta = eventData.delta;
            ApplyPosition(_currentPosition);

            float progress = CalculateDismissProgress();
            UpdateVisuals(progress);
        }

        /// <summary>Determines whether to dismiss or snap back based on drag distance and velocity.</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging)
                return;

            IsDragging = false;

            float progress = CalculateDismissProgress();
            float velocity = CalculateFlingVelocity(eventData);

            bool shouldDismiss = progress >= _dismissThreshold || velocity >= _flingVelocityThreshold;

            _dismissedPosition = CalculateDismissedPosition();

            if (shouldDismiss)
            {
                _targetPosition = _dismissedPosition;
                IsShown = false;
                // Give the spring an initial velocity boost from the fling
                _springVelocity = GetDismissDirectionVector() * velocity;
            }
            else
            {
                _targetPosition = _shownPosition;
                IsShown = true;
            }

            _isAnimating = true;
        }

        private void StepSpringPhysics()
        {
            Vector2 displacement = _currentPosition - _targetPosition;
            Vector2 springForce = -_springStiffness * displacement;
            Vector2 dampingForce = -_springDamping * _springVelocity;
            Vector2 acceleration = springForce + dampingForce;

            float dt = Time.unscaledDeltaTime;
            _springVelocity += acceleration * dt;
            _currentPosition += _springVelocity * dt;

            ApplyPosition(_currentPosition);

            float progress = CalculateDismissProgress();
            UpdateVisuals(Mathf.Clamp01(progress));

            // Check if spring has settled
            if (displacement.sqrMagnitude < 0.5f && _springVelocity.sqrMagnitude < 1f)
            {
                _currentPosition = _targetPosition;
                _springVelocity = Vector2.zero;
                _isAnimating = false;
                ApplyPosition(_currentPosition);

                if (IsShown)
                {
                    UpdateVisuals(0f);
                    OnShown?.Invoke();
                }
                else
                {
                    UpdateVisuals(1f);
                    OnDismissed?.Invoke();
                    gameObject.SetActive(false);
                }
            }
        }

        private void ApplyPosition(Vector2 position)
        {
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = position;
            }
        }

        private void UpdateVisuals(float dismissProgress)
        {
            if (_canvasGroup == null)
                return;

            if (_fadeOnDrag)
            {
                float alpha = Mathf.Lerp(1f, _minAlpha, dismissProgress);
                _canvasGroup.alpha = alpha;
            }

            if (_scrim != null)
            {
                _scrim.alpha = 1f - dismissProgress;
            }
        }

        private float CalculateDismissProgress()
        {
            Vector2 totalTravel = _dismissedPosition - _shownPosition;
            float totalDistance = totalTravel.magnitude;

            if (totalDistance < 0.001f)
                return 0f;

            Vector2 currentTravel = _currentPosition - _shownPosition;
            float projection = Vector2.Dot(currentTravel, totalTravel.normalized);

            return Mathf.Clamp01(projection / totalDistance);
        }

        private float CalculateFlingVelocity(PointerEventData eventData)
        {
            float elapsed = Time.unscaledTime - _dragStartTime;
            if (elapsed < 0.001f)
                return 0f;

            Vector2 dirVector = GetDismissDirectionVector();
            float axialDelta = Vector2.Dot(eventData.delta, dirVector);

            // Use delta per frame converted to per-second
            float dt = Time.unscaledDeltaTime;
            if (dt < 0.001f)
                return 0f;

            return Mathf.Max(0f, axialDelta / dt);
        }

        private float ProjectOnDismissAxis(Vector2 delta)
        {
            return Vector2.Dot(delta, GetDismissDirectionVector());
        }

        private Vector2 ConstrainToDismissAxis(Vector2 delta)
        {
            Vector2 dir = GetDismissDirectionVector();
            float projection = Vector2.Dot(delta, dir);
            return dir * projection;
        }

        private Vector2 GetDismissDirectionVector()
        {
            switch (_dismissDirection)
            {
                case DismissDirection.Left:
                    return Vector2.left;
                case DismissDirection.Right:
                    return Vector2.right;
                case DismissDirection.Down:
                    return Vector2.down;
                default:
                    return Vector2.right;
            }
        }

        private Vector2 CalculateDismissedPosition()
        {
            if (_rectTransform == null)
                return _shownPosition;

            Rect rect = _rectTransform.rect;
            Vector2 dir = GetDismissDirectionVector();

            // Move the panel fully off-screen along the dismiss axis.
            // Use the panel's own size plus a margin to ensure it is completely hidden.
            float panelExtent;
            if (_dismissDirection == DismissDirection.Down)
            {
                panelExtent = rect.height + 50f;
            }
            else
            {
                panelExtent = rect.width + 50f;
            }

            return _shownPosition + dir * panelExtent;
        }
    }
}
