using System.Collections.Generic;
using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Grid;
using _Project.Scripts.Data.Tasks;
using _Project.Scripts.Systems.Resources;
using _Project.Scripts.Systems.Tasks;
using UnityEngine;

namespace _Project.Scripts.Systems.Construction
{
    /// <summary>
    /// Owns life-module chain rules, merge logic, and grid state updates.
    /// </summary>
    public sealed class LifeModulePlacementService
    {
        private const int PART_HEIGHT = 3;
        private const int SIDE_WIDTH = 2;
        private const int MIN_CHAIN_WIDTH = 6;

        private readonly GridState _gridState;
        private readonly GlobalTaskBoardService _globalTaskBoardService;
        private readonly BuildingPlacementService _buildingPlacementService;
        private readonly ResourceInventoryService _resourceInventoryService;
        private readonly LifeModuleConstructionConfig _constructionConfig;

        public LifeModulePlacementService(
            GridState gridState,
            GlobalTaskBoardService globalTaskBoardService,
            BuildingPlacementService buildingPlacementService,
            ResourceInventoryService resourceInventoryService,
            LifeModuleConstructionConfig constructionConfig)
        {
            _gridState = gridState;
            _globalTaskBoardService = globalTaskBoardService;
            _buildingPlacementService = buildingPlacementService;
            _resourceInventoryService = resourceInventoryService;
            _constructionConfig = constructionConfig;
        }

        public bool TryCreatePayloadFromDrag(Vector2Int dragStartCell, Vector2Int dragEndCell, out LifeModuleTaskPayload payload)
        {
            payload = null;

            int anchorY = Mathf.Min(dragStartCell.y, dragEndCell.y);
            int selectedMinX = Mathf.Min(dragStartCell.x, dragEndCell.x);
            int selectedMaxX = Mathf.Max(dragStartCell.x, dragEndCell.x);
            int selectedWidth = selectedMaxX - selectedMinX + 1;
            if (selectedWidth <= 0)
            {
                return false;
            }

            if (!TryResolveMergedBounds(selectedMinX, selectedMaxX, anchorY, out int mergedMinX, out int mergedMaxX, out int[] replacedGroupIds))
            {
                return false;
            }

            int totalWidth = mergedMaxX - mergedMinX + 1;
            bool isMinimumWidthReached = totalWidth >= MIN_CHAIN_WIDTH;
            Vector2Int previewAnchorCell = isMinimumWidthReached
                ? new Vector2Int(mergedMinX, anchorY)
                : new Vector2Int(selectedMinX, anchorY);
            int previewWidth = isMinimumWidthReached ? totalWidth : selectedWidth;

            // Under the minimal width we still build a staged preview payload,
            // so the player sees the future footprint and the invalid red state.
            List<LifeModulePartPayload> parts = isMinimumWidthReached
                ? BuildParts(previewAnchorCell, previewWidth)
                : BuildUnderMinimumPreviewParts(previewAnchorCell, previewWidth);
            Vector2Int[] occupiedCells = BuildOccupiedCells(parts);
            bool canOccupy = isMinimumWidthReached
                ? ValidateOccupiedCells(occupiedCells, replacedGroupIds, allowLifeModuleOverlapWithinReplacedGroups: true)
                : AreCellsInsideGrid(occupiedCells);
            bool addsNewFootprintCells = !isMinimumWidthReached || AddsNewFootprintCells(occupiedCells);

            if (!canOccupy && !AreCellsInsideGrid(occupiedCells))
            {
                return false;
            }

            bool isPlacementValid = isMinimumWidthReached && canOccupy && addsNewFootprintCells;

            payload = new LifeModuleTaskPayload
            {
                AnchorCell = previewAnchorCell,
                Width = previewWidth,
                Height = PART_HEIGHT,
                IsPlacementValid = isPlacementValid,
                RemainingBuildTicks = 3,
                IsBuildCostPaid = false,
                Parts = parts.ToArray(),
                OccupiedCells = occupiedCells,
                ReplacedGroupIds = replacedGroupIds
            };
            return true;
        }

        public bool TryQueueBuild(LifeModuleTaskPayload payload, int currentTick, out int taskId)
        {
            taskId = 0;
            if (payload == null || !payload.IsPlacementValid)
            {
                return false;
            }

            ReplaceMergedPreviewTasks(payload);
            taskId = _globalTaskBoardService.CreateBuildLifeModuleTask(payload.AnchorCell, currentTick, payload);
            if (taskId == 0)
            {
                return false;
            }

            ApplyPreviewState(payload);
            return true;
        }

