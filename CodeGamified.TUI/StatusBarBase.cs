// ═══════════════════════════════════════════════════════════
//  StatusBarBase — Shared status bar with triple-column row
//  Subclass in each game to provide game-specific data
// ═══════════════════════════════════════════════════════════
using UnityEngine;
using TMPro;
using System;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Abstract base for a persistent top status bar.
    /// Creates a single TerminalRow in triple-column mode (left/center/right).
    /// Subclass and override BuildLeftSection(), BuildCenterSection(),
    /// BuildRightSection() to provide game-specific content.
    /// </summary>
    public abstract class StatusBarBase : MonoBehaviour
    {
        [SerializeField] protected TMP_Text seedText;
        [SerializeField] protected float updateInterval = 0.1f;
        [SerializeField] protected float columnPadding = 10f;
        [SerializeField] protected bool debugVisibleButtons;

        protected TerminalRow row;
        protected bool rowInitialized;
        protected float age;
        float nextUpdate;

        // ── Lifecycle ───────────────────────────────────────────

        protected virtual void Awake() { }

        protected virtual void Start()
        {
            Invoke(nameof(InitializeRow), 0.1f);
        }

        protected virtual void Update()
        {
            age += Time.deltaTime;
            if (Time.time < nextUpdate) return;
            nextUpdate = Time.time + updateInterval;
            Refresh();
        }

        // ── Initialization ──────────────────────────────────────

        /// <summary>
        /// Programmatic setup — creates the bar TMP_Text if not wired via Inspector.
        /// Call before Start().
        /// </summary>
        public void InitializeProgrammatic(TMP_FontAsset font, float fontSize)
        {
            if (seedText != null) return;

            var textGO = new GameObject("BarText");
            textGO.transform.SetParent(transform, false);

            var rt = textGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            seedText = textGO.AddComponent<TextMeshProUGUI>();
            if (font != null) seedText.font = font;
            seedText.fontSize = fontSize;
            seedText.alignment = TextAlignmentOptions.Left;
            seedText.enableWordWrapping = false;
            seedText.overflowMode = TextOverflowModes.Overflow;
            seedText.richText = true;
            seedText.color = Color.white;
            seedText.raycastTarget = false;
        }

        void InitializeRow()
        {
            if (rowInitialized) return;

            TMP_FontAsset font = seedText?.font;
            float fontSize = seedText?.fontSize ?? 11f;

            if (font == null)
            {
                font = Resources.Load<TMP_FontAsset>("Fonts/Unifont SDF");
            }

            if (font == null)
            {
                Debug.LogWarning("[StatusBarBase] No font available — cannot initialize row");
                return;
            }

            if (seedText != null)
                seedText.gameObject.SetActive(false);

            row = TerminalRow.Create(transform, font, fontSize, 0, debugVisibleButtons);
            row.SetTripleColumnMode(true, columnPadding);

            rowInitialized = true;
            OnRowInitialized();
        }

        /// <summary>Called after the triple-column row is ready. Override to create buttons etc.</summary>
        protected virtual void OnRowInitialized() { }

        // ── Refresh ─────────────────────────────────────────────

        void Refresh()
        {
            if (!rowInitialized || row == null) return;
            row.SetTripleTexts(
                BuildLeftSection(),
                BuildCenterSection(),
                BuildRightSection());
        }

        // ── Abstract content ────────────────────────────────────

        /// <summary>Left-justified status bar content (e.g., game title, ship name).</summary>
        protected abstract string BuildLeftSection();

        /// <summary>Center-justified status bar content (e.g., time, wind).</summary>
        protected abstract string BuildCenterSection();

        /// <summary>Right-justified status bar content (e.g., clock, metrics).</summary>
        protected abstract string BuildRightSection();
    }
}
