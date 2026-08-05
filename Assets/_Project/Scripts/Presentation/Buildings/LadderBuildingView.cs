using _Project.Scripts.Data.Construction;

namespace _Project.Scripts.Presentation.Buildings
{
    /// <summary>
    /// Runtime-представление построенной лестницы.
    /// Наследует общие данные BuildingDef/Anchor/Size из базового view.
    /// </summary>
    public sealed class LadderBuildingView : BuildingViewBase
    {
        /// <summary>
        /// Refreshes this ladder part after a neighboring ladder is built or removed.
        /// </summary>
        public void RefreshVisual(bool hasLadderBelow, bool hasLadderAbove)
        {
            // The ladder prefab is wired with LadderBuildingDef; an invalid asset type must be visible immediately.
            LadderBuildingDef ladderBuildingDef = (LadderBuildingDef)BuildingDef;
            SetVisualSprite(ladderBuildingDef.ResolveLadderSprite(hasLadderBelow, hasLadderAbove, false));
        }
    }
}
