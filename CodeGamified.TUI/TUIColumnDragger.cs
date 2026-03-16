// ═══════════════════════════════════════════════════════════
//  TUIColumnDragger — Draggable column divider inside a TUI panel
//  Drag to resize columns within a multi-column TerminalWindow
//  Counterpart to TUIEdgeDragger (panel edges) — this is for
//  intra-panel column boundaries.
// ═══════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CodeGamified.TUI
{
    public class TUIColumnDragger : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        float _charWidth;
        int _minCharPos;
        int _maxCharPos;
        int _charPos;
        RectTransform _rectTransform;
        RectTransform _parentRect;
        Image _handleImage;
        Action<int> _onPositionChanged;

        /// <summary>
        /// Optional secondary callback for external listeners.
        /// Fires alongside the primary callback when the dragger moves.
        /// </summary>
        public Action<int> ExternalCallback { get; set; }

        // Linked draggers — stay at the same screen X when dragged
        private List<TUIColumnDragger> _linkedDraggers;
        private bool _isSyncing;

        static readonly Color IDLE_COLOR  = new(1f, 1f, 1f, 0f);
        static readonly Color HOVER_COLOR = new(0.4f, 0.8f, 1f, 0.25f);
        static readonly Color DRAG_COLOR  = new(0.4f, 0.8f, 1f, 0.45f);

        /// <summary>Current column position in characters from left edge.</summary>
        public int CharPosition => _charPos;

        /// <summary>Current character width in pixels.</summary>
        public float CharWidth => _charWidth;

        /// <summary>
        /// Link this dragger to another so they stay at the same screen X.
        /// Bidirectional — dragging either one moves the other.
        /// </summary>
        public void LinkDragger(TUIColumnDragger other)
        {
            if (other == null || other == this) return;
            _linkedDraggers ??= new List<TUIColumnDragger>();
            if (!_linkedDraggers.Contains(other)) _linkedDraggers.Add(other);
            other._linkedDraggers ??= new List<TUIColumnDragger>();
            if (!other._linkedDraggers.Contains(this)) other._linkedDraggers.Add(this);
        }

        // ── Factory ─────────────────────────────────────────────

        /// <summary>
        /// Create a column dragger as a child of the given parent panel.
        /// Position is specified in character units; dragger spans full height.
        /// </summary>
        public static TUIColumnDragger Create(
            RectTransform parent, float charWidth, int charPos,
            int minCharPos, int maxCharPos,
            Action<int> onPositionChanged,
            float thickness = 12f)
        {
            var go = new GameObject($"ColDragger_{charPos}");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(thickness, 0);
            rt.anchoredPosition = new Vector2(charPos * charWidth, 0);

            var img = go.AddComponent<Image>();
            img.color = IDLE_COLOR;
            img.raycastTarget = true;

            var dragger = go.AddComponent<TUIColumnDragger>();
            dragger._charWidth = charWidth;
            dragger._charPos = charPos;
            dragger._minCharPos = minCharPos;
            dragger._maxCharPos = maxCharPos;
            dragger._handleImage = img;
            dragger._rectTransform = rt;
            dragger._parentRect = parent;
            dragger._onPositionChanged = onPositionChanged;

            return dragger;
        }

        // ── Update API ──────────────────────────────────────────

        /// <summary>Snap dragger to a new character position (e.g. after resize).</summary>
        public void UpdatePosition(int newCharPos)
        {
            _charPos = newCharPos;
            _rectTransform.anchoredPosition = new Vector2(_charPos * _charWidth, 0);
        }

        /// <summary>
        /// Move dragger to a new position and fire all callbacks
        /// (primary, external, and linked dragger sync).
        /// Use when an external system (e.g. an edge dragger) moves this column.
        /// </summary>
        public void SetPositionWithNotify(int newCharPos)
        {
            newCharPos = Mathf.Clamp(newCharPos, _minCharPos, _maxCharPos);
            if (newCharPos == _charPos) return;
            _charPos = newCharPos;
            _rectTransform.anchoredPosition = new Vector2(_charPos * _charWidth, 0);
            _onPositionChanged?.Invoke(_charPos);
            ExternalCallback?.Invoke(_charPos);
            SyncLinkedDraggers();
        }

        /// <summary>Update drag limits (e.g. when a neighbour dragger moves).</summary>
        public void UpdateLimits(int min, int max)
        {
            _minCharPos = min;
            _maxCharPos = max;
        }

        /// <summary>Update character width (e.g. after font size change).</summary>
        public void UpdateCharWidth(float charWidth)
        {
            _charWidth = charWidth;
            _rectTransform.anchoredPosition = new Vector2(_charPos * _charWidth, 0);
        }

        // ── Drag handlers ───────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            _handleImage.color = DRAG_COLOR;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_parentRect == null || _charWidth <= 0) return;

            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, eventData.position, cam, out Vector2 localPoint))
                return;

            float fromLeft = localPoint.x - _parentRect.rect.xMin;
            int newCharPos = Mathf.RoundToInt(fromLeft / _charWidth);
            newCharPos = Mathf.Clamp(newCharPos, _minCharPos, _maxCharPos);

            if (newCharPos != _charPos)
            {
                _charPos = newCharPos;
                _rectTransform.anchoredPosition = new Vector2(_charPos * _charWidth, 0);
                _onPositionChanged?.Invoke(_charPos);
                ExternalCallback?.Invoke(_charPos);
                SyncLinkedDraggers();
            }
        }

        private void SyncLinkedDraggers()
        {
            if (_linkedDraggers == null || _isSyncing) return;
            _isSyncing = true;

            // This dragger's world X
            float localX = _parentRect.rect.xMin + _charPos * _charWidth;
            Vector3 worldPos = _parentRect.TransformPoint(new Vector3(localX, 0, 0));

            foreach (var linked in _linkedDraggers)
            {
                if (linked == null || linked._isSyncing) continue;
                linked._isSyncing = true;

                // Convert world X into linked dragger's parent space
                Vector3 linkedLocal = linked._parentRect.InverseTransformPoint(worldPos);
                float fromLeft = linkedLocal.x - linked._parentRect.rect.xMin;
                int newPos = Mathf.RoundToInt(fromLeft / linked._charWidth);
                newPos = Mathf.Clamp(newPos, linked._minCharPos, linked._maxCharPos);

                if (newPos != linked._charPos)
                {
                    linked._charPos = newPos;
                    linked._rectTransform.anchoredPosition =
                        new Vector2(linked._charPos * linked._charWidth, 0);
                    linked._onPositionChanged?.Invoke(linked._charPos);
                    linked.ExternalCallback?.Invoke(linked._charPos);
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
