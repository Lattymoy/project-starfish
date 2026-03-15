using UnityEngine;

namespace ProjectLegacy.Core
{
    /// <summary>
    /// Reads device screen properties (safe area, notch, gesture nav bar)
    /// and provides layout Rects that all UI elements use for positioning.
    /// Handles aspect ratios from 16:9 to 21:9+.
    /// </summary>
    public class ScreenLayoutResolver : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        /// <summary>The usable screen area after safe area insets.</summary>
        public Rect SafeArea { get; private set; }

        /// <summary>Top inset in pixels (notch/cutout at the top).</summary>
        public float TopInset { get; private set; }

        /// <summary>Bottom inset in pixels (gesture nav bar, home indicator).</summary>
        public float BottomInset { get; private set; }

        /// <summary>Left inset in pixels.</summary>
        public float LeftInset { get; private set; }

        /// <summary>Right inset in pixels.</summary>
        public float RightInset { get; private set; }

        /// <summary>The screen aspect ratio (height / width). Portrait 16:9 ≈ 1.78, 21:9 ≈ 2.33.</summary>
        public float AspectRatio { get; private set; }

        /// <summary>Screen category based on physical size and density.</summary>
        public ScreenCategory Category { get; private set; }

        /// <summary>Rect for the look zone (upper portion of usable viewport).</summary>
        public Rect LookZoneRect { get; private set; }

        /// <summary>Rect for the move zone (lower portion of usable viewport).</summary>
        public Rect MoveZoneRect { get; private set; }

        /// <summary>Rect for the top HUD bar (compass strip).</summary>
        public Rect TopBarRect { get; private set; }

        /// <summary>Rect for the bottom HUD bar (vitals, quick slots).</summary>
        public Rect BottomBarRect { get; private set; }

        [Header("HUD Layout")]
        [SerializeField]
        [Tooltip("Height of the top HUD bar in dp (compass strip).")]
        private float _topBarHeightDp = 20f;

        [SerializeField]
        [Tooltip("Height of the bottom HUD bar in dp (vitals, quick slots).")]
        private float _bottomBarHeightDp = 80f;

        private Rect _lastSafeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private void OnEnable()
        {
            Recalculate();
        }

        private void Update()
        {
            // Check for screen geometry changes
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenWidth != Screen.width ||
                _lastScreenHeight != Screen.height)
            {
                Recalculate();
            }
        }

        /// <summary>
        /// Forces a recalculation of all layout values.
        /// Call this when settings change or screen geometry updates.
        /// </summary>
        public void Recalculate()
        {
            _lastSafeArea = Screen.safeArea;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            CalculateSafeArea();
            CalculateAspectRatio();
            CalculateScreenCategory();
            CalculateZones();

            Debug.Log($"{LogPrefix} Layout recalculated — SafeArea: {SafeArea}, Aspect: {AspectRatio:F2}, Category: {Category}");
        }

        /// <summary>
        /// Converts dp (density-independent pixels) to screen pixels.
        /// </summary>
        /// <param name="dp">Value in dp.</param>
        /// <returns>Value in screen pixels.</returns>
        public float DpToPixels(float dp)
        {
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            return dp * (dpi / 160f);
        }

        /// <summary>
        /// Converts screen pixels to dp.
        /// </summary>
        /// <param name="pixels">Value in screen pixels.</param>
        /// <returns>Value in dp.</returns>
        public float PixelsToDp(float pixels)
        {
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            return pixels * (160f / dpi);
        }

        private void CalculateSafeArea()
        {
            SafeArea = Screen.safeArea;

            TopInset = SafeArea.y;
            BottomInset = Screen.height - (SafeArea.y + SafeArea.height);
            LeftInset = SafeArea.x;
            RightInset = Screen.width - (SafeArea.x + SafeArea.width);
        }

        private void CalculateAspectRatio()
        {
            if (Screen.width > 0)
            {
                AspectRatio = (float)Screen.height / Screen.width;
            }
        }

        private void CalculateScreenCategory()
        {
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            float diagonalPixels = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);
            float diagonalInches = diagonalPixels / dpi;

            if (diagonalInches < 5.0f)
                Category = ScreenCategory.Small;
            else if (diagonalInches < 6.5f)
                Category = ScreenCategory.Medium;
            else if (diagonalInches < 8.0f)
                Category = ScreenCategory.Large;
            else
                Category = ScreenCategory.Tablet;
        }

        private void CalculateZones()
        {
            float lookZoneRatio = 0.6f;
            if (LegacyBootstrapper.Instance != null && LegacyBootstrapper.Instance.Settings != null)
            {
                lookZoneRatio = LegacyBootstrapper.Instance.Settings.LookZoneRatio;
            }

            float topBarPx = DpToPixels(_topBarHeightDp);
            float bottomBarPx = DpToPixels(_bottomBarHeightDp);

            // Top bar sits at the top of the safe area
            TopBarRect = new Rect(
                SafeArea.x,
                SafeArea.y + SafeArea.height - topBarPx,
                SafeArea.width,
                topBarPx
            );

            // Bottom bar sits at the bottom of the safe area
            BottomBarRect = new Rect(
                SafeArea.x,
                SafeArea.y,
                SafeArea.width,
                bottomBarPx
            );

            // Viewport area = safe area minus HUD bars
            float viewportBottom = SafeArea.y + bottomBarPx;
            float viewportTop = SafeArea.y + SafeArea.height - topBarPx;
            float viewportHeight = viewportTop - viewportBottom;

            float lookHeight = viewportHeight * lookZoneRatio;
            float moveHeight = viewportHeight * (1f - lookZoneRatio);

            // Look zone is the upper portion
            LookZoneRect = new Rect(
                SafeArea.x,
                viewportBottom + moveHeight,
                SafeArea.width,
                lookHeight
            );

            // Move zone is the lower portion
            MoveZoneRect = new Rect(
                SafeArea.x,
                viewportBottom,
                SafeArea.width,
                moveHeight
            );
        }
    }

    /// <summary>
    /// Screen size categories based on physical diagonal size.
    /// </summary>
    public enum ScreenCategory
    {
        /// <summary>Small phones (under 5").</summary>
        Small,
        /// <summary>Standard phones (5"–6.5").</summary>
        Medium,
        /// <summary>Large phones / phablets (6.5"–8").</summary>
        Large,
        /// <summary>Tablets (8"+).</summary>
        Tablet
    }
}
