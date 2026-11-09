using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SlugOut.Board
{
    public class BoardTile : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int column;
        public int row;
        public TileType tileType;

        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private CanvasGroup canvasGroup;

        public event Action<BoardTile, Vector2> OnSwipeDetected;
        public event Action<BoardTile> OnTileSelected;

        private Vector2 _pointerDownPos;
        private bool _isDragging = false;

        public void Initialize(int col, int r, TileType type)
        {
            column = col;
            row = r;
            SetType(type);
        }

        public void SetType(TileType type)
        {
            tileType = type;
            Color c = TileHelper.GetTileColor(type);

            if (backgroundImage != null)
            {
                backgroundImage.color = c;
            }
            else
            {
                var img = GetComponent<Image>();
                if (img != null) img.color = c;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDownPos = eventData.position;
            _isDragging = false;
            OnTileSelected?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Drag tracking si se requiere feedback visual
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;

            Vector2 dragVector = eventData.position - _pointerDownPos;
            if (dragVector.magnitude > 25f)
            {
                Vector2 swipeDir = Vector2.zero;
                if (Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y))
                {
                    swipeDir = dragVector.x > 0 ? Vector2.right : Vector2.left;
                }
                else
                {
                    swipeDir = dragVector.y > 0 ? Vector2.up : Vector2.down;
                }

                OnSwipeDetected?.Invoke(this, swipeDir);
            }
        }

        public void AnimatePop(Action onComplete)
        {
            // Animacion de desaparicion al hacer match
            LeanTweenOrCoroutinePop(onComplete);
        }

        private void LeanTweenOrCoroutinePop(Action onComplete)
        {
            StartCoroutine(PopRoutine(onComplete));
        }

        private System.Collections.IEnumerator PopRoutine(Action onComplete)
        {
            float elapsed = 0f;
            float duration = 0.15f;
            Vector3 originalScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.3f, progress);
                yield return null;
            }

            transform.localScale = originalScale;
            onComplete?.Invoke();
        }
    }
}
