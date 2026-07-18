using _Project.Scripts.Data.Construction;
using _Project.Scripts.Data.Water;
using System;
using UnityEngine;

namespace _Project.Scripts.Presentation.Buildings
{
    /// <summary>
    /// Базовый runtime-view построенного объекта.
    /// Хранит BuildingDef и параметры размещения в сетке.
    /// </summary>
    public abstract class BuildingViewBase : MonoBehaviour
    {
        [Header("Building")]
        // Def построенного объекта (назначается на prefab в инспекторе).
        [SerializeField] private BuildingDef _buildingDef;
        [Header("Pathfinding")]
        // Если true, юниты могут считать клетки этого building view проходимыми при поиске пути.
        [SerializeField] private bool _ignoreAsObstacleForPathfinding;
        [Header("Optimization")]
        // Если true, объект автоматически помечается как static.
        // По умолчанию включено, т.к. постройки не перемещаются и не меняют scale.
        [SerializeField] private bool _canBeStatic = true;
        // Рендерер финального спрайта объекта. Если не задан, будет получен/добавлен автоматически.
        // Ссылка на SpriteRenderer дочернего visual-объекта.

        
        [SerializeField] private SpriteRenderer _rootSpriteRenderer;
        [SerializeField] private SpriteRenderer _visualSpriteRenderer;
        [Header("Water Warning")]
        // Child object name that contains water warning icon sprite renderer.
        [SerializeField] private string _waterWarningChildName = "WaterWarning";
        // Blink interval in seconds when warning is active.
        [SerializeField] private float _waterWarningBlinkIntervalSeconds = 1f;
        // Optional direct link to warning icon sprite renderer.
        [SerializeField] private SpriteRenderer _waterWarningSpriteRenderer;
        [Header("Power Warning")]
        [SerializeField] private string _powerWarningChildName = "PowerWarning";
        [SerializeField] private float _powerWarningBlinkIntervalSeconds = 1f;
        [SerializeField] private SpriteRenderer _powerWarningSpriteRenderer;
        // Кешируем renderers и исходные цвета, чтобы безопасно возвращать tint после режима cable build.
        private SpriteRenderer[] _tintSpriteRenderers = Array.Empty<SpriteRenderer>();
        private Color[] _originalTintColors = Array.Empty<Color>();
        private bool _hasOriginalTintSnapshot;

        // Якорная клетка объекта (левый-нижний угол footprint).
        public Vector2Int AnchorCell { get; private set; }
        // Размер footprint в клетках.
        public Vector2Int Size { get; private set; }
        // Def объекта для runtime-логики.
        public BuildingDef BuildingDef => _buildingDef;
        // Флаг игнорирования препятствия для маршрутизации.
        public bool IgnoreAsObstacleForPathfinding => _ignoreAsObstacleForPathfinding;
        // Разрешение автоматически помечать объект как static.
        public bool CanBeStatic => _canBeStatic;

        /// <summary>ц
        /// Инициализирует размещение объекта в сетке.
        /// </summary>
        public virtual void Initialize(Vector2Int anchorCell, Vector2Int size)
        {
            
            
            AnchorCell = anchorCell;
            Size = size;
            ApplyStaticFlag();
            HideRootSprite();
            ApplyBuiltSprite();
            CacheWaterWarningSpriteRenderer();
            CachePowerWarningSpriteRenderer();
            HideWaterWarning();
            HidePowerWarning();
        }

        private void Awake()
        {
            CacheWaterWarningSpriteRenderer();
            CachePowerWarningSpriteRenderer();
            HideWaterWarning();
            HidePowerWarning();
        }

        private void OnEnable()
        {
            // Safety: hide warning by default for all objects on activation.
            HideWaterWarning();
            HidePowerWarning();
        }

        private void OnValidate()
        {
            ApplyStaticFlag();
            CacheWaterWarningSpriteRenderer();
            CachePowerWarningSpriteRenderer();
            if (_buildingDef == null || _buildingDef.WaterRole != WaterRole.Consumer)
            {
                HideWaterWarning();
            }
            if (_buildingDef == null || !_buildingDef.RequiresPower)
            {
                HidePowerWarning();
            }
        }

        /// <summary>
        /// Применяет static-флаг по настройке компонента.
        /// </summary>
        private void ApplyStaticFlag()
        {
            gameObject.isStatic = _canBeStatic;
        }

        /// <summary>
        /// Применяет финальный спрайт из BuildingDef к SpriteRenderer.
        /// </summary>
        private void ApplyBuiltSprite()
        {
                Debug.Log($"[Build] VisualSpriteRenderer .");
            if (_buildingDef == null) return;
            Debug.Log($"[Build] VisualSpriteRenderer 2.");
            if (_buildingDef.BuiltSprite == null) return;
            Debug.Log($"[Build] VisualSpriteRenderer 3.");

            if (_visualSpriteRenderer == null)
            {
                Debug.LogError($"[Build] VisualSpriteRenderer is not wired on '{name}' view prefab.");
                return;
            }

            Debug.Log($"[Build] VisualSpriteRenderer 4.");
            _visualSpriteRenderer.sprite = _buildingDef.BuiltSprite;
        }

