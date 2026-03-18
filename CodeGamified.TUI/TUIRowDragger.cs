// ═══════════════════════════════════════════════════════════
//  TUIRowDragger — Draggable row divider inside a TUI panel
//  Drag to resize vertical sections within a TerminalWindow.
//  Counterpart to TUIColumnDragger (intra-panel columns) —
//  this is for intra-panel row boundaries.
// ═══════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CodeGamified.TUI
{
    public class TUIRowDragger : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        float _rowHeight;
        int _minRowPos;
        int _maxRowPos;
        int _rowPos;
        RectTransform _rectTransform;
        RectTransform _parentRect;
        Image _handleImage;
        Action<int> _onPositionChanged;

        /// <summary>
        /// Optional secondary callback for external listeners.
        /// Fires alongside the primary callback when the dragger moves.
        /// </summary>
        public Action<int> ExternalCallback { get; set; }

        // Linked draggers — stay at the same screen Y when dragged
        private List<TUIRowDragger> _linkedDraggers;
        private bool _isSyncing;

        static readonly Color IDLE_COLOR  = new(1f, 1f, 1f, 0f);
        static readonly Color HOVER_COLOR = new(0.4f, 0.8f, 1f, 0.25f);
        static readonly Color DRAG_COLOR  = new(0.4f, 0.8f, 1f, 0.45f);

        /// <summary>Current row position (rows from top, 0-based).</summary>
        public int RowPosition => _rowPos;

        /// <summary>Current row height in pixels.</summary>
        public float RowHeight => _rowHeight;

        /// <summary>
        /// Link this dragger to another so they stay at the same screen Y.
        /// Bidirectional — dragging either one moves the other.
        /// </summary>
        public void LinkDragger(TUIRowDragger other)
        {
            if (other == null || other == this) return;
            _linkedDraggers ??= new List<TUIRowDragger>();
            if (!_linkedDraggers.Contains(other)) _linkedDraggers.Add(other);
            other._linkedDraggers ??= new List<TUIRowDragger>();
            if (!other._linkedDraggers.Contains(this)) other._linkedDraggers.Add(this);
        }

        // ── Factory ─────────────────────────────────────────────

        /// <summary>
        /// Create a row dragger as a child of the given parent panel.
        /// Position is specified in row units; dragger spans full width.
        /// Rows count from the top of the panel downward.
        /// </summary>
        public static TUIRowDragger Create(
            RectTransform parent, float rowHeight, int rowPos,
            int minRowPos, int maxRowPos,
            Action<int> onPositionChanged,
            float thickness = 12f)
        {
            var go = new GameObject($"RowDragger_{rowPos}");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            // Anchor to top, span full width
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0, thickness);
            rt.anchoredPosition = new Vector2(0, -rowPos * rowHeight);

            var img = go.AddComponent<Image>();
            img.color = IDLE_COLOR;
            img.raycastTarget = true;

            var dragger = go.AddComponent<TUIRowDragger>();
            dragger._rowHeight = rowHeight;
            dragger._rowPos = rowPos;
            dragger._minRowPos = minRowPos;
            dragger._maxRowPos = maxRowPos;
            dragger._handleImage = img;
            dragger._rectTransform = rt;
            dragger._parentRect = parent;
            dragger._onPositionChanged = onPositionChanged;

            return dragger;
        }

        // ── Update API ──────────────────────────────────────────

        /// <summary>Snap dragger to a new row position (e.g. after resize).</summary>
        public void UpdatePosition(int newRowPos)
        {
            _rowPos = newRowPos;
            _rectTransform.anchoredPosition = new Vector2(0, -_rowPos * _rowHeight);
        }

        /// <summary>
        /// Move dragger to a new position and fire all callbacks
        /// (primary, external, and linked dragger sync).
        /// </summary>
        public void SetPositionWithNotify(int newRowPos)
        {
            newRowPos = Mathf.Clamp(newRowPos, _minRowPos, _maxRowPos);
            if (newRowPos == _rowPos) return;
            _rowPos = newRowPos;
            _rectTransform.anchoredPosition = new Vector2(0, -_rowPos * _rowHeight);
            _onPositionChanged?.Invoke(_rowPos);
            ExternalCallback?.Invoke(_rowPos);
            SyncLinkedDraggers();
        }

        /// <summary>Update drag limits (e.g. when a neighbour dragger moves).</summary>
        public void UpdateLimits(int min, int max)
        {
            _minRowPos = min;
            _maxRowPos = max;
        }

        /// <summary>Update row height (e.g. after font size change).</summary>
        public void UpdateRowHeight(float rowHeight)
        {
            _rowHeight = rowHeight;
            _rectTransform.anchoredPosition = new Vector2(0, -_rowPos * _rowHeight);
        }

        // ── Drag handlers ───────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            _handleImage.color = DRAG_COLOR;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_parentRect == null || _rowHeight <= 0) return;

            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, eventData.position, cam, out Vector2 localPoint))
                return;

            // Rows counted from top: row 0 is at yMax
            float fromTop = _parentRect.rect.yMax - localPoint.y;
            int newRowPos = Mathf.RoundToInt(fromTop / _rowHeight);
            newRowPos = Mathf.Clamp(newRowPos, _minRowPos, _maxRowPos);

            if (newRowPos != _rowPos)
            {
                _rowPos = newRowPos;
                _rectTransform.anchoredPosition = new Vector2(0, -_rowPos * _rowHeight);
                _onPositionChanged?.Invoke(_rowPos);
                ExternalCallback?.Invoke(_rowPos);
                SyncLinkedDraggers();
            }
        }

        private void SyncLinkedDraggers()
        {
            if (_linkedDraggers == null || _isSyncing) return;
            _isSyncing = true;

            // This dragger's world Y
            float localY = _parentRect.rect.yMax - _rowPos * _rowHeight;
            Vector3 worldPos = _parentRect.TransformPoint(new Vector3(0, localY, 0));

            foreach (var linked in _linkedDraggers)
            {
                if (linked == null || linked._isSyncing) continue;
                linked._isSyncing = true;

                Vector3 linkedLocal = linked._parentRect.InverseTransformPoint(worldPos);
                float fromTop = linked._parentRect.rect.yMax - linkedLocal.y;
                int newPos = Mathf.RoundToInt(fromTop / linked._rowHeight);
                newPos = Mathf.Clamp(newPos, linked._minRowPos, linked._maxRowPos);

                if (newPos != linked._rowPos)
                {
                    linked._rowPos = newPos;
                    linked._rectTransform.anchoredPosition =
                        new Vector2(0, -linked._rowPos * linked._rowHeight);
                    linked._onPositionChanged?.Invoke(linked._rowPos);
                    linked.ExternalCallback?.Invoke(linked._rowPos);
                }

                linked._isSyncing = false;
            }

            _isSyncing = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _handleImage.color = HOVER_COLOR;
        }

        // ── Hover feedback ──────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            _handleImage.color = HOVER_COLOR;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _handleImage.color = IDLE_COLOR;
        }
    }
}
