using System;
using _Project.Scripts.Data.Localization;
using _Project.Scripts.Systems.Simulation;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace _Project.Scripts.Presentation.UI
{
    /// <summary>
    /// Обновляет правый верхний индикатор внутреннего игрового времени и счётчик миссий.
    /// </summary>
    public sealed class GameTimeHudPresenter
    {
        private const string ACTIVE_BUTTON_CLASS = "time-control-btn-active";

        private readonly Label _timeLabel;
        private readonly Button _pauseButton;
        private readonly Button _playButton;
        private readonly Button _speedX1Button;
        private readonly Button _speedX5Button;
        private readonly Button _speedX10Button;
        private readonly Button _speedX20Button;
        private readonly Action _pauseAction;
        private readonly Action _playAction;
        private readonly Action<float> _speedChangeAction;
        private readonly LocalizedString _solLabel = new LocalizedString(
            "UI", LocalizationKeyBuilder.FromEnum("time", GameTimeLocalizationToken.Sol));
        private readonly LocalizedString _dayLabel = new LocalizedString(
            "UI", LocalizationKeyBuilder.FromEnum("time", GameTimeLocalizationToken.Day));
        private readonly LocalizedString _nightLabel = new LocalizedString(
            "UI", LocalizationKeyBuilder.FromEnum("time", GameTimeLocalizationToken.Night));

        public GameTimeHudPresenter(
            VisualElement root,
            Action pauseAction,
            Action playAction,
            Action<float> speedChangeAction)
        {
            _timeLabel = root?.Q<Label>("game-time-label");
            _pauseButton = root?.Q<Button>("time-pause-btn");
            _playButton = root?.Q<Button>("time-play-btn");
            _speedX1Button = root?.Q<Button>("time-speed-x1-btn");
            _speedX5Button = root?.Q<Button>("time-speed-x5-btn");
            _speedX10Button = root?.Q<Button>("time-speed-x10-btn");
            _speedX20Button = root?.Q<Button>("time-speed-x20-btn");
            _pauseAction = pauseAction;
            _playAction = playAction;
            _speedChangeAction = speedChangeAction;

            _pauseButton?.RegisterCallback<ClickEvent>(OnPauseClicked);
            _playButton?.RegisterCallback<ClickEvent>(OnPlayClicked);
            _speedX1Button?.RegisterCallback<ClickEvent>(OnSpeedX1Clicked);
            _speedX5Button?.RegisterCallback<ClickEvent>(OnSpeedX5Clicked);
            _speedX10Button?.RegisterCallback<ClickEvent>(OnSpeedX10Clicked);
            _speedX20Button?.RegisterCallback<ClickEvent>(OnSpeedX20Clicked);
        }

        public void Refresh(GameTimeService gameTimeService)
        {
            if (_timeLabel == null)
            {
                return;
            }

            int minutes = gameTimeService.TotalGameMinutes % 60;
            string phaseText = (gameTimeService.IsDay ? _dayLabel : _nightLabel).GetLocalizedString();
            string solText = _solLabel.GetLocalizedString();
            _timeLabel.text = $"{gameTimeService.Hour:00}:{minutes:00} / {solText} {gameTimeService.Sol} / {phaseText}";
        }

        // Keeps the HUD buttons aligned with the current simulation mode.
        public void RefreshControls(bool isPaused, float speedMultiplier)
        {
            SetButtonState(_pauseButton, isPaused);
            SetButtonState(_playButton, !isPaused);
            SetButtonState(_speedX1Button, !isPaused && Mathf.Approximately(speedMultiplier, 1f));
            SetButtonState(_speedX5Button, !isPaused && Mathf.Approximately(speedMultiplier, 5f));
            SetButtonState(_speedX10Button, !isPaused && Mathf.Approximately(speedMultiplier, 10f));
            SetButtonState(_speedX20Button, !isPaused && Mathf.Approximately(speedMultiplier, 20f));
        }

        public void RefreshRocketMissionCount(int missionCount)
        {
            _ = missionCount;
        }

        private void OnPauseClicked(ClickEvent _)
        {
            _pauseAction?.Invoke();
        }

        private void OnPlayClicked(ClickEvent _)
        {
            _playAction?.Invoke();
        }

        private void OnSpeedX1Clicked(ClickEvent _)
        {
            _speedChangeAction?.Invoke(1f);
        }

        private void OnSpeedX5Clicked(ClickEvent _)
        {
            _speedChangeAction?.Invoke(5f);
        }

        private void OnSpeedX10Clicked(ClickEvent _)
        {
            _speedChangeAction?.Invoke(10f);
        }

        private void OnSpeedX20Clicked(ClickEvent _)
        {
            _speedChangeAction?.Invoke(20f);
        }

        private static void SetButtonState(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            if (isActive)
            {
                button.AddToClassList(ACTIVE_BUTTON_CLASS);
            }
            else
            {
                button.RemoveFromClassList(ACTIVE_BUTTON_CLASS);
            }
        }
    }
}