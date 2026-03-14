// CodeGamified.Procedural — Shared procedural rendering framework
// MIT License
using UnityEngine;
using System;
using System.Collections.Generic;

namespace CodeGamified.Procedural
{
    /// <summary>
    /// Visual channel type for data-driven animation bindings.
    /// </summary>
    public enum VisualChannel
    {
        Emission,    // Material emission intensity (glow)
        ScaleY,      // Local Y scale (fill bars, growth)
        ColorAlpha,  // Material alpha (fade in/out)
        PositionY,   // Local Y offset (bob, bounce)
        ColorTint    // Blend toward a target color
    }

    /// <summary>
    /// Generic animation hooks for procedural GameObjects.
    ///
    /// Replaces per-module animation logic:
    ///   Rack:  OnInstructionExecuted → pulse emission on avionics slot
    ///   Crew:  idle bob → Sin(time) on PositionY
    ///   Satellite: spin → handled externally, but emission glow for active state
    ///   Launch: exhaust brightness during burn
    ///
    /// Two modes:
    ///   1. Imperative: Pulse(), Throb(), SetEmission() — fire-and-forget
    ///   2. Declarative: Bind() — continuously drives a visual channel from a data source
    /// </summary>
    public class ProceduralVisualState : MonoBehaviour
    {
        // ── Renderer lookup ─────────────────────────────────────
        private Dictionary<string, Renderer> _renderers;
        private Dictionary<string, MaterialPropertyBlock> _propBlocks;

        // ── Active animations ───────────────────────────────────
        private readonly List<PulseAnim> _pulses = new(8);
        private readonly List<ThrobAnim> _throbs = new(4);
        private readonly List<Binding> _bindings = new(8);

        // ── Structs ─────────────────────────────────────────────

        struct PulseAnim
        {
            public string partId;
            public Color color;
            public float duration;
            public float elapsed;
        }

        struct ThrobAnim
        {
            public string partId;
            public float scaleMultiplier;
            public float duration;
            public float elapsed;
            public Vector3 originalScale;
        }

        struct Binding
        {
            public string partId;
            public Func<float> source;
            public VisualChannel channel;
            public float min;
            public float max;
        }

        // ═══════════════════════════════════════════════════════════════
        // INIT
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize with the renderer map from AssemblyResult.
        /// Called automatically by ProceduralAssembler.BuildWithVisualState.
        /// </summary>
        public void Initialize(Dictionary<string, Renderer> renderers)
        {
            _renderers = renderers ?? new Dictionary<string, Renderer>();
            _propBlocks = new Dictionary<string, MaterialPropertyBlock>(_renderers.Count);
            foreach (var kv in _renderers)
                _propBlocks[kv.Key] = new MaterialPropertyBlock();
        }

        // ═══════════════════════════════════════════════════════════════
        // IMPERATIVE API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Flash a part's emission color, then fade to zero.
        /// Replaces: RackVisualization instruction pulse, comms flash.
        /// </summary>
        public void Pulse(string partId, Color color, float duration = 0.2f)
        {
            if (!_renderers.ContainsKey(partId)) return;
            _pulses.Add(new PulseAnim
            {
                partId = partId,
                color = color,
                duration = duration,
                elapsed = 0f
            });
        }

        /// <summary>
        /// Briefly scale a part up then back to original.
        /// Replaces: CrewAgent idle bob, engine throb.
        /// </summary>
        public void Throb(string partId, float scaleMultiplier = 1.2f, float duration = 0.3f)
        {
            if (!_renderers.ContainsKey(partId)) return;
            var t = _renderers[partId].transform;
            _throbs.Add(new ThrobAnim
            {
                partId = partId,
                scaleMultiplier = scaleMultiplier,
                duration = duration,
                elapsed = 0f,
                originalScale = t.localScale
            });
        }

        /// <summary>
        /// Set a part's emission intensity directly.
        /// </summary>
        public void SetEmission(string partId, float intensity)
        {
            if (!TryGetRendererAndBlock(partId, out var r, out var block)) return;
            Color emColor = Color.white * intensity;
            block.SetColor("_EmissiveColor", emColor);
            block.SetColor("_EmissionColor", emColor); // URP fallback
            r.SetPropertyBlock(block);
        }

