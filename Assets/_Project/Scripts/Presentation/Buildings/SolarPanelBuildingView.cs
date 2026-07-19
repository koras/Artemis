using Spine;
using Spine.Unity;
using UnityEngine;

namespace _Project.Scripts.Presentation.Buildings
{
    /// <summary>
    /// Runtime-представление построенной солнечной панели.
    /// </summary>
    public sealed class SolarPanelBuildingView : BuildingViewBase
    {
        [Header("Animation")]
        // Spine-анимация блика на панели. Живёт отдельным дочерним объектом внутри Visual.
        [SerializeField] private SkeletonAnimation _daylightSkeletonAnimation;
        [SerializeField] private string _dayAnimationName = "solar_loop";

        private bool _isDayAnimationActive;

        public override void SetLightPhaseState(bool isDay)
        {
            if (_daylightSkeletonAnimation == null)
            {
                return;
            }

            if (isDay)
            {
                EnableDayAnimation();
                return;
            }

            DisableDayAnimation();
        }

        private void EnableDayAnimation()
        {
            if (_isDayAnimationActive)
            {
                return;
            }

            string animationName = ResolveAnimationName();
            if (string.IsNullOrWhiteSpace(animationName))
            {
                return;
            }

            if (!_daylightSkeletonAnimation.gameObject.activeSelf)
            {
                _daylightSkeletonAnimation.gameObject.SetActive(true);
            }

            _daylightSkeletonAnimation.timeScale = 1f;
            _daylightSkeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
            _isDayAnimationActive = true;
        }

        private void DisableDayAnimation()
        {
            if (!_isDayAnimationActive && !_daylightSkeletonAnimation.gameObject.activeSelf)
            {
                return;
            }

            if (_daylightSkeletonAnimation.IsValid)
            {
                // Очищаем текущий трек, чтобы при следующем дне анимация стартовала заново.
                _daylightSkeletonAnimation.AnimationState.ClearTracks();
            }

            _daylightSkeletonAnimation.timeScale = 0f;
            _daylightSkeletonAnimation.gameObject.SetActive(false);
            _isDayAnimationActive = false;
        }

        private string ResolveAnimationName()
        {
            if (string.IsNullOrWhiteSpace(_dayAnimationName))
            {
                return null;
            }

            SkeletonData skeletonData = _daylightSkeletonAnimation.SkeletonDataAsset?.GetSkeletonData(false);
            if (skeletonData == null)
            {
                return null;
            }

            return skeletonData.FindAnimation(_dayAnimationName) != null ? _dayAnimationName : null;
        }
    }
}