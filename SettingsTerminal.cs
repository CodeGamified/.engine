//Copyright BitNaughts 2024-2026
//MIT License
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using CodeGamified.Quality;

namespace BitNaughtsAI.UI
{
    /// <summary>
    /// Terminal for adjusting game settings.
    /// Hybrid TUI+GUI approach with row-based layout for precise slider alignment.
    /// 
    /// Settings:
    /// - Time Scale: Simulation speed multiplier
    /// - Graphics Quality: Low/Medium/High/Ultra
    /// - Master Volume: Audio volume
    /// - Music Volume: Background music
    /// - SFX Volume: Sound effects
    /// </summary>
    public class SettingsTerminal : TerminalWindow
    {
        [Header("Settings State")]
        [SerializeField] private int graphicsQuality = 3; // 0=Low, 1=Medium, 2=High, 3=Ultra
        [SerializeField] private float masterVolume = 1f;
        [SerializeField] private float musicVolume = 0.7f;
        [SerializeField] private float sfxVolume = 1f;
        
        [Header("Debug")]
        [SerializeField] private bool debugVisibleSliders = true;
        
        // Navigation state
        private bool _isActive = false;
        private int _selectedParameter = 0; // 0=graphics, 1=master, 2=music, 3=sfx
        
        // Parameter ranges
        private readonly string[] _graphicsOptions = { "LOW", "MEDIUM", "HIGH", "ULTRA" };
        private const float MIN_VOLUME = 0f;
        private const float MAX_VOLUME = 1f;
        
        // Slider row mapping (parameter index -> row that has the slider)
        private Dictionary<int, TerminalRow> _sliderRowMap = new Dictionary<int, TerminalRow>();
        
        // Button setup tracking
        private bool _graphicsButtonsCreated = false;
        private bool _actionButtonsCreated = false;
        private TerminalRow _graphicsButtonRow = null;
        private TerminalRow _actionButtonRow = null;
        
        // Events
        public event Action OnSettingsClosed;
        
        // Row layout - using shared constants from TerminalStyle
        // Local aliases for settings-specific rows within content area
        // LEFT COLUMN: Settings controls
        private const int ROW_GRAPHICS_LABEL = 2;
        private const int ROW_GRAPHICS_BUTTONS = 3;
        private const int ROW_MASTER_LABEL = 5;
        private const int ROW_MASTER_SLIDER = 6;
        private const int ROW_MUSIC_LABEL = 8;
        private const int ROW_MUSIC_SLIDER = 9;
        private const int ROW_SFX_LABEL = 11;
        private const int ROW_SFX_SLIDER = 12;
        
        // RIGHT COLUMN: Info display (shows mesh quality details)
        private const int ROW_INFO_HEADER = 2;
        private const int ROW_INFO_EARTH = 3;
        private const int ROW_INFO_MOON = 4;
        private const int ROW_INFO_ATMOSPHERE = 5;
        private const int ROW_INFO_DIVIDER = 6;
        private const int ROW_INFO_TEXTURE = 7;
        private const int ROW_INFO_TRIS_LABEL = 9;
        private const int ROW_INFO_TRIS_VALUE = 10;
        
        protected override void Awake()
        {
            base.Awake();
            windowTitle = "SETTINGS";
            
            // Load saved settings
            LoadSettings();
        }
        
        protected override void OnInitialized()
        {
            // Use base class row layout initialization
            InitializeRowLayout(debugVisibleSliders);
            
            // Initialize dynamic columns with 55% left / 45% right split
            InitializeDynamicColumns(true, 0.55f);
        }
        
        /// <summary>
        /// Called after dynamic columns are calculated.
        /// </summary>
        protected override void OnColumnsInitialized()
        {
            Debug.Log($"[SettingsTerminal] Dynamic columns ready: {_totalChars} chars, divider at {_dividerPos}");
        }
        
