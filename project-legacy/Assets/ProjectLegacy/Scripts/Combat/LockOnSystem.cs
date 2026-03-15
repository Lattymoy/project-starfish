using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using ProjectLegacy.Core;

namespace ProjectLegacy.Combat
{
    /// <summary>
    /// Target locking system that allows the player to focus on a specific enemy.
    /// When locked on, the camera gently tracks the target and attacks are
    /// directed toward it. Activated by tapping an enemy.
    /// </summary>
    public class LockOnSystem : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Lock-On Settings")]
        [SerializeField]
        [Range(5f, 30f)]
        [Tooltip("Maximum range in meters for locking on to a target.")]
        private float _maxLockRange = 20f;

        [SerializeField]
        [Range(10f, 25f)]
        [Tooltip("Distance in meters at which lock-on automatically breaks.")]
        private float _breakDistance = 15f;

        [SerializeField]
        [Range(1f, 5f)]
        [Tooltip("Radius of the sphere cast used to find lock-on targets.")]
        private float _targetDetectionRadius = 2f;

        [Header("Camera Tracking")]
        [SerializeField]
        [Range(1f, 10f)]
        [Tooltip("Speed at which the camera tracks the locked target.")]
        private float _trackingSpeed = 5f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How much player look input offsets from the lock-on tracking. 0 = pure tracking, 1 = full player control.")]
        private float _playerLookInfluence = 0.3f;

        [Header("Indicator")]
        [SerializeField]
        [Tooltip("Color of the lock-on reticle.")]
        private Color _reticleColor = new Color(1f, 0.3f, 0.3f, 0.8f);

        [SerializeField]
        [Range(20f, 60f)]
        [Tooltip("Size of the lock-on reticle in pixels.")]
        private float _reticleSize = 40f;

        /// <summary>The currently locked-on target entity.</summary>
        public DaggerfallEntityBehaviour CurrentTarget { get; private set; }

        /// <summary>Whether a target is currently locked on.</summary>
        public bool IsLockedOn => CurrentTarget != null;

        private Camera _mainCamera;
        private Texture2D _reticleTexture;
        private Vector2 _reticleScreenPos;

        private void Start()
        {
            LoadSettings();
            CreateReticleTexture();
        }

        private void LateUpdate()
        {
            if (!IsLockedOn)
                return;

            // Check if lock should break
            if (ShouldBreakLock())
            {
                Release();
                return;
            }

            // Track the target with the camera
            UpdateCameraTracking();

            // Update reticle position
            UpdateReticlePosition();
        }

        private void OnDestroy()
        {
            if (_reticleTexture != null)
                Destroy(_reticleTexture);
        }

        /// <summary>
        /// Attempts to lock on to a target at the given screen position.
        /// Uses a sphere cast from the camera through the touch point.
        /// </summary>
        /// <param name="screenPosition">Screen position of the tap.</param>
        /// <returns>True if a target was found and locked on.</returns>
        public bool TryLockOn(Vector2 screenPosition)
        {
            EnsureCamera();
            if (_mainCamera == null)
                return false;

            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);

