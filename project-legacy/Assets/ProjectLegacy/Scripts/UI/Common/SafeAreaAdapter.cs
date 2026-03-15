using UnityEngine;

namespace ProjectLegacy.UI.Common
{
    /// <summary>
    /// Automatically adjusts a RectTransform's anchors and offsets to respect
    /// <see cref="Screen.safeArea"/>, ensuring UI content avoids notches, rounded
    /// corners, and system gesture bars on any edge. Attach to any RectTransform
    /// that should be inset to the safe area. Updates continuously when the screen
    /// geometry or device orientation changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaAdapter : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Edge Padding")]
        [SerializeField]
        [Tooltip("Apply safe area inset on the left edge.")]
        private bool _applyLeft = true;

        [SerializeField]
        [Tooltip("Apply safe area inset on the right edge.")]
        private bool _applyRight = true;

        [SerializeField]
        [Tooltip("Apply safe area inset on the top edge.")]
        private bool _applyTop = true;

        [SerializeField]
        [Tooltip("Apply safe area inset on the bottom edge.")]
        private bool _applyBottom = true;

        [Header("Extra Padding")]
        [SerializeField]
        [Tooltip("Additional padding in pixels added inside the safe area on each edge.")]
        private RectOffset _extraPadding = new RectOffset(0, 0, 0, 0);

        [Header("Behavior")]
        [SerializeField]
        [Tooltip("When true, logs safe area changes to the console.")]
        private bool _debugLogging = false;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private ScreenOrientation _lastOrientation;

        /// <summary>The most recently applied safe area in screen coordinates.</summary>
        public Rect CurrentSafeArea => _lastSafeArea;

        /// <summary>Whether the adapter has applied at least one update.</summary>
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                Debug.LogError($"{LogPrefix} SafeAreaAdapter requires a RectTransform but none was found on '{gameObject.name}'.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            if (HasScreenGeometryChanged())
            {
                ApplySafeArea();
            }
        }

        /// <summary>
        /// Forces an immediate recalculation and application of the safe area.
        /// Call this after programmatically changing edge flags or extra padding.
        /// </summary>
        public void ForceUpdate()
        {
            ApplySafeArea();
        }

        /// <summary>
        /// Sets which edges should respect the safe area inset at runtime.
        /// </summary>
        /// <param name="left">Apply inset on the left edge.</param>
        /// <param name="right">Apply inset on the right edge.</param>
        /// <param name="top">Apply inset on the top edge.</param>
        /// <param name="bottom">Apply inset on the bottom edge.</param>
        public void SetEdges(bool left, bool right, bool top, bool bottom)
        {
            _applyLeft = left;
            _applyRight = right;
            _applyTop = top;
            _applyBottom = bottom;
            ApplySafeArea();
        }

        private bool HasScreenGeometryChanged()
        {
            return Screen.safeArea != _lastSafeArea
                || Screen.width != _lastScreenWidth
                || Screen.height != _lastScreenHeight
                || Screen.orientation != _lastOrientation;
        }

        private void ApplySafeArea()
        {
            if (_rectTransform == null)
                return;

            Rect safeArea = Screen.safeArea;
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            if (screenWidth <= 0 || screenHeight <= 0)
                return;

            _lastSafeArea = safeArea;
            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            _lastOrientation = Screen.orientation;

            // Convert safe area from screen pixels to normalized anchor coordinates.
            // Screen.safeArea origin is bottom-left; RectTransform anchors are also bottom-left.
            float leftAnchor = _applyLeft ? safeArea.x / screenWidth : 0f;
            float rightAnchor = _applyRight ? (safeArea.x + safeArea.width) / screenWidth : 1f;
            float bottomAnchor = _applyBottom ? safeArea.y / screenHeight : 0f;
            float topAnchor = _applyTop ? (safeArea.y + safeArea.height) / screenHeight : 1f;

            // Clamp anchors to valid range
            leftAnchor = Mathf.Clamp01(leftAnchor);
            rightAnchor = Mathf.Clamp01(rightAnchor);
            bottomAnchor = Mathf.Clamp01(bottomAnchor);
            topAnchor = Mathf.Clamp01(topAnchor);

            _rectTransform.anchorMin = new Vector2(leftAnchor, bottomAnchor);
            _rectTransform.anchorMax = new Vector2(rightAnchor, topAnchor);

            // Apply extra padding as offsets (inward from anchors).
            // offsetMin is bottom-left corner offset; offsetMax is top-right corner offset.
            if (_extraPadding != null)
            {
                _rectTransform.offsetMin = new Vector2(_extraPadding.left, _extraPadding.bottom);
                _rectTransform.offsetMax = new Vector2(-_extraPadding.right, -_extraPadding.top);
            }
            else
            {
                _rectTransform.offsetMin = Vector2.zero;
                _rectTransform.offsetMax = Vector2.zero;
            }

            IsInitialized = true;

            if (_debugLogging)
            {
                float topInset = screenHeight - (safeArea.y + safeArea.height);
                float bottomInset = safeArea.y;
                float leftInset = safeArea.x;
                float rightInset = screenWidth - (safeArea.x + safeArea.width);

                Debug.Log($"{LogPrefix} SafeAreaAdapter on '{gameObject.name}' updated — " +
                    $"SafeArea: {safeArea}, Insets: L={leftInset:F0} R={rightInset:F0} " +
                    $"T={topInset:F0} B={bottomInset:F0}, Orientation: {Screen.orientation}");
            }
        }
    }
}
