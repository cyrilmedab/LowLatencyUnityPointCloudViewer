using System.Threading.Tasks;
using PointCloudViewer.Core;

namespace PointCloudViewer.DataLoading
{
    /// <summary>
    /// Interface for loading point cloud data from various sources.
    /// Supports both synchronous and asynchronous loading patterns.
    /// </summary>
    public interface IPointCloudLoader
    {
        /// <summary>Human-readable name of the loader (e.g., "Binary", "PLY").</summary>
        string LoaderName { get; }

        /// <summary>File extensions this loader supports (e.g., ".bin", ".ply").</summary>
        string[] SupportedExtensions { get; }

        /// <summary>Check if this loader can handle the given file path.</summary>
        bool CanLoad(string filePath);

        /// <summary>
        /// Load point cloud data synchronously.
        /// Use for small files or when blocking is acceptable.
        /// </summary>
        /// <param name="filePath">Path to the point cloud file.</param>
        /// <returns>Loaded point cloud data, or null on failure.</returns>
        PointCloudData Load(string filePath);

        /// <summary>
        /// Load point cloud data asynchronously.
        /// Preferred for large files to avoid main thread blocking.
        /// </summary>
        /// <param name="filePath">Path to the point cloud file.</param>
        /// <returns>Task that completes with loaded data, or null on failure.</returns>
        Task<PointCloudData> LoadAsync(string filePath);
    }
}