        // ═══════════════════════════════════════════════════════════════
        // DECLARATIVE API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Bind a part's visual channel to a continuous data source.
        /// The source function is polled every frame.
        ///
        /// Example: Bind("Battery", () => battery.Charge, VisualChannel.ScaleY, 0.1f, 1f)
        /// </summary>
        public void Bind(string partId, Func<float> source, VisualChannel channel,
            float min = 0f, float max = 1f)
        {
            if (!_renderers.ContainsKey(partId)) return;
            _bindings.Add(new Binding
            {
                partId = partId,
                source = source,
                channel = channel,
                min = min,
                max = max
            });
        }

        /// <summary>
        /// Remove all bindings for a part.
        /// </summary>
        public void Unbind(string partId)
        {
            for (int i = _bindings.Count - 1; i >= 0; i--)
                if (_bindings[i].partId == partId)
                    _bindings.RemoveAt(i);
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdatePulses(dt);
            UpdateThrobs(dt);
            UpdateBindings();
        }

        void UpdatePulses(float dt)
        {
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                var p = _pulses[i];
                p.elapsed += dt;

                if (p.elapsed >= p.duration)
                {
                    SetEmission(p.partId, 0f);
                    _pulses.RemoveAt(i);
                    continue;
                }

                float t = 1f - (p.elapsed / p.duration); // fade out
                if (TryGetRendererAndBlock(p.partId, out var r, out var block))
                {
                    Color emColor = p.color * t;
                    block.SetColor("_EmissiveColor", emColor);
                    block.SetColor("_EmissionColor", emColor);
                    r.SetPropertyBlock(block);
                }
                _pulses[i] = p;
            }
        }

        void UpdateThrobs(float dt)
        {
            for (int i = _throbs.Count - 1; i >= 0; i--)
            {
                var th = _throbs[i];
                th.elapsed += dt;

                if (th.elapsed >= th.duration)
                {
                    _renderers[th.partId].transform.localScale = th.originalScale;
                    _throbs.RemoveAt(i);
                    continue;
                }

                float t = th.elapsed / th.duration;
                float curve = Mathf.Sin(t * Mathf.PI); // 0→1→0
                float s = Mathf.Lerp(1f, th.scaleMultiplier, curve);
                _renderers[th.partId].transform.localScale = th.originalScale * s;
                _throbs[i] = th;
            }
        }

        void UpdateBindings()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                var b = _bindings[i];
                if (b.source == null) continue;

                float raw = b.source();
                float value = Mathf.Lerp(b.min, b.max, Mathf.Clamp01(raw));

                if (!_renderers.TryGetValue(b.partId, out var r)) continue;

                switch (b.channel)
                {
                    case VisualChannel.Emission:
                        SetEmission(b.partId, value);
                        break;

                    case VisualChannel.ScaleY:
                        var sc = r.transform.localScale;
                        sc.y = value;
                        r.transform.localScale = sc;
                        break;

                    case VisualChannel.ColorAlpha:
                        if (TryGetRendererAndBlock(b.partId, out _, out var block))
                        {
                            Color c = r.material.color;
                            c.a = value;
                            block.SetColor("_BaseColor", c);
                            block.SetColor("_Color", c);
                            r.SetPropertyBlock(block);
                        }
                        break;

                    case VisualChannel.PositionY:
                        var pos = r.transform.localPosition;
                        pos.y = value;
                        r.transform.localPosition = pos;
                        break;

                    case VisualChannel.ColorTint:
                        // value 0-1 blends from base color to white
                        if (TryGetRendererAndBlock(b.partId, out _, out var tb))
                        {
                            Color baseC = r.material.color;
                            Color tinted = Color.Lerp(baseC, Color.white, value);
                            tb.SetColor("_BaseColor", tinted);
                            tb.SetColor("_Color", tinted);
                            r.SetPropertyBlock(tb);
                        }
                        break;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        bool TryGetRendererAndBlock(string partId, out Renderer r, out MaterialPropertyBlock block)
        {
            r = null;
            block = null;
            if (_renderers == null || !_renderers.TryGetValue(partId, out r)) return false;
            if (!_propBlocks.TryGetValue(partId, out block))
            {
                block = new MaterialPropertyBlock();
                _propBlocks[partId] = block;
            }
            return true;
        }
    }
}
