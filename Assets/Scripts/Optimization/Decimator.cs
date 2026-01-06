using System.Diagnostics;
using UnityEngine;
using PointCloudViewer.Core;

namespace PointCloudViewer.Optimization
{
    /// <summary>
    /// Stride-based point decimation for performance control.
    /// Simple but effective way to reduce point count.
    /// </summary>
    public class Decimator : MonoBehaviour, IOptimization
    {
        #region Serialized Member Variables

        [Header("Settings")]
        [SerializeField] private bool _enabled = true;

        [Header("Decimation")]
        [SerializeField, Range(1, 100)]
        private int _stride = 1; // 1 = no decimation, 2 = half points, etc.

        [SerializeField, Range(0f, 1f)]
        private float _ratio = 1f; // Alternative: 0-1 ratio (1 = all points)

        [SerializeField] private DecimationMode _mode = DecimationMode.Stride;

        [Header("Adaptive")]
        [SerializeField] private bool _adaptiveMode = false;
        [SerializeField] private float _targetFps = 60f;
        [SerializeField] private float _adaptSpeed = 0.1f;

        #endregion

        #region Private Member Variables

        // Stats
        private OptimizationStats _stats;
        private Stopwatch _timer;

        // Adaptive state
        private float _adaptiveRatio = 1f;

        // Cached RNG for random decimation mode (avoids per-frame allocation)
        private System.Random _rng;

        #endregion

        #region Public Member Variables And Properties

        /// <inheritdoc/>
        public string OptimizationName => "Decimation";

        /// <inheritdoc/>
        public bool IsEnabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>Gets or sets the decimation stride (1 = no decimation, 2 = half points, etc.).</summary>
        public int Stride
        {
            get => _stride;
            set => _stride = Mathf.Max(1, value);
        }

        /// <summary>Gets or sets the decimation ratio (0-1, where 1 = all points).</summary>
        public float Ratio
        {
            get => _ratio;
            set => _ratio = Mathf.Clamp01(value);
        }

        /// <summary>Decimation algorithm modes.</summary>
        public enum DecimationMode
        {
            /// <summary>Select every Nth point.</summary>
            Stride,
            /// <summary>Select a percentage of points.</summary>
            Ratio,
            /// <summary>Random sampling with deterministic seed per frame.</summary>
            Random
        }

        #endregion

        private void Awake()
        {
            _timer = new Stopwatch();
            _rng = new System.Random();
        }

        public OptimizationStats Process(PointCloudData data, int[] indices, ref int count)
        {
            // Early out if no decimation would occur
            bool shouldSkip = !_enabled
                || (_mode == DecimationMode.Stride && _stride <= 1)
                || (_mode == DecimationMode.Ratio && _ratio >= 1f)
                || (_mode == DecimationMode.Random && _ratio >= 1f);

            if (shouldSkip)
            {
                _stats = OptimizationStats.PassThrough(OptimizationName, count);
                return _stats;
            }

            _timer.Restart();

            int inputCount = count;

            // Update adaptive ratio if enabled
            if (_adaptiveMode) { UpdateAdaptiveRatio(); }

            // Apply decimation based on mode
            switch (_mode)
            {
                case DecimationMode.Stride:
                    ApplyStrideDecimation(data, indices, ref count);
                    break;
                case DecimationMode.Ratio:
                    ApplyRatioDecimation(data, indices, ref count);
                    break;
                case DecimationMode.Random:
                    ApplyRandomDecimation(data, indices, ref count);
                    break;
            }

            _timer.Stop();

            _stats = new OptimizationStats
            {
                Name = OptimizationName,
                InputCount = inputCount,
                OutputCount = count,
                ProcessTimeMs = (float)_timer.Elapsed.TotalMilliseconds,
                IsActive = true
            };

            return _stats;
        }

        private void ApplyStrideDecimation(PointCloudData data, int[] indices, ref int count)
        {
            int writeIndex = 0;
            int stride = _stride;

            if (count == data.PointCount)
            {
                // Fresh pass - direct indexing
                for (int i = 0; i < data.PointCount; i += stride)
                {
                    indices[writeIndex++] = i;
                }
            }
            else
            {
                // Stacked pass - from existing indices
                for (int i = 0; i < count; i += stride)
                {
                    indices[writeIndex++] = indices[i];
                }
            }

            count = writeIndex;
        }

