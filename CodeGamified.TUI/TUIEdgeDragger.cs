// ═══════════════════════════════════════════════════════════
//  TUIEdgeDragger — Draggable edge handle for TUI resize
//  Drag any edge to control what % of the screen the TUI uses
//  Deduplicated: identical in BNUI and SRUI
// ═══════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CodeGamified.TUI
{
    public class TUIEdgeDragger : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        public enum Edge { Left, Right, Top, Bottom }

        Edge edge;
        RectTransform targetRect;
        RectTransform canvasRect;
        Image handleImage;

        const float MIN_FRACTION = 0.05f;
        static readonly Color IDLE_COLOR    = new(1f, 1f, 1f, 0f);
        static readonly Color HOVER_COLOR   = new(0.4f, 0.8f, 1f, 0.25f);
        static readonly Color DRAG_COLOR    = new(0.4f, 0.8f, 1f, 0.45f);

        // Linked edges — when this dragger moves, propagate to these targets
        readonly List<(RectTransform rect, Edge edge)> _linkedEdges = new();

        /// <summary>
        /// Optional callback invoked after each drag update.
        /// Receives the current anchor value for this edge.
        /// </summary>
        public Action<float> OnDragged { get; set; }

        /// <summary>The edge this dragger controls.</summary>
        public Edge DragEdge => edge;

        /// <summary>The target RectTransform this dragger resizes.</summary>
        public RectTransform TargetRect => targetRect;

        /// <summary>The canvas RectTransform used for anchor calculations.</summary>
        public RectTransform CanvasRect => canvasRect;

        /// <summary>
        /// Link another panel's edge to follow this dragger's movements.
        /// E.g. dragging a status bar's Top edge can push code panels' Bottom edges.
        /// </summary>
        public TUIEdgeDragger LinkEdge(RectTransform target, Edge targetEdge)
        {
            _linkedEdges.Add((target, targetEdge));
            return this;
        }

        // ── Factory ─────────────────────────────────────────────

        public static TUIEdgeDragger Create(
            RectTransform target, RectTransform canvas, Edge edge, float thickness = 16f)
        {
            var go = new GameObject($"Dragger_{edge}");
            go.transform.SetParent(target, false);

            var rt = go.AddComponent<RectTransform>();
            switch (edge)
            {
                case Edge.Left:
                    rt.anchorMin = new Vector2(0, 0);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot     = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(thickness, 0);
                    rt.anchoredPosition = Vector2.zero;
                    break;
                case Edge.Right:
                    rt.anchorMin = new Vector2(1, 0);
                    rt.anchorMax = new Vector2(1, 1);
                    rt.pivot     = new Vector2(1f, 0.5f);
                    rt.sizeDelta = new Vector2(thickness, 0);
                    rt.anchoredPosition = Vector2.zero;
                    break;
                case Edge.Top:
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(1, 1);
                    rt.pivot     = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(0, thickness);
                    rt.anchoredPosition = Vector2.zero;
                    break;
                case Edge.Bottom:
                    rt.anchorMin = new Vector2(0, 0);
                    rt.anchorMax = new Vector2(1, 0);
                    rt.pivot     = new Vector2(0.5f, 0f);
                    rt.sizeDelta = new Vector2(0, thickness);
                    rt.anchoredPosition = Vector2.zero;
                    break;
            }

            var img = go.AddComponent<Image>();
            img.color = IDLE_COLOR;
            img.raycastTarget = true;

            var dragger = go.AddComponent<TUIEdgeDragger>();
            dragger.edge = edge;
            dragger.targetRect = target;
            dragger.canvasRect = canvas;
            dragger.handleImage = img;

            return dragger;
        }

        // ── Drag handlers ───────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            handleImage.color = DRAG_COLOR;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 canvasSize = canvasRect.rect.size;
            if (canvasSize.x <= 0 || canvasSize.y <= 0) return;

            var canvas = canvasRect.GetComponent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 anchorDelta = new(
                eventData.delta.x / (canvasSize.x * scale),
                eventData.delta.y / (canvasSize.y * scale));

            Vector2 aMin = targetRect.anchorMin;
            Vector2 aMax = targetRect.anchorMax;

            switch (edge)
            {
                case Edge.Left:
                    aMin.x = Mathf.Clamp(aMin.x + anchorDelta.x, 0f, aMax.x - MIN_FRACTION);
                    break;
                case Edge.Right:
                    aMax.x = Mathf.Clamp(aMax.x + anchorDelta.x, aMin.x + MIN_FRACTION, 1f);
                    break;
                case Edge.Top:
                    aMax.y = Mathf.Clamp(aMax.y + anchorDelta.y, aMin.y + MIN_FRACTION, 1f);
                    break;
                case Edge.Bottom:
                    aMin.y = Mathf.Clamp(aMin.y + anchorDelta.y, 0f, aMax.y - MIN_FRACTION);
                    break;
            }

            targetRect.anchorMin = aMin;
            targetRect.anchorMax = aMax;

            // Propagate to linked edges
            if (_linkedEdges.Count > 0)
            {
                float value = GetEdgeAnchorValue(edge, aMin, aMax);
                foreach (var (linkedRect, linkedEdge) in _linkedEdges)
                    ApplyEdgeAnchorValue(linkedRect, linkedEdge, value);
            }

            OnDragged?.Invoke(GetEdgeAnchorValue(edge, aMin, aMax));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            handleImage.color = HOVER_COLOR;
        }

        // ── Edge linking helpers ────────────────────────────────

        static float GetEdgeAnchorValue(Edge e, Vector2 aMin, Vector2 aMax) => e switch
        {
            Edge.Left   => aMin.x,
            Edge.Right  => aMax.x,
            Edge.Top    => aMax.y,
            Edge.Bottom => aMin.y,
            _ => 0f
        };

        static void ApplyEdgeAnchorValue(RectTransform rect, Edge e, float value)
        {
            Vector2 min = rect.anchorMin;
            Vector2 max = rect.anchorMax;
            switch (e)
            {
                case Edge.Left:   min.x = value; break;
                case Edge.Right:  max.x = value; break;
                case Edge.Top:    max.y = value; break;
                case Edge.Bottom: min.y = value; break;
            }
            rect.anchorMin = min;
            rect.anchorMax = max;
        }

        // ── Hover feedback ──────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            handleImage.color = HOVER_COLOR;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            handleImage.color = IDLE_COLOR;
        }
    }
}
