using UnityEngine;

namespace _Project.Scripts.Systems.Resources
{
    /// <summary>
    /// Данные ресурса, лежащего на сцене в виде префаба.
    /// </summary>
    public sealed class SceneResourceDropView : MonoBehaviour
    {
        [SerializeField] private string _resourceId;
        [SerializeField] private int _amount;

        /// <summary>
        /// Инициализирует runtime-данные ресурса после спавна префаба.
        /// </summary>
        public void Initialize(string resourceId, int amount)
        {
            _resourceId = resourceId;
            _amount = Mathf.Max(0, amount);
        }

        /// <summary>
        /// Добавляет количество ресурса в текущий дроп.
        /// </summary>
        public void AddAmount(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _amount += amount;
        }

        public string ResourceId => _resourceId;
        public int Amount => _amount;
    }
}
