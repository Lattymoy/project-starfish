using UnityEngine;
using DaggerfallWorkshop.Game;
using ProjectLegacy.Core;
using ProjectLegacy.Input;

namespace ProjectLegacy.Combat
{
    /// <summary>
    /// Bridges gesture input with DFU's weapon system. Listens to swipe
    /// events from GestureRecognizer, maps them to weapon swing directions
    /// via SwipeDirectionMapper, and triggers attacks through DFU's WeaponManager.
    /// Provides haptic feedback on hit confirmation.
    /// </summary>
    public class SwipeCombatHandler : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Combat Settings")]
        [SerializeField]
        [Range(0.1f, 0.5f)]
        [Tooltip("Minimum time in seconds between attacks to prevent spam.")]
        private float _attackCooldown = 0.25f;

        [SerializeField]
        [Tooltip("Enable haptic feedback on hit confirmation.")]
        private bool _hapticOnHit = true;

        [Header("References")]
        [SerializeField]
        [Tooltip("Swipe direction mapper. Auto-created if not assigned.")]
        private SwipeDirectionMapper _directionMapper;

        [SerializeField]
        [Tooltip("Lock-on system reference. Auto-found if not assigned.")]
        private LockOnSystem _lockOnSystem;

        private GestureRecognizer _gestureRecognizer;
        private float _lastAttackTime;

        private void Awake()
        {
            if (_directionMapper == null)
                _directionMapper = gameObject.AddComponent<SwipeDirectionMapper>();
        }

        private void OnEnable()
        {
            SubscribeToGestures();
        }

        private void OnDisable()
        {
            UnsubscribeFromGestures();
        }

        private void Start()
        {
            // Find lock-on system
            if (_lockOnSystem == null)
                _lockOnSystem = GetComponent<LockOnSystem>();

            // Re-subscribe in case gesture recognizer wasn't ready during OnEnable
            SubscribeToGestures();
        }

        private void OnDestroy()
        {
            UnsubscribeFromGestures();
        }

        /// <summary>
        /// Attempts to execute a weapon attack in the given direction.
        /// </summary>
        /// <param name="direction">The weapon swing direction.</param>
        /// <returns>True if the attack was executed.</returns>
        public bool TryAttack(MouseDirections direction)
        {
            if (Time.time - _lastAttackTime < _attackCooldown)
                return false;

            if (GameManager.Instance == null || GameManager.Instance.WeaponManager == null)
                return false;

            var weaponManager = GameManager.Instance.WeaponManager;
            var screenWeapon = weaponManager.ScreenWeapon;

            if (screenWeapon == null)
                return false;

            // Check if weapon is ready (not mid-swing)
            if (screenWeapon.IsAttacking())
                return false;

            // Execute the attack
            screenWeapon.OnAttackDirection(direction);
            _lastAttackTime = Time.time;

            Debug.Log($"{LogPrefix} Attack: {direction}");
            return true;
        }

        private void SubscribeToGestures()
        {
            FindGestureRecognizer();
            if (_gestureRecognizer == null)
                return;

            _gestureRecognizer.OnSwipe += HandleSwipe;
            _gestureRecognizer.OnDoubleTap += HandleDoubleTap;
            _gestureRecognizer.OnTap += HandleTap;
        }

        private void UnsubscribeFromGestures()
        {
            if (_gestureRecognizer == null)
                return;

            _gestureRecognizer.OnSwipe -= HandleSwipe;
            _gestureRecognizer.OnDoubleTap -= HandleDoubleTap;
            _gestureRecognizer.OnTap -= HandleTap;
        }

        private void FindGestureRecognizer()
        {
            if (_gestureRecognizer != null)
                return;

            // Look for gesture recognizer in the input system hierarchy
            var touchZoneManager = FindObjectOfType<Input.TouchZoneManager>();
            if (touchZoneManager != null)
            {
                _gestureRecognizer = touchZoneManager.GestureRecognizer;
            }
        }

        private void HandleSwipe(Vector2 direction, float speed)
        {
            if (_directionMapper == null)
                return;

            MouseDirections weaponDir = _directionMapper.MapSwipeToWeaponDirection(direction);

            if (weaponDir == MouseDirections.None)
                return;

            // During lock-on, attacks automatically target the locked enemy
            if (_lockOnSystem != null && _lockOnSystem.IsLockedOn)
            {
                // Lock-on ensures aim is on target — just swing
                TryAttack(weaponDir);
            }
            else
            {
                TryAttack(weaponDir);
            }
        }

        private void HandleDoubleTap(Vector2 position)
        {
            // Double-tap = quick attack with default swing direction
            TryAttack(MouseDirections.Down);
        }

        private void HandleTap(Vector2 position)
        {
            // Tap in look zone = interact / activate
            if (GameManager.Instance == null)
                return;

            // Check if tapped on an enemy for lock-on
            if (_lockOnSystem != null)
            {
                if (_lockOnSystem.IsLockedOn)
                {
                    // Tap elsewhere to release lock
                    _lockOnSystem.Release();
                }
                else
                {
                    // Try to lock on to tapped target
                    if (!_lockOnSystem.TryLockOn(position))
                    {
                        // No enemy tapped — activate/interact
                        ActivateTarget();
                    }
                }
            }
            else
            {
                ActivateTarget();
            }
        }

        private void ActivateTarget()
        {
            // Trigger DFU's activate/interact action
            if (GameManager.Instance != null && GameManager.Instance.PlayerActivate != null)
            {
                // REQUIRES DFU PATCH: PlayerActivate may need a public method to
                // trigger activation programmatically. Alternatively, we simulate
                // the input that DFU checks for activation.
                GameManager.Instance.PlayerActivate.FireRayFromScreenPoint(
                    new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)
                );
            }
        }
    }
}
