using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Systems.Power
{
    /// <summary>
    /// Хранит и обновляет заряд батарей (SoC) в кВт*ч.
    /// </summary>
    public sealed class BatteryStorageService
    {
        private readonly Dictionary<Vector2Int, float> _chargeByAnchor = new Dictionary<Vector2Int, float>();

        /// <summary>
        /// Возвращает текущий заряд батареи в кВт*ч.
        /// </summary>
        public float GetChargeKwh(Vector2Int anchorCell)
        {
            return _chargeByAnchor.TryGetValue(anchorCell, out float charge) ? charge : 0f;
        }

        /// <summary>
        /// Возвращает текущий SoC батареи в диапазоне [0..1].
        /// </summary>
        public float GetStateOfCharge01(Vector2Int anchorCell, float capacityKwh)
        {
            if (capacityKwh <= 0f) return 0f;
            float chargeKwh = GetChargeKwh(anchorCell);
            return Mathf.Clamp01(chargeKwh / capacityKwh);
        }

        /// <summary>
        /// Инициализирует запись батареи при появлении в сети.
        /// </summary>
        public void EnsureBattery(Vector2Int anchorCell, float capacityKwh)
        {
            if (_chargeByAnchor.ContainsKey(anchorCell)) return;
            // Новая построенная батарея стартует пустой и заряжается только от реальной генерации сети.
            _chargeByAnchor[anchorCell] = 0f;
        }

        /// <summary>
        /// Удаляет запись батареи после сноса объекта.
        /// </summary>
        public void RemoveBattery(Vector2Int anchorCell)
        {
            _chargeByAnchor.Remove(anchorCell);
        }

        /// <summary>
        /// Пытается зарядить батарею, возвращает фактически принятую мощность в кВт.
        /// </summary>
        public float Charge(Vector2Int anchorCell, float requestedKw, float maxChargeKw, float capacityKwh, float tickHours)
        {
            if (requestedKw <= 0f || maxChargeKw <= 0f || capacityKwh <= 0f || tickHours <= 0f) return 0f;
            EnsureBattery(anchorCell, capacityKwh);

            float current = _chargeByAnchor[anchorCell];
            float allowedKw = Mathf.Min(requestedKw, maxChargeKw);
            float allowedKwh = allowedKw * tickHours;
            float freeKwh = Mathf.Max(0f, capacityKwh - current);
            float acceptedKwh = Mathf.Min(allowedKwh, freeKwh);
            float acceptedKw = acceptedKwh / tickHours;
            _chargeByAnchor[anchorCell] = current + acceptedKwh;
            return acceptedKw;
        }

        /// <summary>
        /// Пытается разрядить батарею, возвращает фактически отданную мощность в кВт.
        /// </summary>
        public float Discharge(Vector2Int anchorCell, float requestedKw, float maxDischargeKw, float tickHours)
        {
            if (requestedKw <= 0f || maxDischargeKw <= 0f || tickHours <= 0f) return 0f;
            if (!_chargeByAnchor.TryGetValue(anchorCell, out float current)) return 0f;

            float allowedKw = Mathf.Min(requestedKw, maxDischargeKw);
            float allowedKwh = allowedKw * tickHours;
            float takenKwh = Mathf.Min(allowedKwh, current);
            float takenKw = takenKwh / tickHours;
            _chargeByAnchor[anchorCell] = current - takenKwh;
            return takenKw;
        }
    }
}
