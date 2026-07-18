using Spine;
using Spine.Unity;
using UnityEngine;

namespace _Project.Scripts.Presentation.Buildings
{
    /// <summary>
    /// Runtime view for a placed storage building.
    /// </summary>
    public sealed class StorageBuildingView : BuildingViewBase
    {
        [Header("Animation")]
        // Spine animation is hosted by a dedicated child object under Visual.
        [SerializeField] private SkeletonAnimation _storageSkeletonAnimation;
        [SerializeField] private string _loopAnimationName = "storage_loop";
        [SerializeField] private string _openAnimationName = "storage_open";
        [SerializeField] private string _closeAnimationName = "storage_close";
        [SerializeField] private float _loopAnimationSpeed = 1f;
        [SerializeField] private float _interactionAnimationSpeed = 1f;
        [SerializeField] private float _closeDelaySeconds = 0.15f;

        private string _currentAnimationName;

        public override void Initialize(Vector2Int anchorCell, Vector2Int size)
        {
            base.Initialize(anchorCell, size);
            PlayLoopAnimation(true);
        }

        private void OnEnable()
        {
            PlayLoopAnimation(false);
        }

        /// <summary>
        /// Plays the storage open-close interaction and returns its total wait duration in seconds.
        /// </summary>
        public float PlayDepositInteraction()
        {
            if (_storageSkeletonAnimation == null)
            {
                return 0f;
            }

            string openAnimationName = ResolveAnimationName(_openAnimationName);
            string closeAnimationName = ResolveAnimationName(_closeAnimationName);
            string loopAnimationName = ResolveAnimationName(_loopAnimationName);
            if (string.IsNullOrWhiteSpace(openAnimationName)
                || string.IsNullOrWhiteSpace(closeAnimationName)
                || string.IsNullOrWhiteSpace(loopAnimationName))
            {
                return 0f;
            }

            if (!_storageSkeletonAnimation.gameObject.activeSelf)
            {
                _storageSkeletonAnimation.gameObject.SetActive(true);
            }

            _storageSkeletonAnimation.timeScale = 1f;
            _storageSkeletonAnimation.AnimationState.ClearTracks();

            TrackEntry openEntry = _storageSkeletonAnimation.AnimationState.SetAnimation(0, openAnimationName, false);
            openEntry.TimeScale = Mathf.Max(0.01f, _interactionAnimationSpeed);

            float openDurationSeconds = GetTrackDuration(openEntry);
            float closeDelaySeconds = openDurationSeconds + Mathf.Max(0f, _closeDelaySeconds);
            TrackEntry closeEntry = _storageSkeletonAnimation.AnimationState.AddAnimation(
                0,
                closeAnimationName,
                false,
                closeDelaySeconds);
            closeEntry.TimeScale = Mathf.Max(0.01f, _interactionAnimationSpeed);

            TrackEntry loopEntry = _storageSkeletonAnimation.AnimationState.AddAnimation(0, loopAnimationName, true, 0f);
            loopEntry.TimeScale = Mathf.Max(0.01f, _loopAnimationSpeed);
            _currentAnimationName = loopAnimationName;

            return closeDelaySeconds + GetTrackDuration(closeEntry);
        }

        private void PlayLoopAnimation(bool forceRestart)
        {
            if (_storageSkeletonAnimation == null)
            {
                return;
            }

            string loopAnimationName = ResolveAnimationName(_loopAnimationName);
            if (string.IsNullOrWhiteSpace(loopAnimationName))
            {
                return;
            }

            if (!_storageSkeletonAnimation.gameObject.activeSelf)
            {
                _storageSkeletonAnimation.gameObject.SetActive(true);
            }

            if (!forceRestart && _currentAnimationName == loopAnimationName && _storageSkeletonAnimation.IsValid)
            {
                return;
            }

            _storageSkeletonAnimation.timeScale = 1f;
            TrackEntry loopEntry = _storageSkeletonAnimation.AnimationState.SetAnimation(0, loopAnimationName, true);
            loopEntry.TimeScale = Mathf.Max(0.01f, _loopAnimationSpeed);
            _currentAnimationName = loopAnimationName;
        }

        private string ResolveAnimationName(string animationName)
        {
            if (string.IsNullOrWhiteSpace(animationName))
            {
                return null;
            }

            SkeletonData skeletonData = _storageSkeletonAnimation.SkeletonDataAsset?.GetSkeletonData(false);
            if (skeletonData == null)
            {
                return null;
            }

            return skeletonData.FindAnimation(animationName) != null ? animationName : null;
        }

        private float GetTrackDuration(TrackEntry trackEntry)
        {
            if (trackEntry?.Animation == null)
            {
                return 0f;
            }

            float timeScale = Mathf.Max(0.01f, trackEntry.TimeScale);
            return trackEntry.Animation.Duration / timeScale;
        }
    }
}
