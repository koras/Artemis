using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Pathfinding;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace _Project.Scripts.Presentation.Character
{
    /// <summary>
    /// View-layer actor state for a character instance.
    /// </summary>
    public sealed class CharacterActor : MonoBehaviour
    {
        private static readonly HashSet<CharacterActor> INSTANCES = new HashSet<CharacterActor>();
        private static readonly List<string> RandomSkinCandidatesBuffer = new List<string>(16);
        private const string WORK_BEAM_START_NAME = "SphereStart";
        private const string WORK_BEAM_END_NAME = "SphereEnd";
        private const string WORK_POINT_BONE_NAME = "POINT_LASER";
        private const string WORK_WEAPON_SLOT_NAME = "pistol";
        private static readonly string[] WORK_ANIMATION_NAMES =
        {
            "laser_01",
            "laser_02",
            "laser_03",
            "laser_04",
            "laser_05"
        };

        private static float _globalMovementSpeedMultiplier = 1f;
        private static bool _isGlobalPaused;

        [Header("Animation")]
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [SerializeField] private CharacterWorkAimRig _workAimRig;
        [SerializeField] private GameObject _workBeamPrefab;
        [SerializeField] private Transform _workBeamOrigin;
        [SerializeField] private List<string> _availableSkinNames = new List<string>();
        [SerializeField] private string _currentSkinName = string.Empty;
        [SerializeField] private string _runtimeSkinOverride = string.Empty;
        [SerializeField] private CharacterAnimationState _currentAnimationState = CharacterAnimationState.Idle;
        [SerializeField] private MovementActionType _currentMovementAnimationAction = MovementActionType.Walk;
        [SerializeField] private bool _shouldUseDownAnimationForCurrentMove;

        [Header("Movement Visual")]
        [SerializeField] private float _moveLerpSpeed = 8f;
        [SerializeField] private float _movementAnimationSpeedMultiplier = 1f;
        [SerializeField] private float _currentMoveSpeed;

        // World-space movement target for smooth interpolation between simulation ticks.
        private Vector3 _targetWorldPosition;
        // Optional second phase for step-up / jump-down presentation.
        private Vector3 _queuedWorldPosition;
        private MovementActionType _queuedMovementAnimationAction;
        private bool _queuedShouldUseDownAnimationForCurrentMove;
        // Runtime instance of the beam VFX that is shown only while the unit is working.
        private GameObject _workBeamInstance;
        private Transform _workBeamStart;
        private Transform _workBeamEnd;
        private LineRenderer _workBeamLineRenderer;
        private Bone _workOriginBone;
        private string _currentWorkAnimationName = string.Empty;
        // Tracks whether the visual movement target has been initialized.
        private bool _hasTargetWorldPosition;
        private bool _hasQueuedWorldPosition;

        [Header("Facing")]
        [SerializeField] private bool _faceByLocalScale = true;
        [SerializeField] private bool _rightIsPositiveX = true;

        [Header("Needs")]
        [SerializeField] [Range(0, 300)] private int _hunger = 100;
        [SerializeField] [Range(0, 300)] private int _sleepDesire = 1;
        // Mood is a lightweight passive emotional stat for runtime and diagnostics.
        [SerializeField] [Range(0, 100)] private int _mood = 60;
        // Runtime-only food preference weights for this spawned character instance.
        private readonly Dictionary<string, int> _foodPreferenceByResourceId = new Dictionary<string, int>();
        private int _foodPreferencesVersion;
        private string _lastProcessedRuntimeSkinOverride = string.Empty;

        public int Hunger => _hunger;
        public int SleepDesire => _sleepDesire;
        public int Mood => _mood;
        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;
        public CharacterWorkAimRig WorkAimRig => _workAimRig;
        public IReadOnlyDictionary<string, int> FoodPreferences => _foodPreferenceByResourceId;
        public int FoodPreferencesVersion => _foodPreferencesVersion;
        public IReadOnlyList<string> AvailableSkinNames => _availableSkinNames;
        public string CurrentSkinName => _currentSkinName;
        public string RuntimeSkinOverride => _runtimeSkinOverride;
        public CharacterAnimationState CurrentAnimationState => _currentAnimationState;
        public MovementActionType CurrentMovementAnimationAction => _currentMovementAnimationAction;
        public bool ShouldUseDownAnimationForCurrentMove => _shouldUseDownAnimationForCurrentMove;
        public float MoveLerpSpeed => _moveLerpSpeed;
        public float CurrentMoveSpeed => _currentMoveSpeed;
        public float EffectiveMoveSpeed => _moveLerpSpeed * _globalMovementSpeedMultiplier;
        public float GlobalMovementSpeedMultiplier => _globalMovementSpeedMultiplier;
        public float MovementAnimationSpeedMultiplier => _movementAnimationSpeedMultiplier;
        public float MovementAnimationPlaybackSpeed => _globalMovementSpeedMultiplier * _movementAnimationSpeedMultiplier;

        private void Awake()
        {
            INSTANCES.Add(this);
            InitializeWorkBeam();
            InitializeSpinePresentation();
            ApplyPauseState();
        }

        /// <summary>
        /// Sets hunger within the allowed range.
        /// </summary>
        public void SetHunger(int value)
        {
            _hunger = Mathf.Clamp(value, 0, 300);
        }

        /// <summary>
        /// Smoothly advances the visual position toward the current target every frame.
        /// </summary>
        private void Update()
        {
            ApplyRuntimeSkinOverrideIfNeeded();

            if (_isGlobalPaused)
            {
                _currentMoveSpeed = 0f;
                return;
            }

            if (!_hasTargetWorldPosition)
            {
                _currentMoveSpeed = 0f;
                return;
            }

            Vector3 previousPosition = transform.position;
            float frameDeltaTime = Time.unscaledDeltaTime;
            // MoveTowards keeps interpolation stable and prevents overshoot.
            transform.position = Vector3.MoveTowards(
                previousPosition,
                _targetWorldPosition,
                _moveLerpSpeed * _globalMovementSpeedMultiplier * frameDeltaTime);

            if (_hasQueuedWorldPosition && Vector3.SqrMagnitude(transform.position - _targetWorldPosition) <= 0.0001f)
            {
                _targetWorldPosition = _queuedWorldPosition;
                _currentMovementAnimationAction = _queuedMovementAnimationAction;
                _shouldUseDownAnimationForCurrentMove = _queuedShouldUseDownAnimationForCurrentMove;
                _hasQueuedWorldPosition = false;
            }

            _currentMoveSpeed = frameDeltaTime > 0f
                ? Vector3.Distance(previousPosition, transform.position) / frameDeltaTime
                : 0f;
        }

        /// <summary>
        /// Sets sleep desire within the allowed range.
        /// </summary>
        public void SetSleepDesire(int value)
        {
            _sleepDesire = Mathf.Clamp(value, 0, 300);
        }

        /// <summary>
        /// Sets mood within the allowed range.
        /// </summary>
        public void SetMood(int value)
        {
            _mood = Mathf.Clamp(value, 0, 100);
        }

        /// <summary>
        /// Applies runtime-generated food preference scores for this character instance.
        /// </summary>
        public void SetFoodPreferences(IReadOnlyDictionary<string, int> foodPreferences)
        {
            _foodPreferenceByResourceId.Clear();
            _foodPreferencesVersion++;
            if (foodPreferences == null)
            {
                return;
            }

            foreach (KeyValuePair<string, int> pair in foodPreferences)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                _foodPreferenceByResourceId[pair.Key] = Mathf.Clamp(pair.Value, 0, 10);
            }
        }

        public int GetFoodPreferenceScore(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return 0;
            }

            return _foodPreferenceByResourceId.TryGetValue(resourceId, out int score)
                ? score
                : 0;
        }

        /// <summary>
        /// Instantly applies a world position, used for spawn and teleport cases.
        /// </summary>
        public void SnapToWorldPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            _targetWorldPosition = worldPosition;
            _hasTargetWorldPosition = true;
            _hasQueuedWorldPosition = false;
            _queuedMovementAnimationAction = MovementActionType.Walk;
            _queuedShouldUseDownAnimationForCurrentMove = false;
            _currentMoveSpeed = 0f;
        }

        /// <summary>
        /// Sets the next world-space movement target.
        /// </summary>
        public void SetMoveTarget(Vector3 worldPosition)
        {
            _targetWorldPosition = worldPosition;
            _hasTargetWorldPosition = true;
            _hasQueuedWorldPosition = false;
            _queuedMovementAnimationAction = MovementActionType.Walk;
            _queuedShouldUseDownAnimationForCurrentMove = false;
        }

        /// <summary>
        /// Uses a two-phase visual move so climb/jump edges do not render as a straight diagonal line.
        /// </summary>
        public void SetMoveTargetViaWaypoint(Vector3 waypointWorldPosition, Vector3 finalWorldPosition)
        {
            _currentMovementAnimationAction = ResolveMovementAnimationActionForSegment(transform.position, waypointWorldPosition);
            _targetWorldPosition = waypointWorldPosition;
            _queuedWorldPosition = finalWorldPosition;
            _queuedMovementAnimationAction = ResolveMovementAnimationActionForSegment(waypointWorldPosition, finalWorldPosition);
            _shouldUseDownAnimationForCurrentMove = false;
            _queuedShouldUseDownAnimationForCurrentMove = false;
            _hasTargetWorldPosition = true;
            _hasQueuedWorldPosition = true;
        }

        /// <summary>
        /// Returns true when the actor has visually reached the current movement target.
        /// </summary>
        public bool IsAtMoveTarget()
        {
            if (!_hasTargetWorldPosition) return true;
            return !_hasQueuedWorldPosition
                && Vector3.SqrMagnitude(transform.position - _targetWorldPosition) <= 0.0001f;
        }

        /// <summary>
        /// Routes work-direction updates to the prefab-owned aim rig.
        /// </summary>
        public void SetWorkPresentation(Vector2 targetWorld)
        {
            Vector2 originWorld = GetWorkOriginWorldPosition();
            Vector2 direction = targetWorld - originWorld;

            // Work aim rig remains optional for prefabs that only need the beam presentation.
            if (_workAimRig != null)
            {
                _workAimRig.SetWorkAim(direction);
            }

            PlayWorkAnimation(direction);
            UpdateWorkBeam(originWorld, targetWorld);
        }

        /// <summary>
        /// Stops work-direction aiming when the unit leaves a work action.
        /// </summary>
        public void ClearWorkPresentation()
        {
            // Work aim rig remains optional for prefabs that only need the beam presentation.
            if (_workAimRig != null)
            {
                _workAimRig.DisableWorkAim();
            }

            _currentWorkAnimationName = string.Empty;
            SetWorkBeamActive(false);
        }

        /// <summary>
        /// Stores the latest movement action so the animation layer can distinguish run/up/down clips.
        /// </summary>
        public void SetMovementAnimationAction(MovementActionType actionType)
        {
            SetMovementAnimationAction(actionType, false);
        }

        /// <summary>
        /// Stores the latest movement action and whether downward movement should use the explicit down clip.
        /// </summary>
        public void SetMovementAnimationAction(MovementActionType actionType, bool shouldUseDownAnimation)
        {
            _currentMovementAnimationAction = actionType;
            _shouldUseDownAnimationForCurrentMove = shouldUseDownAnimation;
        }

        /// <summary>
        /// Applies the requested base locomotion/need animation on the Spine track.
        /// </summary>
        public void SetAnimationState(CharacterAnimationState animationState)
        {
            InitializeSpinePresentation();

            if (_currentAnimationState == animationState && _skeletonAnimation != null && _skeletonAnimation.IsValid)
            {
                return;
            }

            _currentAnimationState = animationState;
            PlayBaseAnimation(GetAnimationName(animationState));
        }

        /// <summary>
        /// Picks one available Spine skin for this runtime actor instance.
        /// </summary>
        public void InitializeRandomSkin(System.Random random)
        {
            InitializeSpinePresentation();

            if (_availableSkinNames == null || _availableSkinNames.Count == 0)
            {
                return;
            }

            IReadOnlyList<string> randomSkinCandidates = BuildRandomSkinCandidates();
            int skinIndex = random != null
                ? random.Next(0, randomSkinCandidates.Count)
                : 0;
            string selectedSkinName = randomSkinCandidates[skinIndex];
            if (!SetSkin(selectedSkinName))
            {
                SetSkin("default");
            }
        }

        /// <summary>
        /// Applies one explicit skin by name and keeps the debug override field in sync.
        /// </summary>
        public bool SetSkin(string skinName)
        {
            InitializeSpinePresentation();

            if (_skeletonAnimation == null || !_skeletonAnimation.IsValid)
            {
                Debug.LogError($"[CharacterActor] {name} cannot apply skin '{skinName}' because Spine is not initialized.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(skinName))
            {
                Debug.LogError($"[CharacterActor] {name} rejected an empty skin name.", this);
                return false;
            }

            Skeleton skeleton = _skeletonAnimation.Skeleton;
            Skin requestedSkin = skeleton.Data.FindSkin(skinName);
            if (requestedSkin == null)
            {
                Debug.LogError($"[CharacterActor] {name} cannot find Spine skin '{skinName}'.", this);
                return false;
            }

            if (!TryBuildAppliedSkin(skeleton, requestedSkin, out Skin appliedSkin))
            {
                Debug.LogError($"[CharacterActor] {name} cannot compose Spine skin '{skinName}'.", this);
                return false;
            }

            skeleton.SetSkin(appliedSkin);
            skeleton.SetupPoseSlots();
            _skeletonAnimation.AnimationState.Apply(skeleton);

            _currentSkinName = requestedSkin.Name;
            _runtimeSkinOverride = requestedSkin.Name;
            _lastProcessedRuntimeSkinOverride = requestedSkin.Name;
            return true;
        }

        /// <summary>
        /// Rotates the actor to face the horizontal movement direction.
        /// </summary>
        public void SetFacing(Vector2Int direction)
        {
            if (direction.x == 0)
            {
                // Keep the last facing direction for vertical steps.
                return;
            }

            int sign = direction.x > 0 ? 1 : -1;
            int appliedSign = _rightIsPositiveX ? sign : -sign;

            if (_faceByLocalScale)
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * appliedSign;
                transform.localScale = scale;
            }
        }

        public static void SetGlobalMovementSpeedMultiplier(float movementSpeedMultiplier)
        {
            _globalMovementSpeedMultiplier = Mathf.Max(0f, movementSpeedMultiplier);

            foreach (CharacterActor actor in INSTANCES)
            {
                if (actor == null)
                {
                    continue;
                }

                actor.ApplyPauseState();
            }
        }

        public static void SetGlobalPauseState(bool isPaused)
        {
            _isGlobalPaused = isPaused;

            foreach (CharacterActor actor in INSTANCES)
            {
                if (actor == null)
                {
                    continue;
                }

                actor.ApplyPauseState();
            }
        }

        private void OnDestroy()
        {
            INSTANCES.Remove(this);
        }

        private void ApplyPauseState()
        {
            if (_skeletonAnimation == null)
            {
                return;
            }

            // Pause must freeze the full authored Spine playback, while speed presets scale all character clips.
            _skeletonAnimation.timeScale = _isGlobalPaused ? 0f : MovementAnimationPlaybackSpeed;
        }

        private void InitializeWorkBeam()
        {
            if (_workBeamPrefab == null)
            {
                return;
            }

            // Instantiate the authored beam prefab once and keep it disabled until work begins.
            _workBeamInstance = Instantiate(_workBeamPrefab, transform);
            _workBeamInstance.name = _workBeamPrefab.name;
            _workBeamInstance.SetActive(false);

            Transform workBeamRoot = _workBeamInstance.transform;
            _workBeamStart = workBeamRoot.Find(WORK_BEAM_START_NAME);
            _workBeamEnd = workBeamRoot.Find(WORK_BEAM_END_NAME);
            _workBeamLineRenderer = _workBeamInstance.GetComponentInChildren<LineRenderer>(true);
        }

        private void UpdateWorkBeam(Vector2 originWorld, Vector2 targetWorld)
        {
            if (_workBeamPrefab == null)
            {
                return;
            }

            if (_workBeamInstance == null)
            {
                InitializeWorkBeam();
                if (_workBeamInstance == null)
                {
                    return;
                }
            }

            Vector3 beamOriginWorldPosition = new Vector3(originWorld.x, originWorld.y, transform.position.z);
            Vector3 targetWorldPosition = new Vector3(targetWorld.x, targetWorld.y, beamOriginWorldPosition.z);

            if (_workBeamStart != null)
            {
                _workBeamStart.position = beamOriginWorldPosition;
            }

            if (_workBeamEnd != null)
            {
                _workBeamEnd.position = targetWorldPosition;
            }

            if (_workBeamLineRenderer != null)
            {
                _workBeamLineRenderer.positionCount = 2;
                _workBeamLineRenderer.SetPosition(0, beamOriginWorldPosition);
                _workBeamLineRenderer.SetPosition(1, targetWorldPosition);
            }

            SetWorkBeamActive(true);
        }

        private void SetWorkBeamActive(bool isActive)
        {
            if (_workBeamInstance == null)
            {
                return;
            }

            if (_workBeamInstance.activeSelf == isActive)
            {
                return;
            }

            _workBeamInstance.SetActive(isActive);
        }

        private void CacheWorkOriginBone()
        {
            _workOriginBone = null;
            if (_skeletonAnimation == null || !_skeletonAnimation.IsValid)
            {
                return;
            }

            Skeleton skeleton = _skeletonAnimation.Skeleton;
            _workOriginBone = skeleton.FindBone(WORK_POINT_BONE_NAME);
            if (_workOriginBone != null)
            {
                return;
            }

            Slot weaponSlot = skeleton.FindSlot(WORK_WEAPON_SLOT_NAME);
            _workOriginBone = weaponSlot?.Bone;
        }

        private Vector2 GetWorkOriginWorldPosition()
        {
            if (_skeletonAnimation != null && _skeletonAnimation.IsValid)
            {
                if (_workOriginBone == null)
                {
                    CacheWorkOriginBone();
                }

                if (_workOriginBone != null)
                {
                    BonePose workOriginPose = _workOriginBone.AppliedPose;
                    Vector3 worldPosition = _skeletonAnimation.transform.TransformPoint(
                        new Vector3(workOriginPose.WorldX, workOriginPose.WorldY, 0f));
                    return new Vector2(worldPosition.x, worldPosition.y);
                }
            }

            Transform workBeamOrigin = _workBeamOrigin != null ? _workBeamOrigin : transform;
            Vector3 fallbackWorldPosition = workBeamOrigin.position;
            return new Vector2(fallbackWorldPosition.x, fallbackWorldPosition.y);
        }

        private void PlayWorkAnimation(Vector2 direction)
        {
            InitializeSpinePresentation();

            if (_skeletonAnimation == null || !_skeletonAnimation.IsValid)
            {
                return;
            }

            string animationName = ResolveWorkAnimationName(direction);
            if (string.Equals(_currentWorkAnimationName, animationName, StringComparison.Ordinal))
            {
                return;
            }

            _currentWorkAnimationName = animationName;
            _skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
        }

        private static string ResolveWorkAnimationName(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return WORK_ANIMATION_NAMES[2];
            }

            Vector2 normalizedDirection = direction.normalized;
            float mirroredX = Mathf.Abs(normalizedDirection.x);
            float angle = Mathf.Atan2(normalizedDirection.y, mirroredX) * Mathf.Rad2Deg;

            if (angle >= 67.5f)
            {
                return WORK_ANIMATION_NAMES[0];
            }

            if (angle >= 22.5f)
            {
                return WORK_ANIMATION_NAMES[1];
            }

            if (angle > -22.5f)
            {
                return WORK_ANIMATION_NAMES[2];
            }

            if (angle > -67.5f)
            {
                return WORK_ANIMATION_NAMES[3];
            }

            return WORK_ANIMATION_NAMES[4];
        }

        private void InitializeSpinePresentation()
        {
            if (_skeletonAnimation == null)
            {
                return;
            }

            _skeletonAnimation.Initialize(false);
            CacheAvailableSkinNames();
            CacheWorkOriginBone();
            EnsureFallbackSkinApplied();

            if (string.IsNullOrWhiteSpace(_currentSkinName) && _skeletonAnimation.IsValid)
            {
                _currentSkinName = _skeletonAnimation.Skeleton.Skin?.Name ?? "default";
            }

            if (string.IsNullOrWhiteSpace(_runtimeSkinOverride))
            {
                _runtimeSkinOverride = _currentSkinName;
                _lastProcessedRuntimeSkinOverride = _currentSkinName;
            }
        }

        private void CacheAvailableSkinNames()
        {
            _availableSkinNames.Clear();

            if (_skeletonAnimation == null)
            {
                return;
            }

            SkeletonData skeletonData = _skeletonAnimation.SkeletonDataAsset != null
                ? _skeletonAnimation.SkeletonDataAsset.GetSkeletonData(false)
                : null;
            if (skeletonData == null)
            {
                return;
            }

            ExposedList<Skin> skins = skeletonData.Skins;
            for (int i = 0; i < skins.Count; i++)
            {
                Skin skin = skins.Items[i];
                if (skin == null || string.IsNullOrWhiteSpace(skin.Name))
                {
                    continue;
                }

                _availableSkinNames.Add(skin.Name);
            }

            if (_availableSkinNames.Count == 0)
            {
                _availableSkinNames.Add("default");
            }
        }

        private IReadOnlyList<string> BuildRandomSkinCandidates()
        {
            RandomSkinCandidatesBuffer.Clear();

            if (_availableSkinNames == null || _availableSkinNames.Count == 0)
            {
                RandomSkinCandidatesBuffer.Add("default");
                return RandomSkinCandidatesBuffer;
            }

            for (int i = 0; i < _availableSkinNames.Count; i++)
            {
                string skinName = _availableSkinNames[i];
                if (string.IsNullOrWhiteSpace(skinName))
                {
                    continue;
                }

                // Keep default as a fallback skin, but do not random-pick it when visual variants exist.
                if (string.Equals(skinName, "default", StringComparison.Ordinal))
                {
                    continue;
                }

                RandomSkinCandidatesBuffer.Add(skinName);
            }

            return RandomSkinCandidatesBuffer.Count > 0
                ? RandomSkinCandidatesBuffer
                : _availableSkinNames;
        }

        private void EnsureFallbackSkinApplied()
        {
            if (_skeletonAnimation == null || !_skeletonAnimation.IsValid)
            {
                return;
            }

            string activeSkinName = _skeletonAnimation.Skeleton.Skin?.Name;
            if (!string.IsNullOrWhiteSpace(activeSkinName))
            {
                return;
            }

            string fallbackSkinName = GetFallbackSkinName();
            if (string.IsNullOrWhiteSpace(fallbackSkinName))
            {
                return;
            }

            if (!SetSkin(fallbackSkinName))
            {
                Debug.LogError($"[CharacterActor] {name} failed to apply fallback skin '{fallbackSkinName}'.", this);
            }
        }

        private string GetFallbackSkinName()
        {
            if (_availableSkinNames == null || _availableSkinNames.Count == 0)
            {
                return "default";
            }

            for (int i = 0; i < _availableSkinNames.Count; i++)
            {
                if (string.Equals(_availableSkinNames[i], "default", StringComparison.Ordinal))
                {
                    return _availableSkinNames[i];
                }
            }

            return _availableSkinNames[0];
        }

        private void ApplyRuntimeSkinOverrideIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(_runtimeSkinOverride))
            {
                return;
            }

            if (string.Equals(_runtimeSkinOverride, _lastProcessedRuntimeSkinOverride, StringComparison.Ordinal))
            {
                return;
            }

            string requestedSkinName = _runtimeSkinOverride;
            _lastProcessedRuntimeSkinOverride = requestedSkinName;
            SetSkin(requestedSkinName);
        }

        private void PlayBaseAnimation(string animationName)
        {
            if (_skeletonAnimation == null || !_skeletonAnimation.IsValid || string.IsNullOrWhiteSpace(animationName))
            {
                return;
            }

            TrackEntry currentTrack = _skeletonAnimation.AnimationState.GetTrack(0);
            if (currentTrack != null && string.Equals(currentTrack.Animation?.Name, animationName, StringComparison.Ordinal))
            {
                return;
            }

            _skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
        }

        private static bool TryBuildAppliedSkin(Skeleton skeleton, Skin requestedSkin, out Skin appliedSkin)
        {
            appliedSkin = null;
            if (skeleton == null || requestedSkin == null)
            {
                return false;
            }

            if (string.Equals(requestedSkin.Name, "default", StringComparison.Ordinal))
            {
                appliedSkin = requestedSkin;
                return true;
            }

            Skin defaultSkin = skeleton.Data.FindSkin("default");
            if (defaultSkin == null)
            {
                appliedSkin = requestedSkin;
                return true;
            }

            var compositeSkin = new Skin($"runtime-{requestedSkin.Name}");
            compositeSkin.AddSkin(defaultSkin);
            compositeSkin.AddSkin(requestedSkin);
            appliedSkin = compositeSkin;
            return true;
        }

        private static string GetAnimationName(CharacterAnimationState animationState)
        {
            switch (animationState)
            {
                case CharacterAnimationState.Run:
                    return "run";
                case CharacterAnimationState.Eat:
                    return "eat";
                case CharacterAnimationState.MoveUp:
                    return "up";
                case CharacterAnimationState.MoveDown:
                    return "down";
                default:
                    return "idle";
            }
        }

        private static MovementActionType ResolveMovementAnimationActionForSegment(Vector3 fromWorldPosition, Vector3 toWorldPosition)
        {
            Vector3 delta = toWorldPosition - fromWorldPosition;
            if (delta.y > 0.001f)
            {
                return MovementActionType.JumpUp1;
            }

            if (delta.y < -0.001f)
            {
                return MovementActionType.Fall;
            }

            return MovementActionType.Walk;
        }
    }
}
