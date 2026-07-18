using _Project.Scripts.Input;
using _Project.Scripts.Presentation.Grid;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Simulation;
using UnityEngine;

namespace _Project.Scripts.Bootstrap.Runtime
{
    /// <summary>
    /// Composes world-level services and visual layers.
    /// </summary>
    internal static class WorldRuntimeComposer
    {
        public static WorldRuntimeContext Compose(
            int width,
            int height,
            int cellSize,
            Vector2 gridOrigin,
            float cameraMoveSpeed,
            Transform cameraMovementTarget,
            GridTilemapRenderSettings gridTilemapRenderSettings,
            SpriteRenderer fogOverlayRenderer,
            Material fogOverlayMaterial,
            float fogHalfDarkDistanceCells,
            float fogFullDarkDistanceCells)
        {
            var context = new WorldRuntimeContext();

            context.GridSystem = new GridSystem();
            context.SimulationSystem = new SimulationSystem();
            context.GameTimeService = new GameTimeService();
            context.GridState = context.GridSystem.Create(width, height, cellSize);
            context.GridTileVisualService = new GridTileVisualService(gridTilemapRenderSettings);
            context.GridTileVisualService.RenderFull(context.GridState);

            context.FogMaskService = new FogMaskService();
            context.FogMaskService.Initialize(context.GridState, fogHalfDarkDistanceCells, fogFullDarkDistanceCells);
            context.FogOverlayRenderer = new FogOverlayRenderer(
                context.GridState,
                gridOrigin,
                cellSize,
                fogOverlayRenderer,
                fogOverlayMaterial);
            context.FogOverlayRenderer.ApplyFull(context.FogMaskService);

            context.MaterialTransitionOverlayService = new MaterialTransitionOverlayService(context.GridState, context.GridTileVisualService);
            context.MaterialTransitionOverlayService.RefreshAll();
            context.GridCoordinateConverter = new GridCoordinateConverter(gridOrigin, cellSize);
            context.GameInputRuntimeCoordinator = new GameInputRuntimeCoordinator(
                Camera.main,
                cameraMoveSpeed,
                cameraMovementTarget,
                gridOrigin,
                context.GridState,
                context.GridCoordinateConverter);

            return context;
        }
    }
}
