using System;
using System.Collections.Generic;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Keeps HUD windows mutually exclusive so one opener can close the rest before showing its own content.
    /// </summary>
    public sealed class HudWindowCoordinator
    {
        private readonly Dictionary<string, Action> _closeActions = new Dictionary<string, Action>();
        private readonly HashSet<string> _blockingOpenWindows = new HashSet<string>();

        public bool HasBlockingWindowOpen => _blockingOpenWindows.Count > 0;

        public void Register(string windowId, Action closeAction)
        {
            if (string.IsNullOrWhiteSpace(windowId) || closeAction == null)
            {
                return;
            }

            _closeActions[windowId] = closeAction;
        }

        public void SetBlockingWindowOpen(string windowId, bool isOpen)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                return;
            }

            // Scrollable HUD windows mark themselves here so map zoom ignores mouse wheel while they are visible.
            if (isOpen)
            {
                _blockingOpenWindows.Add(windowId);
                return;
            }

            _blockingOpenWindows.Remove(windowId);
        }

        public void CloseAll(string exceptWindowId = null)
        {
            foreach (KeyValuePair<string, Action> pair in _closeActions)
            {
                if (string.Equals(pair.Key, exceptWindowId, StringComparison.Ordinal))
                {
                    continue;
                }

                pair.Value?.Invoke();
            }
        }
    }
}