        /// <summary>Get the effective decimation ratio (adaptive or manual).</summary>
        private float GetEffectiveRatio() => _adaptiveMode ? _adaptiveRatio : _ratio;

        private void ApplyRatioDecimation(PointCloudData data, int[] indices, ref int count)
        {
            float ratio = GetEffectiveRatio();
            int targetCount = Mathf.Max(1, Mathf.RoundToInt(count * ratio));

            if (targetCount >= count)
            {
                return; // No decimation needed
            }

            int writeIndex = 0;

            if (count == data.PointCount)
            {
                // Fresh pass - use integer math to avoid floating-point accumulation error
                for (int i = 0; i < targetCount; i++)
                {
                    int sourceIndex = GetSourceIndexForRatio(i, data.PointCount, targetCount);
                    indices[writeIndex++] = sourceIndex;
                }
            }
            else
            {
                // Stacked pass - work on previously filtered indices
                for (int i = 0; i < targetCount; i++)
                {
                    int readIndex = GetSourceIndexForRatio(i, count, targetCount);
                    indices[writeIndex++] = indices[readIndex];
                }
            }

            count = writeIndex;
        }

        private void ApplyRandomDecimation(PointCloudData data, int[] indices, ref int count)
        {
            float ratio = GetEffectiveRatio();
            int targetCount = Mathf.Max(1, Mathf.RoundToInt(count * ratio));

            if (targetCount >= count) { return; }

            // Re-seed cached RNG for deterministic behavior based on frame
            _rng = new System.Random(Time.frameCount);

            // Fisher-Yates shuffle partial (only shuffle first targetCount elements)
            for (int i = 0; i < targetCount; i++)
            {
                int j = _rng.Next(i, count);
                // Swap
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            count = targetCount;
        }

        private void UpdateAdaptiveRatio()
        {
            float currentFps = 1f / Time.deltaTime;
            float fpsError = (_targetFps - currentFps) / _targetFps;

            // Adjust ratio based on FPS difference
            if (fpsError > 0.1f) // FPS too low, reduce points
            {
                _adaptiveRatio -= _adaptSpeed * fpsError * Time.deltaTime;
            }
            else if (fpsError < -0.1f) // FPS higher than needed, can show more
            {
                _adaptiveRatio += _adaptSpeed * -fpsError * Time.deltaTime;
            }

            _adaptiveRatio = Mathf.Clamp(_adaptiveRatio, 0.01f, 1f);
        }

        /// <summary>
        /// Calculate source index from step using integer arithmetic to avoid accumulation error.
        /// </summary>
        /// <param name="outputIndex">Current output index (0 to targetCount-1)</param>
        /// <param name="inputCount">Total input count</param>
        /// <param name="targetCount">Target output count</param>
        /// <returns>Corresponding input index</returns>
        private int GetSourceIndexForRatio(int outputIndex, int inputCount, int targetCount)
        {
            // Use integer math: sourceIndex = (outputIndex * inputCount) / targetCount
            // This avoids accumulation error from repeated float addition
            long numerator = (long)outputIndex * inputCount;
            return (int)(numerator / targetCount);
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _adaptiveRatio = 1f;
            _stats = new OptimizationStats { Name = OptimizationName };
        }

        /// <inheritdoc/>
        public OptimizationStats GetStats() => _stats;

        /// <summary>
        /// Set decimation to show approximately N points.
        /// Automatically calculates appropriate stride and ratio.
        /// </summary>
        /// <param name="totalPoints">Total points in the cloud.</param>
        /// <param name="targetPoints">Desired number of visible points.</param>
        public void SetTargetPointCount(int totalPoints, int targetPoints)
        {
            if (targetPoints >= totalPoints)
            {
                _stride = 1;
                _ratio = 1f;
            }
            else
            {
                _stride = Mathf.Max(1, totalPoints / targetPoints);
                _ratio = (float)targetPoints / totalPoints;
            }
        }
    }
}