        private void HideRootSprite()
        {
            if (_rootSpriteRenderer != null)
            {
                _rootSpriteRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Applies current water warning state on this view.
        /// </summary>
        public void SetWaterWarningState(bool shouldShowWarning)
        {
            if (_buildingDef == null || _buildingDef.WaterRole != WaterRole.Consumer)
            {
                HideWaterWarning();
                return;
            }

            if (!shouldShowWarning)
            {
                HideWaterWarning();
                return;
            }

            if (_waterWarningSpriteRenderer == null)
            {
                return;
            }

            float interval = Mathf.Max(0.05f, _waterWarningBlinkIntervalSeconds);
            bool visible = Mathf.FloorToInt(Time.time / interval) % 2 == 0;
            _waterWarningSpriteRenderer.enabled = visible;
        }

        /// <summary>
        /// Applies current power warning state on this view.
        /// </summary>
        public void SetPowerWarningState(bool shouldShowWarning)
        {
            if (_buildingDef == null || !_buildingDef.RequiresPower)
            {
                HidePowerWarning();
                return;
            }

            if (!shouldShowWarning)
            {
                HidePowerWarning();
                return;
            }

            if (_powerWarningSpriteRenderer == null)
            {
                return;
            }

            float interval = Mathf.Max(0.05f, _powerWarningBlinkIntervalSeconds);
            bool visible = Mathf.FloorToInt(Time.time / interval) % 2 == 0;
            _powerWarningSpriteRenderer.enabled = visible;
        }

        /// <summary>
        /// Updates optional light-phase driven visuals for a building view.
        /// </summary>
        public virtual void SetLightPhaseState(bool isDay)
        {
        }

        /// <summary>
        /// Applies temporary mode tint to the whole building view.
        /// </summary>
        public void SetModeTint(Color color)
        {
            CacheTintSpriteRenderers();
            CaptureOriginalTintColorsIfNeeded();

            for (int i = 0; i < _tintSpriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = _tintSpriteRenderers[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                spriteRenderer.color = color;
            }
        }

        /// <summary>
        /// Restores original sprite colors after temporary mode tint.
        /// </summary>
        public void ResetModeTint()
        {
            if (!_hasOriginalTintSnapshot)
            {
                return;
            }

            for (int i = 0; i < _tintSpriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = _tintSpriteRenderers[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                spriteRenderer.color = _originalTintColors[i];
            }
        }

        private void HideWaterWarning()
        {
            if (_waterWarningSpriteRenderer != null)
            {
                _waterWarningSpriteRenderer.enabled = false;
            }
        }

        private void HidePowerWarning()
        {
            if (_powerWarningSpriteRenderer != null)
            {
                _powerWarningSpriteRenderer.enabled = false;
            }
        }

        private void CacheWaterWarningSpriteRenderer()
        {
            if (_waterWarningSpriteRenderer != null)
            {
                return;
            }
            _waterWarningSpriteRenderer = FindWarningSpriteRenderer(_waterWarningChildName);
        }

        private void CachePowerWarningSpriteRenderer()
        {
            if (_powerWarningSpriteRenderer != null)
            {
                return;
            }
            _powerWarningSpriteRenderer = FindWarningSpriteRenderer(_powerWarningChildName);
        }

        private void CacheTintSpriteRenderers()
        {
            if (_tintSpriteRenderers.Length > 0)
            {
                return;
            }

            _tintSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void CaptureOriginalTintColorsIfNeeded()
        {
            if (_hasOriginalTintSnapshot)
            {
                return;
            }

            _originalTintColors = new Color[_tintSpriteRenderers.Length];
            for (int i = 0; i < _tintSpriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = _tintSpriteRenderers[i];
                _originalTintColors[i] = spriteRenderer != null ? spriteRenderer.color : Color.white;
            }

            _hasOriginalTintSnapshot = true;
        }

        private SpriteRenderer FindWarningSpriteRenderer(string childName)
        {
            if (string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            Transform directChild = transform.Find(childName);
            if (directChild != null)
            {
                return directChild.GetComponent<SpriteRenderer>();
            }

            Transform iconChild = transform.Find("Icons/" + childName);
            if (iconChild != null)
            {
                return iconChild.GetComponent<SpriteRenderer>();
            }

            Transform visualIconChild = transform.Find("Visual/Icons/" + childName);
            if (visualIconChild != null)
            {
                return visualIconChild.GetComponent<SpriteRenderer>();
            }

            Transform recursiveChild = FindChildRecursive(transform, childName);
            return recursiveChild != null ? recursiveChild.GetComponent<SpriteRenderer>() : null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && string.Equals(child.name, childName, System.StringComparison.Ordinal))
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