        protected override void Update()
        {
            base.Update();
            
            if (_isActive)
            {
                HandleKeyboardInput();
                UpdateDisplay();
            }
        }
        
        /// <summary>
        /// Show the settings terminal.
        /// </summary>
        public void Show()
        {
            _isActive = true;
            _selectedParameter = 0;
            
            // Clear slider row map to force rewiring callbacks
            _sliderRowMap.Clear();
            
            // Reset button tracking for fresh setup
            _graphicsButtonsCreated = false;
            _actionButtonsCreated = false;
            
            gameObject.SetActive(true);
            Debug.Log("[SettingsTerminal] Opened");
        }
        
        /// <summary>
        /// Hide the settings terminal.
        /// </summary>
        public void Hide()
        {
            _isActive = false;
            
            // Save settings on close
            SaveSettings();
            
            gameObject.SetActive(false);
            
            // Fire event first - handler may open LaunchConfig if site is selected
            OnSettingsClosed?.Invoke();
            
            // Only return to main if no other modal was opened by the event handler
            if (TerminalUIManager.Instance?.ActiveModal == TerminalUIManager.ModalTerminal.None)
            {
                TerminalUIManager.Instance?.ShowMain();
            }
        }
        
        /// <summary>
        /// Force close without triggering ShowMain (called by TerminalUIManager).
        /// Note: Does NOT fire OnSettingsClosed to prevent loops during modal switching.
        /// </summary>
        public void ForceClose()
        {
            _isActive = false;
            SaveSettings(); // Still save on force close
            // Don't fire OnSettingsClosed here - that causes infinite loops when switching modals
            // Don't call ShowMain - let TerminalUIManager handle it
        }
        
        public bool IsActive => _isActive;
        
        /// <summary>
        /// Handle keyboard navigation.
        /// </summary>
        private void HandleKeyboardInput()
        {
            // Navigation: W/S or Up/Down
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                _selectedParameter = Mathf.Max(0, _selectedParameter - 1);
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                _selectedParameter = Mathf.Min(3, _selectedParameter + 1);
            }
            
            // Adjust: A/D or Left/Right
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                AdjustParameter(-1);
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                AdjustParameter(1);
            }
            
            // Close: Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }
        
        /// <summary>
        /// Adjust the currently selected parameter.
        /// </summary>
        private void AdjustParameter(int direction)
        {
            float volumeStep = 0.1f;
            
            switch (_selectedParameter)
            {
                case 0: // Graphics Quality
                    graphicsQuality = Mathf.Clamp(graphicsQuality + direction, 0, _graphicsOptions.Length - 1);
                    ApplyGraphicsQuality();
                    break;
                case 1: // Master Volume
                    masterVolume = Mathf.Clamp(masterVolume + direction * volumeStep, MIN_VOLUME, MAX_VOLUME);
                    ApplyVolume();
                    break;
                case 2: // Music Volume
                    musicVolume = Mathf.Clamp(musicVolume + direction * volumeStep, MIN_VOLUME, MAX_VOLUME);
                    ApplyVolume();
                    break;
                case 3: // SFX Volume
                    sfxVolume = Mathf.Clamp(sfxVolume + direction * volumeStep, MIN_VOLUME, MAX_VOLUME);
                    ApplyVolume();
                    break;
            }
        }
        
