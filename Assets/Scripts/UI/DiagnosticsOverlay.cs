using UnityEngine;
using PointCloudViewer.Rendering;
using System.Text;

namespace PointCloudViewer.UI
{
    /// <summary>
    /// IMGUI-based diagnostics overlay for performance metrics.
    /// Updates display at configurable rate to reduce visual noise.
    /// </summary>
    public class DiagnosticsOverlay : MonoBehaviour
    {
        #region Serialized Member Variables

        [Header("Display Settings")]
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private float _updateRate = 10f; // Hz
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

        [Header("Layout")]
        [SerializeField] private Vector2 _position = new Vector2(10, 10);
        [SerializeField] private float _panelWidth = 320f;
        [SerializeField] private float _panelHeight = 260f;

        [Header("Styling")]
        [SerializeField] private int _fontSize = 14;
        [SerializeField] private Color _backgroundColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _warningColor = Color.yellow;
        [SerializeField] private Color _criticalColor = Color.red;

        #endregion

        #region Private Member Variables

        // Cached stats for display (updated at refresh rate)
        private RenderStats _displayStats;
        private float _lastUpdateTime;
        private float _updateInterval;

        // GUI styles - pre-cached to avoid per-frame allocations
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _valueStyle;

        // Pre-cached colored value styles
        private GUIStyle _greenValueStyle;
        private GUIStyle _yellowValueStyle;
        private GUIStyle _redValueStyle;
        private GUIStyle _cyanValueStyle;
        private GUIStyle _lightBlueValueStyle;
        private GUIStyle _controlsHintStyle;

        private Texture2D _backgroundTexture;
        private bool _stylesInitialized;

        // External stats source
        private System.Func<RenderStats> _statsProvider;

        // Cached formatted strings to avoid per-frame allocations
        private string _cachedFps;
        private string _cachedFrameTime;
        private string _cachedCpuPrep;
        private string _cachedRenderer;
        private string _cachedPoints;
        private string _cachedMemory;
        private string _cachedCulled;

        // StringBuilder for string formatting (reused to avoid allocations)
        private StringBuilder _stringBuilder;

        // Cached GUIContent to avoid allocations
        private GUIContent _emptyContent;
        private GUIContent _headerContent;
        private GUIContent _controlsContent;

        // Cached rects to avoid allocations
        private Rect _panelRect;
        private Rect _contentRect;

        #endregion

        /// <summary>Gets or sets whether the overlay is visible.</summary>
        public bool ShowOverlay
        {
            get => _showOverlay;
            set => _showOverlay = value;
        }

        private void Awake()
        {
            // Pre-allocate string builder with reasonable capacity
            _stringBuilder = new StringBuilder(64);

            // Pre-allocate GUIContent objects
            _emptyContent = new GUIContent();
            _headerContent = new GUIContent("DIAGNOSTICS");
            _controlsContent = new GUIContent("F1: Toggle | R: Renderer | C: Culling | D: Decimation");

            // Initialize cached strings
            _cachedFps = "0.0";
            _cachedFrameTime = "0.00 ms";
            _cachedCpuPrep = "0.00 ms";
            _cachedRenderer = "None";
            _cachedPoints = "0 / 0";
            _cachedMemory = "0.0 MB";
            _cachedCulled = "0.0%";
        }

        private void Start()
        {
            // Clamp update rate to prevent division by zero
            _updateRate = Mathf.Max(0.1f, _updateRate);
            _updateInterval = 1f / _updateRate;

            // Validate panel dimensions
            _panelWidth = Mathf.Max(200f, _panelWidth);
            _panelHeight = Mathf.Max(100f, _panelHeight);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _showOverlay = !_showOverlay;
            }

            // Fetch stats from provider if available
            if (_statsProvider != null && Time.time - _lastUpdateTime >= _updateInterval)
            {
                _displayStats = _statsProvider();
                UpdateCachedStrings();
                _lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Set the function that provides render stats each frame.
        /// </summary>
        /// <param name="provider">Function that returns current render statistics.</param>
        public void SetStatsProvider(System.Func<RenderStats> provider) => _statsProvider = provider;

        /// <summary>
        /// Manually update stats. Alternative to using SetStatsProvider.
        /// </summary>
        /// <param name="stats">Current render statistics to display.</param>
        public void UpdateStats(RenderStats stats)
        {
            if (Time.time - _lastUpdateTime >= _updateInterval)
            {
                _displayStats = stats;
                UpdateCachedStrings();
                _lastUpdateTime = Time.time;
            }
        }

        /// <summary>Update all cached formatted strings. Called only when stats change.</summary>
        private void UpdateCachedStrings()
        {
            // Use StringBuilder to avoid intermediate string allocations
            _stringBuilder.Clear();
            _stringBuilder.AppendFormat("{0:F1}", _displayStats.Fps);
            _cachedFps = _stringBuilder.ToString();

            _stringBuilder.Clear();
            _stringBuilder.AppendFormat("{0:F2} ms", _displayStats.FrameTimeMs);
            _cachedFrameTime = _stringBuilder.ToString();

            _stringBuilder.Clear();
            _stringBuilder.AppendFormat("{0:F2} ms", _displayStats.CpuPrepTimeMs);
            _cachedCpuPrep = _stringBuilder.ToString();

            _cachedRenderer = _displayStats.RendererName ?? "None";

            _stringBuilder.Clear();
            _stringBuilder.AppendFormat("{0:N0} / {1:N0}", _displayStats.RenderedPoints, _displayStats.TotalPoints);
            _cachedPoints = _stringBuilder.ToString();

            _stringBuilder.Clear();
            _stringBuilder.AppendFormat("{0:F1} MB", _displayStats.MemoryUsedMB);
            _cachedMemory = _stringBuilder.ToString();

            _stringBuilder.Clear();
            _stringBuilder.AppendFormat("{0:F1}%", _displayStats.CullPercentage);
            _cachedCulled = _stringBuilder.ToString();
        }

        private void InitializeStyles()
        {
            // Background texture
            _backgroundTexture = new Texture2D(1, 1);
            _backgroundTexture.SetPixel(0, 0, _backgroundColor);
            _backgroundTexture.Apply();

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _backgroundTexture },
                padding = new RectOffset(10, 10, 10, 10)
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                normal = { textColor = _textColor }
            };

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold,
                fontSize = _fontSize + 2
            };

