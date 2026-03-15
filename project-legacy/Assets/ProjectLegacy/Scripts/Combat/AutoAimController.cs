using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using ProjectLegacy.Core;

namespace ProjectLegacy.Combat
{
    /// <summary>
    /// Subtle aim assistance that gently nudges the player's camera toward
    /// the nearest enemy within a configurable cone. Uses a soft-aim approach
    /// — it guides, never snaps. Disabled when lock-on is active.
    /// </summary>
    public class AutoAimController : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Auto-Aim Settings")]
        [SerializeField]
        [Tooltip("Enable auto-aim assistance.")]
        private bool _enabled = true;

        [SerializeField]
        [Range(5f, 30f)]
        [Tooltip("Half-angle of the aim-assist cone in degrees.")]
        private float _coneHalfAngle = 15f;

        [SerializeField]
        [Range(0.01f, 0.25f)]
        [Tooltip("Strength of the aim nudge. Lower = more subtle.")]
        private float _aimStrength = 0.1f;

        [SerializeField]
        [Range(5f, 30f)]
        [Tooltip("Maximum detection range in meters.")]
        private float _maxRange = 20f;

        [SerializeField]
        [Range(10f, 50f)]
        [Tooltip("Cone half-angle when lock-on is active.")]
        private float _lockOnConeHalfAngle = 30f;

        [SerializeField]
        [Range(0.1f, 0.5f)]
        [Tooltip("Aim strength when lock-on is active. Higher than normal for stronger tracking.")]
        private float _lockOnAimStrength = 0.3f;

        /// <summary>Whether auto-aim is currently enabled.</summary>
        public bool IsEnabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>The current auto-aim target, if any.</summary>
        public DaggerfallEntityBehaviour CurrentTarget { get; private set; }

        /// <summary>The lock-on system reference.</summary>
        public LockOnSystem LockOnSystem { get; set; }

        private Camera _mainCamera;

        private void Start()
        {
            LoadSettings();

            // Find lock-on system sibling
            if (LockOnSystem == null)
            {
                LockOnSystem = GetComponent<LockOnSystem>();
            }
        }

        private void LateUpdate()
        {
            if (!_enabled)
            {
                CurrentTarget = null;
                return;
            }

            EnsureCamera();
            if (_mainCamera == null)
                return;

            // Don't apply soft aim if lock-on system is handling targeting
            bool isLockedOn = LockOnSystem != null && LockOnSystem.IsLockedOn;

            if (isLockedOn)
            {
                // During lock-on, use stronger aim toward lock target
                ApplyLockOnAim();
            }
            else
            {
                // Normal soft aim assist
                ApplySoftAim();
            }
        }

        /// <summary>
        /// Finds the best auto-aim target within the detection cone.
        /// </summary>
        /// <returns>The nearest valid target, or null if none found.</returns>
        public DaggerfallEntityBehaviour FindBestTarget()
        {
            EnsureCamera();
            if (_mainCamera == null)
                return null;

            float currentConeAngle = (LockOnSystem != null && LockOnSystem.IsLockedOn)
                ? _lockOnConeHalfAngle
                : _coneHalfAngle;

            DaggerfallEntityBehaviour bestTarget = null;
            float bestAngle = currentConeAngle;

            // Find all enemies in range
            var enemies = FindObjectsOfType<DaggerfallEntityBehaviour>();
            Vector3 cameraForward = _mainCamera.transform.forward;
            Vector3 cameraPosition = _mainCamera.transform.position;

            foreach (var entity in enemies)
            {
                // Skip non-enemy entities
                if (entity.EntityType != EntityTypes.EnemyMonster &&
                    entity.EntityType != EntityTypes.EnemyClass)
                    continue;

                // Skip dead enemies
                if (entity.Entity != null && entity.Entity.CurrentHealth <= 0)
                    continue;

                Vector3 toTarget = entity.transform.position - cameraPosition;
                float distance = toTarget.magnitude;

                // Skip if out of range
                if (distance > _maxRange)
                    continue;

                // Check angle
                float angle = Vector3.Angle(cameraForward, toTarget);
                if (angle < bestAngle)
                {
                    // Verify line of sight
                    if (!Physics.Linecast(cameraPosition, entity.transform.position, out RaycastHit _, LayerMask.GetMask("Default")))
                    {
                        bestAngle = angle;
                        bestTarget = entity;
                    }
                }
            }

            CurrentTarget = bestTarget;
            return bestTarget;
        }

        private void ApplySoftAim()
        {
            var target = FindBestTarget();
            if (target == null)
                return;

            NudgeCameraToward(target.transform.position, _aimStrength);
        }

        private void ApplyLockOnAim()
        {
            if (LockOnSystem == null || LockOnSystem.CurrentTarget == null)
                return;

            NudgeCameraToward(LockOnSystem.CurrentTarget.transform.position, _lockOnAimStrength);
        }

        private void NudgeCameraToward(Vector3 targetPosition, float strength)
        {
            if (GameManager.Instance == null)
                return;

            var mouseLook = GameManager.Instance.PlayerMouseLook;
            if (mouseLook == null)
                return;

            Vector3 dirToTarget = (targetPosition - _mainCamera.transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(dirToTarget);
            Quaternion currentRotation = _mainCamera.transform.rotation;

            // Subtle slerp toward target
            Quaternion nudged = Quaternion.Slerp(currentRotation, targetRotation, strength * Time.deltaTime);

            // Extract euler angles and apply to DFU's mouse look
            Vector3 nudgedEuler = nudged.eulerAngles;
            Vector3 currentEuler = currentRotation.eulerAngles;

            float yawDelta = Mathf.DeltaAngle(currentEuler.y, nudgedEuler.y);
            float pitchDelta = Mathf.DeltaAngle(currentEuler.x, nudgedEuler.x);

            mouseLook.Yaw += yawDelta;
            mouseLook.Pitch -= pitchDelta;
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

        private void LoadSettings()
        {
            if (LegacyBootstrapper.Instance == null || LegacyBootstrapper.Instance.Settings == null)
                return;

            var settings = LegacyBootstrapper.Instance.Settings;
            _enabled = settings.AutoAimEnabled;
            _aimStrength = settings.AutoAimStrength;
        }
    }
}
