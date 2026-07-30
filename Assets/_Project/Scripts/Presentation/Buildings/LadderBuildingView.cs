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
            SetVisualSprite(BuildingDef.ResolveLadderSprite(hasLadderBelow, hasLadderAbove, false));
        }
    }
}
