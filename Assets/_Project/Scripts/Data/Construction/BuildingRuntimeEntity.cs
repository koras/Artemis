using UnityEngine;

namespace _Project.Scripts.Data.Construction
{
    /// <summary>
    /// Runtime-сущность построечного объекта в мире.
    /// </summary>
    public sealed class BuildingRuntimeEntity
    {
        public BuildingDef BuildingDef { get; }
        public Vector2Int AnchorCell { get; }
        public Vector2Int Size { get; }
        public bool IsRotated { get; }
        public BuildingRuntimeStatus Status { get; private set; }
        public bool IsActive => Status == BuildingRuntimeStatus.Active;
        public bool IsOperational { get; private set; }
        public bool IsWaterProducerEnabled { get; private set; }
        public bool IsOxygenProducerEnabled { get; private set; }

        public BuildingRuntimeEntity(
            BuildingDef buildingDef,
            Vector2Int anchorCell,
            Vector2Int size,
            bool isRotated,
            BuildingRuntimeStatus status)
        {
            BuildingDef = buildingDef;
            AnchorCell = anchorCell;
            Size = size;
            IsRotated = isRotated;
            Status = status;
            IsOperational = false;
            IsWaterProducerEnabled = buildingDef != null && buildingDef.IsWaterProducerEnabledByDefault;
            IsOxygenProducerEnabled = buildingDef != null && buildingDef.IsOxygenProducerEnabledByDefault;
        }

        /// <summary>
        /// Меняет runtime-статус сущности постройки.
        /// </summary>
        public void SetStatus(BuildingRuntimeStatus status)
        {
            Status = status;
        }

        /// <summary>
        /// Обновляет runtime-признак работоспособности постройки.
        /// </summary>
        public void SetOperational(bool isOperational)
        {
            IsOperational = isOperational;
        }

        /// <summary>
        /// Updates runtime water producer switch state.
        /// </summary>
        public void SetWaterProducerEnabled(bool isEnabled)
        {
            IsWaterProducerEnabled = isEnabled;
        }

        /// <summary>
        /// Toggles runtime water producer switch state and returns the new value.
        /// </summary>
        public bool ToggleWaterProducer()
        {
            IsWaterProducerEnabled = !IsWaterProducerEnabled;
            return IsWaterProducerEnabled;
        }

        /// <summary>
        /// Updates runtime oxygen producer switch state.
        /// </summary>
        public void SetOxygenProducerEnabled(bool isEnabled)
        {
            IsOxygenProducerEnabled = isEnabled;
        }

        /// <summary>
        /// Toggles runtime oxygen producer switch state and returns the new value.
        /// </summary>
        public bool ToggleOxygenProducer()
        {
            IsOxygenProducerEnabled = !IsOxygenProducerEnabled;
            return IsOxygenProducerEnabled;
        }
    }
}
