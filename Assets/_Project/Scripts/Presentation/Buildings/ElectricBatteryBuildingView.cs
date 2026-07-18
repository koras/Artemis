using Spine;
using Spine.Unity;
using UnityEngine;

namespace _Project.Scripts.Presentation.Buildings
{
    /// <summary>
    /// Runtime-представление построенной электрической батареи.
    /// </summary>
    public sealed class ElectricBatteryBuildingView : BuildingViewBase
    {
        [Header("Animation")]
        // Spine-анимация состояния батареи. Живёт отдельным дочерним объектом внутри Visual.
        [SerializeField] private SkeletonAnimation _batterySkeletonAnimation;
        [SerializeField] private string _chargingAnimationName = "energyblock_charges";
        [SerializeField] private string _dischargingAnimationName = "energyblock_discharges";
        [SerializeField] private string _offAnimationName = "energyblock_off";

        private string _currentAnimationName;

        public void SetBatteryAnimationState(bool isConnectedToNetwork, bool isDepleted)
        {
            if (_batterySkeletonAnimation == null)
            {
                return;
            }

            string targetAnimationName = ResolveTargetAnimationName(isConnectedToNetwork, isDepleted);
            if (string.IsNullOrWhiteSpace(targetAnimationName))
            {
                return;
            }

            if (!_batterySkeletonAnimation.gameObject.activeSelf)
            {
                _batterySkeletonAnimation.gameObject.SetActive(true);
            }

            if (_currentAnimationName == targetAnimationName && _batterySkeletonAnimation.IsValid)
            {
                return;
            }

            _batterySkeletonAnimation.timeScale = 1f;
            _batterySkeletonAnimation.AnimationState.SetAnimation(0, targetAnimationName, true);
            _currentAnimationName = targetAnimationName;
        }

        private string ResolveTargetAnimationName(bool isConnectedToNetwork, bool isDepleted)
        {
            string animationName = isDepleted
                ? _offAnimationName
                : (isConnectedToNetwork ? _chargingAnimationName : _dischargingAnimationName);

            if (string.IsNullOrWhiteSpace(animationName))
            {
                return null;
            }

            SkeletonData skeletonData = _batterySkeletonAnimation.SkeletonDataAsset?.GetSkeletonData(false);
            if (skeletonData == null)
            {
                return null;
            }

            return skeletonData.FindAnimation(animationName) != null ? animationName : null;
        }
    }
}
