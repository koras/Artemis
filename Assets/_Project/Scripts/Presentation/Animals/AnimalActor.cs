using UnityEngine;

namespace _Project.Scripts.Presentation.Animals
{
    /// <summary>
    /// View-layer runtime actor for animals with explicit movement ticking from bootstrap.
    /// </summary>
    public sealed class AnimalActor : MonoBehaviour
    {
        private static float _globalMovementSpeedMultiplier = 1f;

        [Header("Movement Visual")]
        [SerializeField] private Transform _visual;

        [Header("Facing")]
        [SerializeField] private bool _faceByLocalScale = true;
        [SerializeField] private bool _rightIsPositiveX = true;

        [Header("Runtime State")]
        [SerializeField] [Min(0f)] private float _hunger;
        [SerializeField] [Range(0f, 1f)] private float _growthProgress;
        [SerializeField] [Min(0f)] private float _ageGameHours;
        [SerializeField] [Range(0, 100)] private int _loyalty;
        [SerializeField] [Min(0f)] private float _currentSpeed;

        private Vector3 _targetWorldPosition;
        private Vector3 _queuedWorldPosition;
        private bool _hasTargetWorldPosition;
        private bool _hasQueuedWorldPosition;
        private Vector3 _baseLocalScale;
        private bool _hasBaseLocalScale;
        private int _currentFacingSign = 1;
        private float _movementSpeedMultiplier = 1f;
        private float _maxMoveSpeed = 2.2f;
        private float _moveAcceleration = 6f;
        private float _stopDistance = 0.03f;
        private SpriteRenderer _selectionRenderer;

        public float Hunger => _hunger;
        public float GrowthProgress => _growthProgress;
        public float AgeGameHours => _ageGameHours;
        public int Loyalty => _loyalty;
        public float CurrentSpeed => _currentSpeed;
        public SpriteRenderer SelectionRenderer => _selectionRenderer;

        private void Awake()
        {
            EnsureBaseLocalScaleInitialized();
            _selectionRenderer = _visual != null ? _visual.GetComponent<SpriteRenderer>() : null;
        }

        /// <summary>
        /// Moves the actor toward the current target so simulation steps can stay grid-based.
        /// </summary>
        public void TickMovementFrame(float deltaTime)
        {
            if (!_hasTargetWorldPosition)
            {
                _currentSpeed = 0f;
                return;
            }

            Vector3 currentPosition = transform.position;
            Vector3 toTarget = _targetWorldPosition - currentPosition;
            float distanceToTarget = toTarget.magnitude;
            if (distanceToTarget <= _stopDistance)
            {
                transform.position = _targetWorldPosition;
                if (_hasQueuedWorldPosition)
                {
                    _targetWorldPosition = _queuedWorldPosition;
                    _hasQueuedWorldPosition = false;
                    return;
                }

                _currentSpeed = 0f;
                return;
            }

            // Keep the authored movement speed until the actor reaches the target cell.
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _maxMoveSpeed, _moveAcceleration * deltaTime);

            float appliedSpeed = _currentSpeed * _movementSpeedMultiplier * _globalMovementSpeedMultiplier;
            float moveDistance = Mathf.Min(appliedSpeed * deltaTime, distanceToTarget);
            if (moveDistance <= 0f)
            {
                return;
            }

            transform.position = currentPosition + (toTarget / distanceToTarget) * moveDistance;
        }

        public void SetHunger(float value)
        {
            _hunger = Mathf.Max(0f, value);
        }

        public void SetAgeGameHours(float value)
        {
            _ageGameHours = Mathf.Max(0f, value);
        }

        public void SetLoyalty(int value)
        {
            _loyalty = Mathf.Clamp(value, 0, 100);
        }

        public void SetMovementSpeedMultiplier(float movementSpeedMultiplier)
        {
            _movementSpeedMultiplier = Mathf.Max(0.01f, movementSpeedMultiplier);
        }

        public void ConfigureMovement(
            float maxMoveSpeed,
            float moveAcceleration,
            float stopDistance)
        {
            _maxMoveSpeed = Mathf.Max(0.01f, maxMoveSpeed);
            _moveAcceleration = Mathf.Max(0.01f, moveAcceleration);
            _stopDistance = Mathf.Max(0.001f, stopDistance);
        }

        public void SetGrowthProgress(float growthProgress, float minVisualScale, float maxVisualScale)
        {
            EnsureBaseLocalScaleInitialized();
            _growthProgress = Mathf.Clamp01(growthProgress);

            float appliedScale = Mathf.Lerp(
                Mathf.Max(0.01f, minVisualScale),
                Mathf.Max(0.01f, maxVisualScale),
                _growthProgress);

            _visual.localScale = new Vector3(
                Mathf.Abs(_baseLocalScale.x) * appliedScale * _currentFacingSign,
                _baseLocalScale.y * appliedScale,
                _baseLocalScale.z * appliedScale);
        }

        public void SnapToWorldPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            _targetWorldPosition = worldPosition;
            _hasTargetWorldPosition = true;
            _hasQueuedWorldPosition = false;
            _currentSpeed = 0f;
        }

        public void SetMoveTarget(Vector3 worldPosition)
        {
            _targetWorldPosition = worldPosition;
            _hasTargetWorldPosition = true;
            _hasQueuedWorldPosition = false;
        }

        public void SetMoveTargetViaWaypoint(Vector3 waypointWorldPosition, Vector3 finalWorldPosition)
        {
            _targetWorldPosition = waypointWorldPosition;
            _queuedWorldPosition = finalWorldPosition;
            _hasTargetWorldPosition = true;
            _hasQueuedWorldPosition = true;
        }

        public bool IsAtMoveTarget()
        {
            if (!_hasTargetWorldPosition)
            {
                return true;
            }

            return !_hasQueuedWorldPosition
                && Vector3.SqrMagnitude(transform.position - _targetWorldPosition) <= 0.0001f;
        }

        public void SetFacing(Vector2Int direction)
        {
            if (direction.x == 0)
            {
                return;
            }

            if (!_faceByLocalScale)
            {
                return;
            }

            int sign = direction.x > 0 ? 1 : -1;
            _currentFacingSign = _rightIsPositiveX ? sign : -sign;
            Vector3 scale = _visual.localScale;
            scale.x = Mathf.Abs(scale.x) * _currentFacingSign;
            _visual.localScale = scale;
        }

        public static void SetGlobalMovementSpeedMultiplier(float movementSpeedMultiplier)
        {
            _globalMovementSpeedMultiplier = Mathf.Max(0f, movementSpeedMultiplier);
        }

        private void EnsureBaseLocalScaleInitialized()
        {
            if (_hasBaseLocalScale)
            {
                return;
            }

            // Cache the authored visual scale once so growth affects only the visual child.
            if (_visual == null)
            {
                _visual = transform;
            }

            _baseLocalScale = _visual.localScale;
            _hasBaseLocalScale = true;
        }
    }
}
