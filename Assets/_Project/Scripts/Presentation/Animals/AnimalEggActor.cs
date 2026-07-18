using System;
using DG.Tweening;
using UnityEngine;

namespace _Project.Scripts.Presentation.Animals
{
    /// <summary>
    /// Lightweight visual actor for a world egg entity.
    /// </summary>
    public sealed class AnimalEggActor : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private string _sourceSpeciesId;
        private Tween _activeTween;

        public string SourceSpeciesId => _sourceSpeciesId;

        public void SetSourceSpeciesId(string sourceSpeciesId)
        {
            _sourceSpeciesId = string.IsNullOrWhiteSpace(sourceSpeciesId)
                ? string.Empty
                : sourceSpeciesId;
        }

        public void SnapToWorldPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }

        public void PlayHatchAnimation(float durationSeconds, Action onCompleted)
        {
            if (_visual == null)
            {
                _visual = transform;
            }

            _activeTween?.Kill();

            Vector3 startScale = _visual.localScale;
            var sequence = DOTween.Sequence();
            // Small squash-and-pop read gives the egg a visible hatch beat before it disappears.
            sequence.Append(_visual.DOScale(startScale * 1.18f, durationSeconds * 0.45f).SetEase(Ease.OutBack));
            sequence.Append(_visual.DOScale(startScale * 0.08f, durationSeconds * 0.55f).SetEase(Ease.InBack));
            sequence.OnComplete(() =>
            {
                _activeTween = null;
                onCompleted?.Invoke();
            });
            _activeTween = sequence;
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

        private void Awake()
        {
            if (_visual == null)
            {
                _visual = transform;
            }
        }

        private void OnDestroy()
        {
            _activeTween?.Kill();
        }
    }
}
