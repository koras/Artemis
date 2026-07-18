using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Input;
using _Project.Scripts.Presentation.Grid;
using DG.Tweening;
using UnityEngine;

namespace _Project.Scripts.Systems.Grid
{
    /// <summary>
    /// Draws smooth hover highlight for the grid.
    /// </summary>
    public sealed class GridHoverHighlightService : IDisposable
    {
        private const float InactiveMinAlpha = 0.01f;
        private const float SingleCellHighlightAlpha = 0.275f;

        private readonly GridState _gridState;
        private readonly GridTileVisualService _gridTileVisualService;
        private readonly float _fadeInSeconds;
        private readonly float _fadeOutSeconds;
        private readonly Dictionary<Vector2Int, float> _currentAlphaByCell = new Dictionary<Vector2Int, float>();
        private readonly Dictionary<Vector2Int, float> _targetAlphaByCell = new Dictionary<Vector2Int, float>();
        private readonly Dictionary<Vector2Int, Tween> _activeTweenByCell = new Dictionary<Vector2Int, Tween>();
        private readonly HashSet<Vector2Int> _previousMaskCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _nextMaskCells = new HashSet<Vector2Int>();

        private bool _isEnabledForCurrentTool;
        private bool _isBaseGridInitialized;

        public GridHoverHighlightService(
            GridState gridState,
            GridTileVisualService gridTileVisualService,
            float fadeInSeconds,
            float fadeOutSeconds)
        {
            _gridState = gridState;
            _gridTileVisualService = gridTileVisualService;
            _fadeInSeconds = Mathf.Max(0.01f, fadeInSeconds);
            _fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);
        }

        public void HandleToolModeChanged(ToolMode mode)
        {
            // Подсветка hover активна только в режимах строительства объектов и кабеля.
            _isEnabledForCurrentTool = IsHoverHighlightConstructionMode(mode);
            if (_isEnabledForCurrentTool)
            {
                EnsureBaseGridVisible();
                return;
            }

            FadeOutAll();
            HideBaseGrid();
        }

        // Явно ограничиваем режимы, где нужна hover-подсветка tilemap.
        private static bool IsHoverHighlightConstructionMode(ToolMode mode)
        {
            return mode == ToolMode.BuildLadder
                   || mode == ToolMode.BuildStorage
                   || mode == ToolMode.BuildSolarPanel
                   || mode == ToolMode.BuildRegolithProcessingUnit
                   || mode == ToolMode.BuildSleepModule
                   || mode == ToolMode.BuildBattery
                   || mode == ToolMode.BuildDinner
                   || mode == ToolMode.BuildOxygenStorage
                   || mode == ToolMode.BuildOxigenProcessingUnit
                   || mode == ToolMode.BuildWaterReclamation
                   || mode == ToolMode.BuildWaterProcessingUnit
                   || mode == ToolMode.BuildBridge
                   || mode == ToolMode.BuildCable
                   || mode == ToolMode.BuildLifeModule
                   || mode == ToolMode.CancelLifeModulePlan
                   || mode == ToolMode.BuildWater
                   || mode == ToolMode.CancelWaterPlan
                   || mode == ToolMode.BuildOxygen
                   || mode == ToolMode.CancelOxygenPlan;
        }

        public void HandleCellHovered(Vector2Int cell)
        {
            if (!_isEnabledForCurrentTool)
            {
                return;
            }

            EnsureBaseGridVisible();
            BuildMask(cell);
            ApplyMaskDelta();
        }

        public void HandleCellHoverExited()
        {
            FadeOutAll();
        }

        public void Dispose()
        {
            foreach (KeyValuePair<Vector2Int, Tween> pair in _activeTweenByCell)
            {
                pair.Value?.Kill();
            }

            _activeTweenByCell.Clear();
            _currentAlphaByCell.Clear();
            _targetAlphaByCell.Clear();
            _previousMaskCells.Clear();
            _nextMaskCells.Clear();
            _isBaseGridInitialized = false;
        }