        /// <summary>
        /// Update the terminal display.
        /// </summary>
        private void UpdateDisplay()
        {
            if (_rows == null || _rows.Count < TerminalStyle.TOTAL_ROWS) return;
            if (!_columnsInitialized) return; // Wait for dynamic columns
            
            // Clear all rows first
            ClearAllRows();
            
            // ═══════════════════════════════════════════════════════════════
            // LEFT COLUMN: Settings Controls
            // ═══════════════════════════════════════════════════════════════
            
            // Graphics Quality
            string graphicsPrefix = _selectedParameter == 0 ? "<color=#FFAA00>></color>" : " ";
            _rows[ROW_GRAPHICS_LABEL].SetText($"{graphicsPrefix} <color=#44AAFF>GRAPHICS QUALITY:</color> {_graphicsOptions[graphicsQuality]}");
            SetupGraphicsButtonsOnRow(_rows[ROW_GRAPHICS_BUTTONS], ROW_GRAPHICS_BUTTONS);
            
            // Master Volume
            string masterPrefix = _selectedParameter == 1 ? "<color=#FFAA00>></color>" : " ";
            _rows[ROW_MASTER_LABEL].SetText($"{masterPrefix} <color=#44AAFF>MASTER VOLUME:</color> {(masterVolume * 100):F0}%");
            SetupSliderOnRow(_rows[ROW_MASTER_SLIDER], ROW_MASTER_SLIDER, 1, masterVolume, MIN_VOLUME, MAX_VOLUME, OnMasterVolumeChanged);
            
            // Music Volume
            string musicPrefix = _selectedParameter == 2 ? "<color=#FFAA00>></color>" : " ";
            _rows[ROW_MUSIC_LABEL].SetText($"{musicPrefix} <color=#44AAFF>MUSIC VOLUME:</color> {(musicVolume * 100):F0}%");
            SetupSliderOnRow(_rows[ROW_MUSIC_SLIDER], ROW_MUSIC_SLIDER, 2, musicVolume, MIN_VOLUME, MAX_VOLUME, OnMusicVolumeChanged);
            
            // SFX Volume
            string sfxPrefix = _selectedParameter == 3 ? "<color=#FFAA00>></color>" : " ";
            _rows[ROW_SFX_LABEL].SetText($"{sfxPrefix} <color=#44AAFF>SFX VOLUME:</color> {(sfxVolume * 100):F0}%");
            SetupSliderOnRow(_rows[ROW_SFX_SLIDER], ROW_SFX_SLIDER, 3, sfxVolume, MIN_VOLUME, MAX_VOLUME, OnSfxVolumeChanged);
            
            // ═══════════════════════════════════════════════════════════════
            // RIGHT COLUMN: Mesh Quality Info
            // ═══════════════════════════════════════════════════════════════
            UpdateRightColumnInfo();
            
            // Fixed footer (use dynamic separator width)
            _rows[TerminalStyle.ROW_SEPARATOR_BOT].SetText($"<color=#666666>{_separator}</color>");
            _rows[TerminalStyle.ROW_ACTIONS].SetText("    <color=#44FF44>[ESC]</color> CLOSE");
            SetupActionButtonsOnRow(TerminalStyle.ROW_ACTIONS);
            _rows[TerminalStyle.ROW_HINT].SetText("    <color=#666666>W/S: Navigate  A/D: Adjust</color>");
        }
        
