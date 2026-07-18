using DG.Tweening;
using _Project.Scripts.Systems.Grid;
using _Project.Scripts.Systems.Simulation;
using System;
using UnityEngine;

namespace _Project.Scripts.Systems.External
{
    /// <summary>
    /// Сервис прилета корабля с Земли по игровому расписанию.
    /// </summary>
    public sealed class IronRocketArrivalService
    {
        public event Action<RocketMissionResult> MissionResolved;

        public enum ArrivalCadenceMode
        {
            ByDays = 0,
            ByHours = 1
        }

        public enum ArrivalOutcomeMode
        {
            Success = 0,
            Crash = 1
        }

        public enum RocketMissionResultType
        {
            Success = 0,
            Crash = 1
        }

        public readonly struct RocketMissionResult
        {
            public readonly RocketMissionResultType ResultType;
            public readonly int MissionCount;
            public readonly int ScheduledMissionIndex;

            public RocketMissionResult(RocketMissionResultType resultType, int missionCount, int scheduledMissionIndex)
            {
                ResultType = resultType;
                MissionCount = missionCount;
                ScheduledMissionIndex = scheduledMissionIndex;
            }

            public bool IsSuccess => ResultType == RocketMissionResultType.Success;
        }

        private enum RocketFlightState
        {
            Idle = 0,
            Descending = 1,
            Landed = 2,
            Ascending = 3
        }

        private readonly GameTimeService _gameTimeService;
        private readonly GridCoordinateConverter _gridCoordinateConverter;
        private readonly GameObject _rocketPrefab;
        private readonly Transform _rocketRoot;
        private readonly Vector2Int _spawnCell;
        private readonly Vector2Int _landingCell;
        private readonly bool _shouldLand;
        private readonly ArrivalOutcomeMode _arrivalOutcomeMode;
        private readonly float _descendDurationSeconds;
        private readonly float _ascendDurationSeconds;
        private readonly int _stayDurationGameHours;
        private readonly int _cadenceHours;

        private RocketFlightState _state;
        private int _lastProcessedGameHour = -1;
        private int _nextArrivalGameHour;
        private int _landedAtGameHour = -1;
        private GameObject _activeRocket;
        private Tween _activeTween;

        public int MissionCount { get; private set; }
        public int ScheduledMissionIndex { get; private set; }
        public int NextArrivalGameHour => _nextArrivalGameHour;
        public int CadenceHours => _cadenceHours;
        public int RemainingHoursToNextArrival => Mathf.Max(0, _nextArrivalGameHour - _gameTimeService.TotalGameHours);

        public IronRocketArrivalService(
            GameTimeService gameTimeService,
            GridCoordinateConverter gridCoordinateConverter,
            GameObject rocketPrefab,
            Transform rocketRoot,
            ArrivalCadenceMode cadenceMode,
            int cadenceValue,
            Vector2Int spawnCell,
            Vector2Int landingCell,
            bool shouldLand,
            ArrivalOutcomeMode arrivalOutcomeMode,
            float descendDurationSeconds,
            float ascendDurationSeconds,
            int stayDurationGameHours)
        {
            _gameTimeService = gameTimeService;
            _gridCoordinateConverter = gridCoordinateConverter;
            _rocketPrefab = rocketPrefab;
            _rocketRoot = rocketRoot;
            _spawnCell = spawnCell;
            _landingCell = landingCell;
            _shouldLand = shouldLand;
            _arrivalOutcomeMode = arrivalOutcomeMode;
            _descendDurationSeconds = Mathf.Max(0.01f, descendDurationSeconds);
            _ascendDurationSeconds = Mathf.Max(0.01f, ascendDurationSeconds);
            _stayDurationGameHours = Mathf.Max(1, stayDurationGameHours);
            _cadenceHours = ResolveCadenceHours(cadenceMode, cadenceValue);
            _nextArrivalGameHour = _gameTimeService.TotalGameHours + _cadenceHours;
        }

        public void Tick()
        {
            int currentGameHour = _gameTimeService.TotalGameHours;
            if (currentGameHour == _lastProcessedGameHour)
            {
                return;
            }

            _lastProcessedGameHour = currentGameHour;

            if (_state == RocketFlightState.Landed)
            {
                if (currentGameHour - _landedAtGameHour >= _stayDurationGameHours)
                {
                    StartAscend();
                }

                return;
            }

            if (_state != RocketFlightState.Idle)
            {
                return;
            }

            if (currentGameHour < _nextArrivalGameHour)
            {
                return;
            }

            StartArrival();
            _nextArrivalGameHour = currentGameHour + _cadenceHours;
        }

