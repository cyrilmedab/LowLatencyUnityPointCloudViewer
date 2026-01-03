using PointCloudViewer.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace PointCloudViewer.DataLoading
{
    /// <summary>
    /// Loader for binary point cloud files.
    /// Format: [uint32 count][PointStruct * count]
    /// Optimized for fast loading with minimal allocations.
    /// </summary>
    public class BinaryPointCloudLoader : IPointCloudLoader
    {
        private const uint MaxPointCount = 50_000_000; // 50M points max

        #region Inherited Properties and Methods 
        
        /// <inheritdoc/>
        public string LoaderName => "Binary";

        /// <inheritdoc/>
        public string[] SupportedExtensions => new[] { ".bin" };

        /// <inheritdoc/>
        public bool CanLoad(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) { return false; }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            int ind = System.Array.IndexOf(SupportedExtensions, ext);
            return ind != -1;
        }

        /// <inheritdoc/>
        public PointCloudData Load(string filePath)
        {
            if (!ValidateFileExists(filePath)) { return null; }

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 65536, FileOptions.SequentialScan)) // 64KB
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    uint pointCount = reader.ReadUInt32();

                    if (!ValidatePointCount(pointCount)) { return null; }

                    ValidateFileSize(fs.Length, pointCount);

                    PointStruct[] points = ReadPointsSequential(reader, pointCount);

                    var data = new PointCloudData(points);
                    Debug.Log($"Loaded {data}");

                    return data;
                }                 
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load point cloud: {ex.Message}");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<PointCloudData> LoadAsync(string filePath)
        {
            if (!ValidateFileExists(filePath)) { return null; }

            try
            {
                // Read entire file into memory buffer for faster parsing (baseline approach)
                byte[] fileBytes = await Task.Run(() => File.ReadAllBytes(filePath));

                return await Task.Run(() =>
                {
                    using (MemoryStream ms = new MemoryStream(fileBytes))
                    using (BinaryReader reader = new BinaryReader(ms))
                    {
                        uint pointCount = reader.ReadUInt32();

                        if (!ValidatePointCount(pointCount))
                        {
                            throw new InvalidDataException($"Point count {pointCount:N0} exceeds maximum.");
                        }

                        ValidateFileSize(fileBytes.Length, pointCount);

                        PointStruct[] points = ReadPointsSequential(reader, pointCount);

                        var data = new PointCloudData(points);
                        Debug.Log($"Loaded async {data}");
                        return data;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load point cloud async: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Validates that the file exists at the given path.
        /// </summary>
        /// <param name="filePath">Path to validate.</param>
        /// <returns>True if file exists, false otherwise (logs error).</returns>
        private bool ValidateFileExists(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Point cloud file not found: {filePath}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validates that the point count is within acceptable limits.
        /// </summary>
        /// <param name="pointCount">Number of points to validate.</param>
        /// <returns>True if valid, false otherwise (logs error).</returns>
        private bool ValidatePointCount(uint pointCount)
        {
            if (pointCount > MaxPointCount)
            {
                Debug.LogError($"Point count {pointCount:N0} exceeds maximum allowed ({MaxPointCount:N0}).");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validates that the file size matches the expected size based on point count.
        /// </summary>
        /// <param name="actualSize">Actual file size in bytes.</param>
        /// <param name="pointCount">Number of points declared in file header.</param>
        /// <returns>True if size matches (warns if mismatch but doesn't fail).</returns>
        private bool ValidateFileSize(long actualSize, uint pointCount)
        {
            long expectedSize = 4 + (long)pointCount * PointStruct.STRIDE;
            if (actualSize != expectedSize)
            {
                Debug.LogWarning($"File size mismatch. Expected {expectedSize}, got {actualSize}. " +
                                 "File may be corrupted.");
                return false;
            }
            return true;
        }

        #endregion

        /// <summary>
        /// Reads point data from a BinaryReader into a PointStruct array.
        /// </summary>
        /// <param name="reader">BinaryReader positioned after the point count header.</param>
        /// <param name="pointCount">Number of points to read.</param>
        /// <returns>Array of points.</returns>
        private PointStruct[] ReadPointsSequential(BinaryReader reader, uint pointCount)
        {
            PointStruct[] points = new PointStruct[pointCount];

            for (int i = 0; i < points.Length; i++)
            {
                points[i].x = reader.ReadSingle();
                points[i].y = reader.ReadSingle();
                points[i].z = reader.ReadSingle();
                points[i].rgba = reader.ReadUInt32();
            }

            return points;
        }

        #region Faster, Unsafe Loading

        /// <summary>
        /// Fast load using unsafe memory copy.
        /// Uses direct buffer copy for maximum performance.
        /// </summary>
        /// <param name="filePath">Path to the binary point cloud file.</param>
        /// <returns>Loaded point cloud data, or null on failure.</returns>
        public unsafe PointCloudData LoadFast(string filePath)
        {
            if (!ValidateFileExists(filePath))
                return null;

            try
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);

                fixed (byte* pBytes = fileBytes)
                {
                    uint pointCount = *(uint*)pBytes;

                    if (!ValidatePointCount(pointCount))
                        return null;

                    ValidateFileSize(fileBytes.Length, pointCount);

                    PointStruct[] points = new PointStruct[pointCount];

                    fixed (PointStruct* pPoints = points)
                    {
                        byte* src = pBytes + 4; // Skip count header
                        byte* dst = (byte*)pPoints;
                        long byteCount = (long)pointCount * PointStruct.STRIDE;

                        Buffer.MemoryCopy(src, dst, byteCount, byteCount);
                    }

                    var data = new PointCloudData(points);
                    Debug.Log($"Fast loaded {data}");
                    return data;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to fast load point cloud: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Fast async load using unsafe memory copy (optimized variant).
        /// Eliminates MemoryStream overhead by processing byte array directly with pointers.
        /// Use for profiling comparison against LoadAsync().
        /// </summary>
        /// <param name="filePath">Path to the binary point cloud file.</param>
        /// <returns>Task that completes with loaded data, or null on failure.</returns>
        public async Task<PointCloudData> LoadAsyncFast(string filePath)
        {
            if (!ValidateFileExists(filePath))
                return null;

            try
            {
                // Read file on thread pool
                byte[] fileBytes = await Task.Run(() => File.ReadAllBytes(filePath));

                // Parse using unsafe pointers on thread pool
                return await Task.Run(() =>
                {
                    unsafe
                    {
                        fixed (byte* pBytes = fileBytes)
                        {
                            uint pointCount = *(uint*)pBytes;

                            if (!ValidatePointCount(pointCount))
                                throw new InvalidDataException($"Point count {pointCount:N0} exceeds maximum.");

                            ValidateFileSize(fileBytes.Length, pointCount);

                            PointStruct[] points = new PointStruct[pointCount];

                            fixed (PointStruct* pPoints = points)
                            {
                                byte* src = pBytes + 4; // Skip count header
                                byte* dst = (byte*)pPoints;
                                long byteCount = (long)pointCount * PointStruct.STRIDE;

                                Buffer.MemoryCopy(src, dst, byteCount, byteCount);
                            }

                            var data = new PointCloudData(points);
                            Debug.Log($"Fast loaded async {data}");
                            return data;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to fast load point cloud async: {ex.Message}");
                return null;
            }
        }

        #endregion

    }
}