        private void EnsureBaseGridVisible()
        {
            if (_isBaseGridInitialized)
            {
                return;
            }

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    _gridTileVisualService.SetHoverHighlightDefault(new Vector2Int(x, y), InactiveMinAlpha);
                }
            }

            _isBaseGridInitialized = true;
        }

        // Полностью убираем hover-слой вне режимов строительства.
        private void HideBaseGrid()
        {
            if (!_isBaseGridInitialized)
            {
                return;
            }

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    _gridTileVisualService.ClearHoverHighlight(new Vector2Int(x, y));
                }
            }

            _isBaseGridInitialized = false;
        }

        private void BuildMask(Vector2Int centerCell)
        {
            _nextMaskCells.Clear();
            _targetAlphaByCell.Clear();

            if (!_gridState.IsInside(centerCell.x, centerCell.y))
            {
                return;
            }

            _nextMaskCells.Add(centerCell);
            _targetAlphaByCell[centerCell] = SingleCellHighlightAlpha;
        }

        private void ApplyMaskDelta()
        {
            foreach (Vector2Int oldCell in _previousMaskCells)
            {
                if (_nextMaskCells.Contains(oldCell))
                {
                    continue;
                }

                StartFade(oldCell, InactiveMinAlpha, _fadeOutSeconds, Ease.InSine);
            }

            foreach (Vector2Int newCell in _nextMaskCells)
            {
                float targetAlpha = _targetAlphaByCell[newCell];
                if (_currentAlphaByCell.TryGetValue(newCell, out float currentAlpha))
                {
                    if (Mathf.Abs(currentAlpha - targetAlpha) < 0.01f)
                    {
                        continue;
                    }

                    StartFade(newCell, targetAlpha, _fadeInSeconds, Ease.OutSine);
                    continue;
                }

                _currentAlphaByCell[newCell] = InactiveMinAlpha;
                StartFade(newCell, targetAlpha, _fadeInSeconds, Ease.OutSine);
            }

            _previousMaskCells.Clear();
            foreach (Vector2Int nextCell in _nextMaskCells)
            {
                _previousMaskCells.Add(nextCell);
            }
        }

        private void FadeOutAll()
        {
            foreach (Vector2Int cell in _previousMaskCells)
            {
                StartFade(cell, InactiveMinAlpha, _fadeOutSeconds, Ease.InSine);
            }

            _previousMaskCells.Clear();
            _nextMaskCells.Clear();
            _targetAlphaByCell.Clear();
        }

        private void StartFade(Vector2Int cell, float targetAlpha, float durationSeconds, Ease ease)
        {
            if (_activeTweenByCell.TryGetValue(cell, out Tween activeTween))
            {
                activeTween?.Kill();
            }

            if (!_currentAlphaByCell.TryGetValue(cell, out float startAlpha))
            {
                startAlpha = InactiveMinAlpha;
                _currentAlphaByCell[cell] = InactiveMinAlpha;
            }

            float alphaValue = startAlpha;
            Tween tween = DOTween.To(
                    () => alphaValue,
                    value =>
                    {
                        alphaValue = value;
                        _currentAlphaByCell[cell] = value;
                        _gridTileVisualService.SetHoverHighlight(cell, value);
                    },
                    targetAlpha,
                    durationSeconds)
                .SetEase(ease)
                .OnComplete(() =>
                {
                    _activeTweenByCell.Remove(cell);
                    _currentAlphaByCell[cell] = targetAlpha;

                    if (targetAlpha > InactiveMinAlpha + 0.0001f)
                    {
                        return;
                    }

                    _gridTileVisualService.SetHoverHighlightDefault(cell, InactiveMinAlpha);
                    _currentAlphaByCell.Remove(cell);
                    _targetAlphaByCell.Remove(cell);
                });

            _activeTweenByCell[cell] = tween;
        }
    }
}
