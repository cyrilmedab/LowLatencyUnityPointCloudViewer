namespace PointCloudViewer.Optimization
{
    /// <summary>
    /// Statistics from optimization passes (culling, decimation).
    /// Designed as struct for zero-allocation usage.
    /// </summary>
    public struct OptimizationStats
    {
        /// <summary>Name of the optimization technique.</summary>
        public string Name;

        /// <summary>Points before optimization.</summary>
        public int InputCount;

        /// <summary>Points after optimization.</summary>
        public int OutputCount;

        /// <summary>Points removed by this optimization.</summary>
        public int RemovedCount => InputCount - OutputCount;

        /// <summary>Percentage of points removed.</summary>
        public float RemovalPercentage => InputCount > 0 ? (RemovedCount / (float)InputCount) * 100f : 0f;

        /// <summary>Time spent on this optimization (ms).</summary>
        public float ProcessTimeMs;

        /// <summary>Whether the optimization is currently active.</summary>
        public bool IsActive;

        public override string ToString()
        {
            return $"[{Name}] {OutputCount:N0}/{InputCount:N0} ({RemovalPercentage:F1}% removed) in {ProcessTimeMs:F2}ms";
        }

        /// <summary>Create stats for a pass-through (no optimization).</summary>
        public static OptimizationStats PassThrough(string name, int count)
        {
            return new OptimizationStats
            {
                Name = name,
                InputCount = count,
                OutputCount = count,
                ProcessTimeMs = 0,
                IsActive = false
            };
        }

        /// <summary>Create stats for an active optimization pass.</summary>
        public static OptimizationStats CreateActive(string name, int inputCount, int outputCount, float processTimeMs)
        {
            return new OptimizationStats
            {
                Name = name,
                InputCount = inputCount,
                OutputCount = outputCount,
                ProcessTimeMs = processTimeMs,
                IsActive = true
            };
        }
    }
}