        /// <summary>
        /// Update the right column with mesh quality information from QualityHints.
        /// </summary>
        private void UpdateRightColumnInfo()
        {
            var tier = (QualityTier)graphicsQuality;
            
            // Get segment counts from QualityHints
            int primarySegs = QualityHints.SphereSegments(tier, DetailRole.Primary);
            int secondarySegs = QualityHints.SphereSegments(tier, DetailRole.Secondary);
            int effectSegs = QualityHints.SphereSegments(tier, DetailRole.Effect);
            int textureRes = QualityHints.TextureResolution(tier);
            string texLabel = QualityHints.TextureLabel(tier);
            int totalTris = QualityHints.EstimatedTriangles(tier);
            
            // Build dynamic right-column divider
            string rightDivider = new string('─', Mathf.Max(1, _rightColWidth - 2));
            
            // Header
            _rows[ROW_INFO_HEADER].SetRightText("<color=#888888>│</color> <color=#FFAA00>MESH QUALITY</color>");
            
            // Segment counts by detail role
            _rows[ROW_INFO_EARTH].SetRightText($"<color=#888888>│</color> Primary:   <color=#44FF44>{primarySegs}</color> segments");
            _rows[ROW_INFO_MOON].SetRightText($"<color=#888888>│</color> Secondary: <color=#44FF44>{secondarySegs}</color> segments");
            _rows[ROW_INFO_ATMOSPHERE].SetRightText($"<color=#888888>│</color> Effects:   <color=#44FF44>{effectSegs}</color> segments");
            
            // Divider (dynamic width)
            _rows[ROW_INFO_DIVIDER].SetRightText($"<color=#888888>│ {rightDivider}</color>");
            
            // Texture resolution
            _rows[ROW_INFO_TEXTURE].SetRightText($"<color=#888888>│</color> Textures: <color=#44AAFF>{texLabel}</color> ({textureRes}px)");
            
            // Triangle count summary
            _rows[ROW_INFO_TRIS_LABEL].SetRightText("<color=#888888>│</color> <color=#666666>Estimated tris:</color>");
            _rows[ROW_INFO_TRIS_VALUE].SetRightText($"<color=#888888>│</color> <color=#44AAFF>{totalTris:N0}</color>");
        }
        
        /// <summary>
        /// Setup graphics quality buttons on a row with actual button overlays.
        /// </summary>
        private void SetupGraphicsButtonsOnRow(TerminalRow row, int rowIndex)
        {
            // Build the text with button placeholders
            // Format: "    [1] LOW  [2] MED  [3] HIGH [4] ULTR"
            // Each button is 9 chars: "[#] XXXX " (bracket + number + bracket + space + 4char name + space)
            const int BUTTON_WIDTH_CHARS = 9;
            var sb = new System.Text.StringBuilder();
            sb.Append("    ");
            
            for (int i = 0; i < _graphicsOptions.Length; i++)
            {
                string optionShort = _graphicsOptions[i].Substring(0, Mathf.Min(4, _graphicsOptions[i].Length));
                bool selected = (i == graphicsQuality);
                string marker = selected ? "*" : (i + 1).ToString();
                string color = selected ? "#44FF44" : "#888888";
                sb.Append($"<color={color}>[{marker}]</color> {optionShort} ");
            }
            
            row.SetText(sb.ToString());
            
            // Create button overlays (only once)
            if (!_graphicsButtonsCreated || _graphicsButtonRow != row)
            {
                _graphicsButtonRow = row;
                
                int[] startChars = new int[_graphicsOptions.Length];
                int[] widthChars = new int[_graphicsOptions.Length];
                System.Action<int>[] callbacks = new System.Action<int>[_graphicsOptions.Length];
                
                int charPos = 4; // Start after "    "
                for (int i = 0; i < _graphicsOptions.Length; i++)
                {
                    startChars[i] = charPos;
                    widthChars[i] = BUTTON_WIDTH_CHARS;
                    
                    int capturedIndex = i;
                    callbacks[i] = (idx) => SelectGraphicsQuality(capturedIndex);
                    
                    charPos += BUTTON_WIDTH_CHARS;
                }
                
                row.CreateButtonOverlays(startChars, widthChars, callbacks);
                _graphicsButtonsCreated = true;
            }
            
            // Update highlight to show selected option
            row.UpdateButtonHighlight(graphicsQuality);
        }
        
        /// <summary>
        /// Select a graphics quality option (called from button click).
        /// </summary>
        private void SelectGraphicsQuality(int index)
        {
            graphicsQuality = Mathf.Clamp(index, 0, _graphicsOptions.Length - 1);
            ApplyGraphicsQuality();
        }
        