        public void ApplyPreviewState(LifeModuleTaskPayload payload)
        {
            if (payload?.Parts == null)
            {
                return;
            }

            for (int i = 0; i < payload.Parts.Length; i++)
            {
                LifeModulePartPayload part = payload.Parts[i];
                ApplyPartToGrid(part, payload.GroupId, LifeModuleType.Preview, allowBuiltCells: true);
            }
        }

        public void ReleasePreviewState(LifeModuleTaskPayload payload)
        {
            if (payload?.OccupiedCells == null)
            {
                return;
            }

            for (int i = 0; i < payload.OccupiedCells.Length; i++)
            {
                Vector2Int cellPos = payload.OccupiedCells[i];
                if (!_gridState.IsInside(cellPos.x, cellPos.y))
                {
                    continue;
                }

                Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
                if (cell.LifeModuleType != LifeModuleType.Preview || cell.LifeModuleGroupId != payload.GroupId)
                {
                    continue;
                }

                ClearLifeModuleState(ref cell);
                _gridState.SetCell(cellPos.x, cellPos.y, cell);
            }
        }

        public bool HasBuildCost(LifeModuleTaskPayload payload)
        {
            if (payload == null)
            {
                return false;
            }

            if (payload.IsBuildCostPaid)
            {
                return true;
            }

            return HasBuildCostForFootprint(payload.OccupiedCells != null ? payload.OccupiedCells.Length : 0);
        }

        public bool TryPayBuildCost(LifeModuleTaskPayload payload)
        {
            if (payload == null)
            {
                return false;
            }

            if (payload.IsBuildCostPaid)
            {
                return true;
            }

            int occupiedCellCount = payload.OccupiedCells != null ? payload.OccupiedCells.Length : 0;
            if (!TrySpendBuildCostForFootprint(occupiedCellCount))
            {
                return false;
            }

            payload.IsBuildCostPaid = true;
            return true;
        }

        public void RefundBuildCost(LifeModuleTaskPayload payload)
        {
            if (payload == null || !payload.IsBuildCostPaid)
            {
                return;
            }

            int occupiedCellCount = payload.OccupiedCells != null ? payload.OccupiedCells.Length : 0;
            RefundBuildCostForFootprint(occupiedCellCount);
            payload.IsBuildCostPaid = false;
        }

        public void FinalizeBuild(LifeModuleTaskPayload payload)
        {
            if (payload?.Parts == null)
            {
                return;
            }

            for (int i = 0; i < payload.Parts.Length; i++)
            {
                LifeModulePartPayload part = payload.Parts[i];
                ApplyPartToGrid(part, payload.GroupId, LifeModuleType.Built, allowBuiltCells: true);
            }
        }

        public bool TryGetLifeModuleGroupId(Vector2Int cell, out int groupId)
        {
            groupId = 0;
            if (!_gridState.IsInside(cell.x, cell.y))
            {
                return false;
            }

            Cell current = _gridState.GetCell(cell.x, cell.y);
            if (current.LifeModuleType == LifeModuleType.None || current.LifeModuleGroupId == 0)
            {
                return false;
            }

            groupId = current.LifeModuleGroupId;
            return true;
        }

        private bool TryResolveMergedBounds(int selectedMinX, int selectedMaxX, int anchorY, out int mergedMinX, out int mergedMaxX, out int[] replacedGroupIds)
        {
            mergedMinX = selectedMinX;
            mergedMaxX = selectedMaxX;
            var mergedGroupIds = new HashSet<int>();

            bool changed;
            do
            {
                changed = false;
                for (int y = anchorY; y < anchorY + PART_HEIGHT; y++)
                {
                    for (int x = mergedMinX - 1; x <= mergedMaxX + 1; x++)
                    {
                        if (!_gridState.IsInside(x, y))
                        {
                            continue;
                        }

                        Cell cell = _gridState.GetCell(x, y);
                        if (cell.LifeModuleType == LifeModuleType.None || cell.LifeModuleGroupId == 0)
                        {
                            continue;
                        }

                        int groupId = cell.LifeModuleGroupId;
                        if (mergedGroupIds.Contains(groupId))
                        {
                            continue;
                        }

                        if (!TryGetGroupBounds(groupId, out int groupMinX, out int groupMaxX, out int groupAnchorY))
                        {
                            continue;
                        }

                        // Merge only modules placed on the same 3-cell-high row band.
                        if (groupAnchorY != anchorY)
                        {
                            continue;
                        }

                        mergedGroupIds.Add(groupId);

                        if (groupMinX < mergedMinX)
                        {
                            mergedMinX = groupMinX;
                            changed = true;
                        }

                        if (groupMaxX > mergedMaxX)
                        {
                            mergedMaxX = groupMaxX;
                            changed = true;
                        }
                    }
                }
            }
            while (changed);

            replacedGroupIds = new int[mergedGroupIds.Count];
            mergedGroupIds.CopyTo(replacedGroupIds);
            return true;
        }