        public void Dispose()
        {
            KillActiveTween();

            if (_activeRocket != null)
            {
                UnityEngine.Object.Destroy(_activeRocket);
                _activeRocket = null;
            }

            _state = RocketFlightState.Idle;
        }

        public void SetPaused(bool isPaused)
        {
            if (_activeTween == null || !_activeTween.IsActive())
            {
                return;
            }

            if (isPaused)
            {
                _activeTween.Pause();
                return;
            }

            _activeTween.Play();
        }

        private void StartArrival()
        {
            if (_rocketPrefab == null)
            {
            // Debug.LogError("[IronRocketArrivalService] Rocket prefab is not assigned.");
                return;
            }

            // Scheduled missions count every launch attempt; successful missions count only resolved landings.
            ScheduledMissionIndex++;

            if (_activeRocket != null)
            {
                UnityEngine.Object.Destroy(_activeRocket);
                _activeRocket = null;
            }

            Vector2 spawnWorld2D = _gridCoordinateConverter.CellToWorldCenter(_spawnCell);
            _activeRocket = UnityEngine.Object.Instantiate(_rocketPrefab, new Vector3(spawnWorld2D.x, spawnWorld2D.y, 0f), Quaternion.identity, _rocketRoot);

            if (!_shouldLand)
            {
                ResolveArrivalOutcome(true);
                return;
            }

            Vector2 landingWorld2D = _gridCoordinateConverter.CellToWorldCenter(_landingCell);
            _state = RocketFlightState.Descending;
            _activeTween = _activeRocket.transform
                .DOMove(new Vector3(landingWorld2D.x, landingWorld2D.y, _activeRocket.transform.position.z), _descendDurationSeconds)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    _activeTween = null;
                    // Mission result is emitted only after the descend tween has actually finished.
                    ResolveArrivalOutcome(true);
                });
        }

        private void StartAscend()
        {
            if (_activeRocket == null)
            {
                _state = RocketFlightState.Idle;
                return;
            }

            Vector2 spawnWorld2D = _gridCoordinateConverter.CellToWorldCenter(_spawnCell);
            _state = RocketFlightState.Ascending;
            KillActiveTween();
            _activeTween = _activeRocket.transform
                .DOMove(new Vector3(spawnWorld2D.x, spawnWorld2D.y, _activeRocket.transform.position.z), _ascendDurationSeconds)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    _activeTween = null;
                    UnityEngine.Object.Destroy(_activeRocket);
                    _activeRocket = null;
                    _state = RocketFlightState.Idle;
                    _landedAtGameHour = -1;
                });
        }

        private void KillActiveTween()
        {
            if (_activeTween == null)
            {
                return;
            }

            _activeTween.Kill(false);
            _activeTween = null;
        }

        private void ResolveArrivalOutcome(bool enterLandedState)
        {
            if (_arrivalOutcomeMode == ArrivalOutcomeMode.Success)
            {
                MissionCount++;
                if (enterLandedState)
                {
                    _state = RocketFlightState.Landed;
                    _landedAtGameHour = _gameTimeService.TotalGameHours;
                }
                else
                {
                    _state = RocketFlightState.Idle;
                    _landedAtGameHour = -1;
                }

                MissionResolved?.Invoke(new RocketMissionResult(
                    RocketMissionResultType.Success,
                    MissionCount,
                    ScheduledMissionIndex));
                return;
            }

            MissionResolved?.Invoke(new RocketMissionResult(
                RocketMissionResultType.Crash,
                MissionCount,
                ScheduledMissionIndex));

            if (_activeRocket != null)
            {
                UnityEngine.Object.Destroy(_activeRocket);
                _activeRocket = null;
            }

            _state = RocketFlightState.Idle;
            _landedAtGameHour = -1;
        }

        private static int ResolveCadenceHours(ArrivalCadenceMode cadenceMode, int cadenceValue)
        {
            int clampedValue = Mathf.Max(1, cadenceValue);
            if (cadenceMode == ArrivalCadenceMode.ByDays)
            {
                return clampedValue * 24;
            }

            return clampedValue;
        }
    }
}