        /// <summary>
        /// Setup action button overlay for close button.
        /// </summary>
        private void SetupActionButtonsOnRow(int rowIdx)
        {
            if (rowIdx < 0 || rowIdx >= _rows.Count) return;
            var row = _rows[rowIdx];
            if (row == null) return;
            
            // Create button overlay for [ESC] CLOSE (only once)
            if (!_actionButtonsCreated || _actionButtonRow != row)
            {
                _actionButtonRow = row;
                
                // "    [ESC] CLOSE" - button starts at char 4, width ~11 chars
                int[] startChars = new int[] { 4 };
                int[] widthChars = new int[] { 11 };
                System.Action<int>[] callbacks = new System.Action<int>[]
                {
                    (idx) => Hide()
                };
                
                row.CreateButtonOverlays(startChars, widthChars, callbacks);
                _actionButtonsCreated = true;
            }
        }
        
        /// <summary>
        /// Setup a slider overlay on a row for volume control.
        /// </summary>
        private void SetupSliderOnRow(TerminalRow row, int rowIndex, int paramIndex, float value, float min, float max, UnityEngine.Events.UnityAction<float> callback)
        {
            // Build progress bar text
            float normalized = Mathf.InverseLerp(min, max, value);
            string progressBar = TextAnimUtils.GetProgressBar(normalized, 22);
            row.SetText($"    {progressBar}");
            
            // Create or get slider
            if (!_sliderRowMap.ContainsKey(paramIndex))
            {
                var slider = row.CreateSliderOverlay(4, 22); // Start at char 4, width 22
                slider.minValue = min;
                slider.maxValue = max;
                slider.interactable = true;
                slider.onValueChanged.AddListener(callback);
                _sliderRowMap[paramIndex] = row;
            }
            
            // Update slider value (avoid triggering callback)
            var existingSlider = _sliderRowMap[paramIndex].Slider;
            if (existingSlider != null && Mathf.Abs(existingSlider.value - value) > 0.001f)
            {
                existingSlider.SetValueWithoutNotify(value);
            }
            
            row.SetSliderVisible(true);
        }
        
        // Slider callbacks
        private void OnMasterVolumeChanged(float value)
        {
            if (!_isActive) return;
            masterVolume = value;
            ApplyVolume();
        }
        
        private void OnMusicVolumeChanged(float value)
        {
            if (!_isActive) return;
            musicVolume = value;
            ApplyVolume();
        }
        
        private void OnSfxVolumeChanged(float value)
        {
            if (!_isActive) return;
            sfxVolume = value;
            ApplyVolume();
        }
        
        /// <summary>
        /// Apply graphics quality setting via QualityBridge.
        /// All IQualityResponsive components receive the change automatically.
        /// </summary>
        private void ApplyGraphicsQuality()
        {
            QualityBridge.SetTier((QualityTier)graphicsQuality);
            Debug.Log($"[Settings] Graphics quality set to: {_graphicsOptions[graphicsQuality]}");
        }
        
        /// <summary>
        /// Apply volume settings.
        /// </summary>
        private void ApplyVolume()
        {
            AudioListener.volume = masterVolume;
            // TODO: Wire to actual audio mixer groups when audio system is implemented
            Debug.Log($"[Settings] Volume: Master={masterVolume:F1}, Music={musicVolume:F1}, SFX={sfxVolume:F1}");
        }
        
        /// <summary>
        /// Save settings to PlayerPrefs.
        /// </summary>
        private void SaveSettings()
        {
            PlayerPrefs.SetInt("GraphicsQuality", graphicsQuality);
            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            PlayerPrefs.SetFloat("MusicVolume", musicVolume);
            PlayerPrefs.SetFloat("SfxVolume", sfxVolume);
            PlayerPrefs.Save();
            Debug.Log("[Settings] Settings saved");
        }
        
        /// <summary>
        /// Load settings from PlayerPrefs.
        /// </summary>
        private void LoadSettings()
        {
            graphicsQuality = PlayerPrefs.GetInt("GraphicsQuality", 3);
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);
            
            // Apply loaded settings
            ApplyGraphicsQuality();
            ApplyVolume();
            Debug.Log("[Settings] Settings loaded");
        }
    }
}
