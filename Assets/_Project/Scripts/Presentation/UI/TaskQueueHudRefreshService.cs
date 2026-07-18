namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Обновляет HUD-панель очереди задач.
    /// </summary>
    public sealed class TaskQueueHudRefreshService
    {
        private readonly TaskQueuePanelPresenter _taskQueuePanelPresenter;
        private readonly TaskQueueHudBuilder _taskQueueHudBuilder;
        private int _lastStateHash = int.MinValue;

        public TaskQueueHudRefreshService(
            TaskQueuePanelPresenter taskQueuePanelPresenter,
            TaskQueueHudBuilder taskQueueHudBuilder)
        {
            _taskQueuePanelPresenter = taskQueuePanelPresenter;
            _taskQueueHudBuilder = taskQueueHudBuilder;
        }

        /// <summary>
        /// Пересобирает и перерисовывает список задач в HUD.
        /// </summary>
        public void Refresh()
        {
            if (_taskQueuePanelPresenter == null) return;
            if (_taskQueueHudBuilder == null) return;

            int currentStateHash = _taskQueueHudBuilder.BuildStateHash();
            if (currentStateHash == _lastStateHash)
            {
                return;
            }

            var items = _taskQueueHudBuilder.BuildItems();
            _taskQueuePanelPresenter.Render(items);
            _lastStateHash = currentStateHash;
        }
    }
}
