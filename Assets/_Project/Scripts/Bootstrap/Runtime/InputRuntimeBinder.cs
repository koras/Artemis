using _Project.Scripts.Input;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Presentation.UI;
using _Project.Scripts.Systems.Grid;
using UnityEngine;

namespace _Project.Scripts.Bootstrap.Runtime
{
    /// <summary>
    /// Owns input/tool event subscriptions and unsubscriptions.
    /// </summary>
    internal static class InputRuntimeBinder
    {
        public static void Bind(
            GameInputRuntimeCoordinator inputCoordinator,
            ToolInputInteractionService toolInputInteractionService,
            ConstructionToolPanelController constructionToolPanelController,
            GridHoverHighlightService gridHoverHighlightService,
            PowerBuildingOverlayService powerBuildingOverlayService,
            BuildModeVisualTintService buildModeVisualTintService,
            System.Action<Vector2Int> onPowerDebugCellHovered,
            System.Action onPowerDebugCellHoverExited)
        {
            if (inputCoordinator == null || toolInputInteractionService == null || constructionToolPanelController == null)
            {
                return;
            }

            inputCoordinator.CellClicked += toolInputInteractionService.HandleCellClicked;
            inputCoordinator.DragRectangleChanged += toolInputInteractionService.HandleDragRectangleChanged;
            inputCoordinator.RightClickPressed += toolInputInteractionService.HandleRightClickPressed;
            inputCoordinator.LeftDragFinished += toolInputInteractionService.HandleLeftDragFinished;
            inputCoordinator.CellHovered += toolInputInteractionService.HandleCellHovered;
            inputCoordinator.CellHoverExited += toolInputInteractionService.HandleCellHoverExited;
            inputCoordinator.CellHovered += onPowerDebugCellHovered;
            inputCoordinator.CellHoverExited += onPowerDebugCellHoverExited;
            constructionToolPanelController.ToolSelectionChanged += toolInputInteractionService.HandleToolSelectionChanged;
            toolInputInteractionService.ToolModeChanged += powerBuildingOverlayService.HandleToolModeChanged;
            if (buildModeVisualTintService != null)
            {
                toolInputInteractionService.ToolModeChanged += buildModeVisualTintService.HandleToolModeChanged;
            }

            if (gridHoverHighlightService != null)
            {
                inputCoordinator.CellHovered += gridHoverHighlightService.HandleCellHovered;
                inputCoordinator.CellHoverExited += gridHoverHighlightService.HandleCellHoverExited;
                toolInputInteractionService.ToolModeChanged += gridHoverHighlightService.HandleToolModeChanged;
            }
        }

        public static void Unbind(
            GameInputRuntimeCoordinator inputCoordinator,
            ToolInputInteractionService toolInputInteractionService,
            ConstructionToolPanelController constructionToolPanelController,
            GridHoverHighlightService gridHoverHighlightService,
            PowerBuildingOverlayService powerBuildingOverlayService,
            BuildModeVisualTintService buildModeVisualTintService,
            System.Action<Vector2Int> onPowerDebugCellHovered,
            System.Action onPowerDebugCellHoverExited)
        {
            if (inputCoordinator != null && toolInputInteractionService != null)
            {
                inputCoordinator.CellClicked -= toolInputInteractionService.HandleCellClicked;
                inputCoordinator.DragRectangleChanged -= toolInputInteractionService.HandleDragRectangleChanged;
                inputCoordinator.RightClickPressed -= toolInputInteractionService.HandleRightClickPressed;
                inputCoordinator.LeftDragFinished -= toolInputInteractionService.HandleLeftDragFinished;
                inputCoordinator.CellHovered -= toolInputInteractionService.HandleCellHovered;
                inputCoordinator.CellHoverExited -= toolInputInteractionService.HandleCellHoverExited;
                inputCoordinator.CellHovered -= onPowerDebugCellHovered;
                inputCoordinator.CellHoverExited -= onPowerDebugCellHoverExited;

                if (gridHoverHighlightService != null)
                {
                    inputCoordinator.CellHovered -= gridHoverHighlightService.HandleCellHovered;
                    inputCoordinator.CellHoverExited -= gridHoverHighlightService.HandleCellHoverExited;
                    toolInputInteractionService.ToolModeChanged -= gridHoverHighlightService.HandleToolModeChanged;
                }
            }

            if (constructionToolPanelController != null && toolInputInteractionService != null)
            {
                constructionToolPanelController.ToolSelectionChanged -= toolInputInteractionService.HandleToolSelectionChanged;
                if (powerBuildingOverlayService != null)
                {
                    toolInputInteractionService.ToolModeChanged -= powerBuildingOverlayService.HandleToolModeChanged;
                }

                if (buildModeVisualTintService != null)
                {
                    toolInputInteractionService.ToolModeChanged -= buildModeVisualTintService.HandleToolModeChanged;
                }
            }
        }
    }
}
