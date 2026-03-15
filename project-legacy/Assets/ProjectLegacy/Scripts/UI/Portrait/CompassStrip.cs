using UnityEngine;
using DaggerfallWorkshop.Game;

namespace ProjectLegacy.UI.Portrait
{
    /// <summary>
    /// Thin compass bar at the top of the viewport showing cardinal and
    /// intercardinal directions. Scrolls horizontally as the player rotates.
    /// Reads heading data from DFU's existing compass system.
    /// </summary>
    public class CompassStrip : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Appearance")]
        [SerializeField]
        [Tooltip("Background color of the compass strip.")]
        private Color _backgroundColor = new Color(0f, 0f, 0f, 0.5f);

        [SerializeField]
        [Tooltip("Color for cardinal directions (N, S, E, W).")]
        private Color _cardinalColor = Color.white;

        [SerializeField]
        [Tooltip("Color for intercardinal directions (NE, SE, SW, NW).")]
        private Color _intercardinalColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        [SerializeField]
        [Tooltip("Color for the current heading indicator.")]
        private Color _headingIndicatorColor = new Color(1f, 0.8f, 0.2f, 1f);

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Height of the compass strip in pixels.")]
        private float _stripHeight = 24f;

        [SerializeField]
        [Tooltip("Font size for direction labels.")]
        private int _fontSize = 12;

        private readonly string[] _directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        private readonly float[] _directionAngles = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        private Texture2D _bgTexture;
        private Texture2D _indicatorTexture;
        private GUIStyle _cardinalStyle;
        private GUIStyle _intercardinalStyle;
        private bool _stylesInitialized;

        private void Start()
        {
            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, Color.white);
            _bgTexture.Apply();

            _indicatorTexture = new Texture2D(1, 1);
            _indicatorTexture.SetPixel(0, 0, Color.white);
            _indicatorTexture.Apply();
        }

        private void OnDestroy()
        {
            if (_bgTexture != null) Destroy(_bgTexture);
            if (_indicatorTexture != null) Destroy(_indicatorTexture);
        }

        private void OnGUI()
        {
            if (_bgTexture == null)
                return;

            InitStyles();

            float heading = GetPlayerHeading();

            var layout = Core.LegacyBootstrapper.Instance != null
                ? Core.LegacyBootstrapper.Instance.ScreenLayout
                : null;

            float stripX, stripY, stripWidth;

            if (layout != null)
            {
                stripX = layout.TopBarRect.x;
                stripY = Screen.height - layout.TopBarRect.y - layout.TopBarRect.height;
                stripWidth = layout.TopBarRect.width;
            }
            else
            {
                stripX = 0;
                stripY = 0;
                stripWidth = Screen.width;
            }

            // Draw background
            GUI.color = _backgroundColor;
            GUI.DrawTexture(new Rect(stripX, stripY, stripWidth, _stripHeight), _bgTexture);

            // Draw heading indicator (small triangle at center bottom)
            GUI.color = _headingIndicatorColor;
            float indicatorWidth = 8f;
            float indicatorHeight = 4f;
            GUI.DrawTexture(new Rect(
                stripX + stripWidth / 2f - indicatorWidth / 2f,
                stripY + _stripHeight - indicatorHeight,
                indicatorWidth,
                indicatorHeight
            ), _indicatorTexture);

            // Draw direction labels
            float visibleRange = 180f; // degrees visible on screen
            float degreesPerPixel = visibleRange / stripWidth;

            for (int i = 0; i < _directions.Length; i++)
            {
                float dirAngle = _directionAngles[i];
                float angleDiff = Mathf.DeltaAngle(heading, dirAngle);

                // Only draw if within visible range
                if (Mathf.Abs(angleDiff) > visibleRange / 2f)
                    continue;

                float xOffset = angleDiff / degreesPerPixel;
                float labelX = stripX + stripWidth / 2f + xOffset;

                bool isCardinal = (i % 2 == 0);
                GUIStyle style = isCardinal ? _cardinalStyle : _intercardinalStyle;

                Vector2 labelSize = style.CalcSize(new GUIContent(_directions[i]));
                GUI.Label(
                    new Rect(labelX - labelSize.x / 2f, stripY + 2, labelSize.x, _stripHeight - 4),
                    _directions[i],
                    style
                );

                // Draw tick mark below cardinal directions
                if (isCardinal)
                {
                    GUI.color = _cardinalColor;
                    GUI.DrawTexture(new Rect(labelX - 0.5f, stripY + _stripHeight - 6, 1, 4), _bgTexture);
                }
            }

            GUI.color = Color.white;
        }

        private float GetPlayerHeading()
        {
            if (GameManager.Instance == null)
                return 0f;

            var mouseLook = GameManager.Instance.PlayerMouseLook;
            if (mouseLook == null)
                return 0f;

            // DFU's Yaw is the horizontal rotation angle
            float yaw = mouseLook.Yaw;
            // Normalize to 0-360
            yaw = ((yaw % 360f) + 360f) % 360f;
            return yaw;
        }

        private void InitStyles()
        {
            if (_stylesInitialized)
                return;

            _cardinalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _cardinalColor }
            };

            _intercardinalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize - 2,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _intercardinalColor }
            };

            _stylesInitialized = true;
        }
    }
}