            if (Physics.SphereCast(ray, _targetDetectionRadius, out RaycastHit hit, _maxLockRange))
            {
                var entity = hit.collider.GetComponent<DaggerfallEntityBehaviour>();
                if (entity == null)
                    entity = hit.collider.GetComponentInParent<DaggerfallEntityBehaviour>();

                if (entity != null && IsValidTarget(entity))
                {
                    LockOn(entity);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Locks on to a specific target entity.
        /// </summary>
        /// <param name="target">The entity to lock on to.</param>
        public void LockOn(DaggerfallEntityBehaviour target)
        {
            if (target == null || !IsValidTarget(target))
                return;

            CurrentTarget = target;
            Debug.Log($"{LogPrefix} Locked on to target: {target.name}");

            // Trigger haptic feedback
            Util.HapticFeedback.LightTap();
        }

        /// <summary>
        /// Releases the current lock-on target.
        /// </summary>
        public void Release()
        {
            if (CurrentTarget == null)
                return;

            Debug.Log($"{LogPrefix} Lock-on released");
            CurrentTarget = null;
        }

        /// <summary>
        /// Toggles lock-on: if locked on, releases; if not, attempts to
        /// lock on to the nearest visible enemy.
        /// </summary>
        public void ToggleLock()
        {
            if (IsLockedOn)
            {
                Release();
            }
            else
            {
                TryLockOnNearest();
            }
        }

        /// <summary>
        /// Attempts to lock on to the nearest visible enemy in front of the camera.
        /// </summary>
        /// <returns>True if a target was found.</returns>
        public bool TryLockOnNearest()
        {
            EnsureCamera();
            if (_mainCamera == null)
                return false;

            DaggerfallEntityBehaviour bestTarget = null;
            float bestDistance = _maxLockRange;

            var enemies = FindObjectsOfType<DaggerfallEntityBehaviour>();
            Vector3 cameraPos = _mainCamera.transform.position;
            Vector3 cameraFwd = _mainCamera.transform.forward;

            foreach (var entity in enemies)
            {
                if (!IsValidTarget(entity))
                    continue;

                Vector3 toTarget = entity.transform.position - cameraPos;
                float distance = toTarget.magnitude;

                if (distance > _maxLockRange)
                    continue;

                // Must be roughly in front of the camera
                float angle = Vector3.Angle(cameraFwd, toTarget);
                if (angle > 45f)
                    continue;

                // Must have line of sight
                if (Physics.Linecast(cameraPos, entity.transform.position, out RaycastHit _, LayerMask.GetMask("Default")))
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = entity;
                }
            }

            if (bestTarget != null)
            {
                LockOn(bestTarget);
                return true;
            }

            return false;
        }

        private bool IsValidTarget(DaggerfallEntityBehaviour entity)
        {
            if (entity == null)
                return false;

            // Must be an enemy
            if (entity.EntityType != EntityTypes.EnemyMonster &&
                entity.EntityType != EntityTypes.EnemyClass)
                return false;

            // Must be alive
            if (entity.Entity != null && entity.Entity.CurrentHealth <= 0)
                return false;

            return true;
        }

        private bool ShouldBreakLock()
        {
            if (CurrentTarget == null)
                return true;

            // Target died
            if (CurrentTarget.Entity != null && CurrentTarget.Entity.CurrentHealth <= 0)
                return true;

            // Target too far
            EnsureCamera();
            if (_mainCamera != null)
            {
                float distance = Vector3.Distance(
                    _mainCamera.transform.position,
                    CurrentTarget.transform.position
                );
                if (distance > _breakDistance)
                    return true;
            }

            // Target GameObject destroyed
            if (CurrentTarget.gameObject == null || !CurrentTarget.gameObject.activeInHierarchy)
                return true;

            return false;
        }

        private void UpdateCameraTracking()
        {
            if (GameManager.Instance == null || CurrentTarget == null)
                return;

            var mouseLook = GameManager.Instance.PlayerMouseLook;
            if (mouseLook == null)
                return;

            EnsureCamera();
            if (_mainCamera == null)
                return;

            // Calculate direction to target
            Vector3 dirToTarget = (CurrentTarget.transform.position - _mainCamera.transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(dirToTarget);
            Quaternion currentRotation = _mainCamera.transform.rotation;

            // Blend between camera tracking and player input
            float trackingInfluence = 1f - _playerLookInfluence;
            Quaternion tracked = Quaternion.Slerp(
                currentRotation,
                targetRotation,
                trackingInfluence * _trackingSpeed * Time.deltaTime
            );

            // Apply to DFU's mouse look
            Vector3 trackedEuler = tracked.eulerAngles;
            Vector3 currentEuler = currentRotation.eulerAngles;

            float yawDelta = Mathf.DeltaAngle(currentEuler.y, trackedEuler.y);
            float pitchDelta = Mathf.DeltaAngle(currentEuler.x, trackedEuler.x);

            mouseLook.Yaw += yawDelta;
            mouseLook.Pitch -= pitchDelta;
        }

        private void UpdateReticlePosition()
        {
            EnsureCamera();
            if (_mainCamera == null || CurrentTarget == null)
                return;

            // Project target position to screen space
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(CurrentTarget.transform.position);

            // Only show reticle if target is in front of camera
            if (screenPos.z > 0)
            {
                _reticleScreenPos = new Vector2(screenPos.x, screenPos.y);
            }
        }

        private void EnsureCamera()
        {
            if (_mainCamera == null)
            {
                _mainCamera = GameManager.Instance != null
                    ? GameManager.Instance.MainCamera
                    : Camera.main;
            }
        }

        private void CreateReticleTexture()
        {
            int size = 64;
            _reticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float outerRadius = size / 2f;
            float innerRadius = outerRadius * 0.7f;
            float lineWidth = 2f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                    // Draw circle outline
                    bool isOnRing = Mathf.Abs(dist - outerRadius * 0.85f) < lineWidth;

                    // Draw crosshair lines (short ticks at cardinal points)
                    bool isOnCross = false;
                    if (Mathf.Abs(x - center) < lineWidth && (dist > innerRadius && dist < outerRadius))
                        isOnCross = true;
                    if (Mathf.Abs(y - center) < lineWidth && (dist > innerRadius && dist < outerRadius))
                        isOnCross = true;

                    if (isOnRing || isOnCross)
                    {
                        _reticleTexture.SetPixel(x, y, _reticleColor);
                    }
                    else
                    {
                        _reticleTexture.SetPixel(x, y, Color.clear);
                    }
                }
            }

            _reticleTexture.Apply();
        }

        private void OnGUI()
        {
            if (!IsLockedOn || _reticleTexture == null)
                return;

            // Convert screen position to GUI coordinates (Y is flipped)
            float guiY = Screen.height - _reticleScreenPos.y;
            float halfSize = _reticleSize / 2f;

            Rect reticleRect = new Rect(
                _reticleScreenPos.x - halfSize,
                guiY - halfSize,
                _reticleSize,
                _reticleSize
            );

            GUI.DrawTexture(reticleRect, _reticleTexture);
        }

        private void LoadSettings()
        {
            if (LegacyBootstrapper.Instance == null || LegacyBootstrapper.Instance.Settings == null)
                return;

            _breakDistance = LegacyBootstrapper.Instance.Settings.LockOnBreakDistance;
        }
    }
}
