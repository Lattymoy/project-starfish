using UnityEngine;
using DaggerfallWorkshop.Game;

namespace ProjectLegacy.Input
{
    /// <summary>
    /// Converts swipe direction vectors into DFU WeaponManager swing modes.
    /// DFU already supports directional weapon swings — this maps touch
    /// gestures to those existing inputs.
    /// </summary>
    public class SwipeDirectionMapper : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Direction Thresholds")]
        [SerializeField]
        [Range(0.3f, 0.8f)]
        [Tooltip("Minimum dot product with a cardinal direction to classify the swipe. Lower = more lenient.")]
        private float _directionThreshold = 0.5f;

        /// <summary>
        /// Maps a swipe direction vector to a DFU mouse direction for weapon swings.
        /// </summary>
        /// <param name="swipeDirection">Normalized swipe direction from GestureRecognizer.</param>
        /// <returns>The corresponding MouseDirections value for DFU's weapon system.</returns>
        public MouseDirections MapSwipeToWeaponDirection(Vector2 swipeDirection)
        {
            if (swipeDirection.sqrMagnitude < 0.01f)
                return MouseDirections.None;

            Vector2 dir = swipeDirection.normalized;

            // Check each cardinal direction
            float dotUp = Vector2.Dot(dir, Vector2.up);
            float dotDown = Vector2.Dot(dir, Vector2.down);
            float dotLeft = Vector2.Dot(dir, Vector2.left);
            float dotRight = Vector2.Dot(dir, Vector2.right);

            // Find the strongest match
            float maxDot = Mathf.Max(Mathf.Max(dotUp, dotDown), Mathf.Max(dotLeft, dotRight));

            if (maxDot < _directionThreshold)
            {
                // Ambiguous direction — default to a downward swing
                return MouseDirections.Down;
            }

            if (Mathf.Approximately(maxDot, dotUp))
                return MouseDirections.Up;
            if (Mathf.Approximately(maxDot, dotDown))
                return MouseDirections.Down;
            if (Mathf.Approximately(maxDot, dotLeft))
                return MouseDirections.Left;
            if (Mathf.Approximately(maxDot, dotRight))
                return MouseDirections.Right;

            return MouseDirections.Down;
        }

        /// <summary>
        /// Maps a swipe direction vector to a descriptive string for debugging.
        /// </summary>
        /// <param name="swipeDirection">Normalized swipe direction.</param>
        /// <returns>Human-readable direction name.</returns>
        public string GetDirectionName(Vector2 swipeDirection)
        {
            var mapped = MapSwipeToWeaponDirection(swipeDirection);
            return mapped switch
            {
                MouseDirections.Up => "Up",
                MouseDirections.Down => "Down",
                MouseDirections.Left => "Left",
                MouseDirections.Right => "Right",
                _ => "None"
            };
        }

        /// <summary>
        /// Calculates attack speed modifier based on swipe velocity.
        /// Faster swipes result in faster attack animations.
        /// </summary>
        /// <param name="swipeSpeed">Swipe speed in pixels per second from GestureRecognizer.</param>
        /// <returns>Speed modifier (0.5 = slow, 1.0 = normal, 1.5 = fast).</returns>
        public float CalculateAttackSpeedModifier(float swipeSpeed)
        {
            // Normalize swipe speed: 200 px/s = slow, 800 px/s = fast
            float normalized = Mathf.InverseLerp(200f, 800f, swipeSpeed);
            return Mathf.Lerp(0.5f, 1.5f, normalized);
        }
    }
}