            _valueStyle = new GUIStyle(_labelStyle)
            {
                alignment = TextAnchor.MiddleRight
            };

            // Pre-create all colored value styles to avoid per-frame allocations
            _greenValueStyle = new GUIStyle(_valueStyle)
            {
                normal = { textColor = Color.green }
            };

            _yellowValueStyle = new GUIStyle(_valueStyle)
            {
                normal = { textColor = _warningColor }
            };

            _redValueStyle = new GUIStyle(_valueStyle)
            {
                normal = { textColor = _criticalColor }
            };

            _cyanValueStyle = new GUIStyle(_valueStyle)
            {
                normal = { textColor = Color.cyan }
            };

            _lightBlueValueStyle = new GUIStyle(_valueStyle)
            {
                normal = { textColor = new Color(0.5f, 0.8f, 1f) }
            };

            _controlsHintStyle = new GUIStyle(_labelStyle)
            {
                fontSize = _fontSize - 2,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!_showOverlay) { return; }

            // Inline style initialization check to avoid function call overhead
            if (!_stylesInitialized) { InitializeStyles(); }

            // Cache rects to avoid allocations
            _panelRect.x = _position.x;
            _panelRect.y = _position.y;
            _panelRect.width = _panelWidth;
            _panelRect.height = _panelHeight;

            GUI.Box(_panelRect, _emptyContent, _boxStyle);

            _contentRect.x = _panelRect.x + 10;
            _contentRect.y = _panelRect.y + 10;
            _contentRect.width = _panelRect.width - 20;
            _contentRect.height = _panelRect.height - 20;

            GUILayout.BeginArea(_contentRect);

            // Header
            GUILayout.Label(_headerContent, _headerStyle);
            GUILayout.Space(5);

            // Performance section - use cached strings and styles
            DrawMetricRowCached("FPS", _cachedFps, GetFpsStyle(_displayStats.Fps));
            DrawMetricRowCached("Frame Time", _cachedFrameTime, GetFrameTimeStyle(_displayStats.FrameTimeMs));
            DrawMetricRowCached("CPU Prep", _cachedCpuPrep, _valueStyle);

            GUILayout.Space(10);

            // Renderer section - use cached strings
            DrawMetricRowCached("Renderer", _cachedRenderer, _valueStyle);
            DrawMetricRowCached("Points", _cachedPoints, _valueStyle);
            DrawMetricRowCached("Memory", _cachedMemory, _valueStyle);

            if (_displayStats.OptimizationsEnabled)
            {
                GUILayout.Space(5);
                DrawMetricRowCached("Culled", _cachedCulled, _cyanValueStyle);

                /*
                // Display detailed optimization breakdown
                if (_displayStats.ActiveOptimizations != null && _displayStats.ActiveOptimizations.Length > 0)
                {
                    // Use StringBuilder to avoid string allocations
                    foreach (var opt in _displayStats.ActiveOptimizations)
                    {
                        if (opt.IsActive)
                        {
                            _stringBuilder.Clear();
                            _stringBuilder.Append("  ");
                            _stringBuilder.Append(opt.Name);
                            string optLabel = _stringBuilder.ToString();

                            _stringBuilder.Clear();
                            _stringBuilder.AppendFormat("{0:F1}% ({1:F2}ms)", opt.RemovalPercentage, opt.ProcessTimeMs);
                            string optValue = _stringBuilder.ToString();

                            DrawMetricRowCached(optLabel, optValue, _lightBlueValueStyle);
                        }
                    }
                }
                */
            }

            GUILayout.Space(10);

            // Controls hint - use cached GUIContent
            GUILayout.Label(_controlsContent, _controlsHintStyle);

            GUILayout.EndArea();
        }

        /// <summary>Optimized metric row drawing using pre-cached styles.</summary>
        private void DrawMetricRowCached(string label, string value, GUIStyle valueStyle)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(100));
            GUILayout.Label(value, valueStyle);
            GUILayout.EndHorizontal();
        }

        /// <summary>Returns pre-cached style based on FPS value.</summary>
        private GUIStyle GetFpsStyle(float fps)
        {
            if (fps >= 55f) { return _greenValueStyle; }
            if (fps >= 30f) { return _yellowValueStyle; }
            return _redValueStyle;
        }

        /// <summary>Returns pre-cached style based on frame time value.</summary>
        private GUIStyle GetFrameTimeStyle(float ms)
        {
            if (ms <= 18f) { return _greenValueStyle; }
            if (ms <= 33f) { return _yellowValueStyle; }
            return _redValueStyle;
        }

        private void OnDestroy()
        {
            if (_backgroundTexture != null)
            {
                if (Application.isPlaying)
                    Destroy(_backgroundTexture);
                else
                    DestroyImmediate(_backgroundTexture);
            }
        }


    }
}