        private bool TryGetGroupBounds(int groupId, out int minX, out int maxX, out int anchorY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            anchorY = int.MinValue;

            for (int y = 0; y < _gridState.Height; y++)
            {
                for (int x = 0; x < _gridState.Width; x++)
                {
                    Cell cell = _gridState.GetCell(x, y);
                    if (cell.LifeModuleGroupId != groupId || cell.LifeModuleType == LifeModuleType.None)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);

                    if (cell.IsLifeModulePartAnchor)
                    {
                        anchorY = y;
                    }
                }
            }

            return minX != int.MaxValue && maxX != int.MinValue && anchorY != int.MinValue;
        }

        private bool ValidateOccupiedCells(Vector2Int[] occupiedCells, int[] replacedGroupIds, bool allowLifeModuleOverlapWithinReplacedGroups)
        {
            var replacedGroups = replacedGroupIds != null ? new HashSet<int>(replacedGroupIds) : new HashSet<int>();

            for (int i = 0; i < occupiedCells.Length; i++)
            {
                Vector2Int occupiedCell = occupiedCells[i];
                if (!_gridState.IsInside(occupiedCell.x, occupiedCell.y))
                {
                    return false;
                }

                Cell cell = _gridState.GetCell(occupiedCell.x, occupiedCell.y);
                if (cell.IsOccupiedByBuilding || _buildingPlacementService.IsPlannedCell(occupiedCell))
                {
                    return false;
                }

                if (!allowLifeModuleOverlapWithinReplacedGroups && cell.LifeModuleType != LifeModuleType.None)
                {
                    return false;
                }

                if (allowLifeModuleOverlapWithinReplacedGroups && cell.LifeModuleType != LifeModuleType.None && !replacedGroups.Contains(cell.LifeModuleGroupId))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AddsNewFootprintCells(Vector2Int[] occupiedCells)
        {
            for (int i = 0; i < occupiedCells.Length; i++)
            {
                Vector2Int occupiedCell = occupiedCells[i];
                if (!_gridState.IsInside(occupiedCell.x, occupiedCell.y))
                {
                    continue;
                }

                Cell cell = _gridState.GetCell(occupiedCell.x, occupiedCell.y);
                if (cell.LifeModuleType == LifeModuleType.None)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AreCellsInsideGrid(Vector2Int[] occupiedCells)
        {
            for (int i = 0; i < occupiedCells.Length; i++)
            {
                Vector2Int occupiedCell = occupiedCells[i];
                if (!_gridState.IsInside(occupiedCell.x, occupiedCell.y))
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2Int[] BuildOccupiedCells(List<LifeModulePartPayload> parts)
        {
            var result = new List<Vector2Int>();
            for (int i = 0; i < parts.Count; i++)
            {
                LifeModulePartPayload part = parts[i];
                for (int y = 0; y < part.Height; y++)
                {
                    for (int x = 0; x < part.Width; x++)
                    {
                        result.Add(new Vector2Int(part.AnchorCell.x + x, part.AnchorCell.y + y));
                    }
                }
            }

            return result.ToArray();
        }

        private static List<LifeModulePartPayload> BuildParts(Vector2Int anchorCell, int totalWidth)
        {
            var result = new List<LifeModulePartPayload>();
            byte order = 0;

            result.Add(CreatePart(LifeModulePartType.Left, anchorCell, SIDE_WIDTH, order++));

            int middleWidth = totalWidth - (SIDE_WIDTH * 2);
            List<int> middlePartWidths = BuildMiddleWidths(middleWidth);
            int currentX = anchorCell.x + SIDE_WIDTH;
            for (int i = 0; i < middlePartWidths.Count; i++)
            {
                int partWidth = middlePartWidths[i];
                LifeModulePartType partType = partWidth switch
                {
                    1 => LifeModulePartType.Middle1,
                    3 => LifeModulePartType.Middle3,
                    4 => LifeModulePartType.Middle4,
                    5 => LifeModulePartType.Middle5,
                    _ => LifeModulePartType.Middle2
                };
                result.Add(CreatePart(partType, new Vector2Int(currentX, anchorCell.y), partWidth, order++));
                currentX += partWidth;
            }

            result.Add(CreatePart(LifeModulePartType.Right, new Vector2Int(anchorCell.x + totalWidth - SIDE_WIDTH, anchorCell.y), SIDE_WIDTH, order));
            return result;
        }

        private static List<int> BuildMiddleWidths(int middleWidth)
        {
            var widths = new List<int>();
            int remainingWidth = middleWidth;
            while (remainingWidth > 0)
            {
                if (remainingWidth == 5)
                {
                    // M5 is a dedicated module part, not a visual stack of M2 + M3.
                    widths.Add(5);
                    remainingWidth = 0;
                    continue;
                }

                if (remainingWidth == 4)
                {
                    widths.Add(4);
                    remainingWidth = 0;
                    continue;
                }

                if (remainingWidth == 3)
                {
                    widths.Add(3);
                    remainingWidth = 0;
                    continue;
                }

                widths.Add(2);
                remainingWidth -= 2;
            }

            for (int i = 0; i < widths.Count - 1; i++)
            {
                if ((widths[i] == 2 && widths[i + 1] == 3) || (widths[i] == 3 && widths[i + 1] == 2))
                {
                    widths[i] = 5;
                    widths.RemoveAt(i + 1);
                    i--;
                }
            }

            for (int i = 0; i < widths.Count - 1; i++)
            {
                if (widths[i] == 2 && widths[i + 1] == 2)
                {
                    widths[i] = 4;
                    widths.RemoveAt(i + 1);
                    i--;
                }
            }

            int m3Index = widths.LastIndexOf(3);
            if (m3Index > 0 && widths.Count > 2)
            {
                widths.RemoveAt(m3Index);
                widths.Insert(widths.Count / 2, 3);
            }

            return widths;
        }

        private static List<LifeModulePartPayload> BuildUnderMinimumPreviewParts(Vector2Int anchorCell, int totalWidth)
        {
            var result = new List<LifeModulePartPayload>(1)
            {
                CreatePart(ResolveStandaloneMiddleType(totalWidth), anchorCell, totalWidth, 0)
            };
            return result;
        }

        private static LifeModulePartType ResolveStandaloneMiddleType(int width)
        {
            return width switch
            {
                1 => LifeModulePartType.Middle1,
                2 => LifeModulePartType.Middle2,
                3 => LifeModulePartType.Middle3,
                4 => LifeModulePartType.Middle4,
                5 => LifeModulePartType.Middle5,
                _ => LifeModulePartType.Middle2
            };
        }

        private static LifeModulePartPayload CreatePart(LifeModulePartType partType, Vector2Int anchorCell, int width, byte order)
        {
            return new LifeModulePartPayload
            {
                PartType = partType,
                AnchorCell = anchorCell,
                Width = (byte)width,
                Height = PART_HEIGHT,
                Order = order
            };
        }

        private void ApplyPartToGrid(LifeModulePartPayload part, int groupId, LifeModuleType stateType, bool allowBuiltCells)
        {
            for (int y = 0; y < part.Height; y++)
            {
                for (int x = 0; x < part.Width; x++)
                {
                    Vector2Int cellPos = new Vector2Int(part.AnchorCell.x + x, part.AnchorCell.y + y);
                    if (!_gridState.IsInside(cellPos.x, cellPos.y))
                    {
                        continue;
                    }

                    Cell cell = _gridState.GetCell(cellPos.x, cellPos.y);
                    if (allowBuiltCells && stateType == LifeModuleType.Preview && cell.LifeModuleType == LifeModuleType.Built)
                    {
                        continue;
                    }

                    cell.LifeModuleType = stateType;
                    cell.LifeModulePartType = part.PartType;
                    cell.LifeModuleGroupId = groupId;
                    cell.LifeModulePartWidth = part.Width;
                    cell.LifeModulePartOrder = part.Order;
                    cell.IsLifeModulePartAnchor = x == 0 && y == 0;
                    _gridState.SetCell(cellPos.x, cellPos.y, cell);
                }
            }
        }

        private void ReplaceMergedPreviewTasks(LifeModuleTaskPayload payload)
        {
            if (payload?.ReplacedGroupIds == null || payload.ReplacedGroupIds.Length == 0)
            {
                return;
            }

            for (int i = 0; i < payload.ReplacedGroupIds.Length; i++)
            {
                int replacedGroupId = payload.ReplacedGroupIds[i];
                if (!_globalTaskBoardService.TryGetTask(replacedGroupId, out UnitTaskRecord task)
                    || task == null
                    || task.TaskType != UnitTaskType.BuildLifeModule
                    || task.LifeModulePayload == null)
                {
                    continue;
                }

                // Merge replaces active preview chains with a new combined task.
                // Built chains keep only their grid state, but planned chains must be removed from task-board lookup first.
                if (_globalTaskBoardService.CancelLifeModuleTaskByCell(
                        task.LifeModulePayload.AnchorCell,
                        out LifeModuleTaskPayload cancelledPayload,
                        out UnitTaskType cancelledTaskType)
                    && cancelledTaskType == UnitTaskType.BuildLifeModule
                    && cancelledPayload != null)
                {
                    ReleasePreviewState(cancelledPayload);
                }
            }
        }

        private bool TrySpendBuildCostForFootprint(int occupiedCellCount)
        {
            if (!HasBuildCostForFootprint(occupiedCellCount))
            {
                return false;
            }

            if (_constructionConfig == null || _constructionConfig.CostPerCellItems == null || _constructionConfig.CostPerCellItems.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < _constructionConfig.CostPerCellItems.Length; i++)
            {
                LifeModuleConstructionConfig.PerCellCostItem item = _constructionConfig.CostPerCellItems[i];
                int totalAmount = Mathf.Max(0, item.AmountPerCell) * occupiedCellCount;
                if (totalAmount <= 0)
                {
                    continue;
                }

                _resourceInventoryService.TryRemove(item.ResourceId, totalAmount);
            }

            return true;
        }

        private bool HasBuildCostForFootprint(int occupiedCellCount)
        {
            if (occupiedCellCount <= 0)
            {
                return false;
            }

            if (_constructionConfig == null || _constructionConfig.CostPerCellItems == null || _constructionConfig.CostPerCellItems.Length == 0)
            {
                return true;
            }

            if (_resourceInventoryService == null)
            {
                return false;
            }

            for (int i = 0; i < _constructionConfig.CostPerCellItems.Length; i++)
            {
                LifeModuleConstructionConfig.PerCellCostItem item = _constructionConfig.CostPerCellItems[i];
                int totalAmount = Mathf.Max(0, item.AmountPerCell) * occupiedCellCount;
                if (totalAmount <= 0)
                {
                    continue;
                }

                if (!_resourceInventoryService.Has(item.ResourceId, totalAmount))
                {
                    return false;
                }
            }

            return true;
        }

        private void RefundBuildCostForFootprint(int occupiedCellCount)
        {
            if (occupiedCellCount <= 0
                || _constructionConfig == null
                || _constructionConfig.CostPerCellItems == null
                || _resourceInventoryService == null)
            {
                return;
            }

            for (int i = 0; i < _constructionConfig.CostPerCellItems.Length; i++)
            {
                LifeModuleConstructionConfig.PerCellCostItem item = _constructionConfig.CostPerCellItems[i];
                int totalAmount = Mathf.Max(0, item.AmountPerCell) * occupiedCellCount;
                if (totalAmount <= 0)
                {
                    continue;
                }

                _resourceInventoryService.Add(item.ResourceId, totalAmount);
            }
        }

        private static void ClearLifeModuleState(ref Cell cell)
        {
            cell.LifeModuleType = LifeModuleType.None;
            cell.LifeModulePartType = LifeModulePartType.None;
            cell.LifeModuleGroupId = 0;
            cell.LifeModulePartWidth = 0;
            cell.LifeModulePartOrder = 0;
            cell.IsLifeModulePartAnchor = false;
        }
    }
}
