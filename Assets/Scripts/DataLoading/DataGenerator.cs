using PointCloudViewer.Core;
using System.IO;
using UnityEngine;
using System;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PointCloudViewer.DataLoading
{
    /// <summary>
    /// Editor tool for generating synthetic point cloud data.
    /// Creates sphere-shaped point clouds at various densities.
    /// </summary>
    public static class DataGenerator
    {
        private const string DefaultOutputPath = "Assets/Data/SamplePointClouds";

        /// <summary>
        /// Generate a sphere point cloud with uniform distribution.
        /// </summary>
        /// <param name="pointCount">Number of points to generate.</param>
        /// <param name="radius">Sphere radius.</param>
        /// <param name="center">Sphere center position.</param>
        /// <returns>Array of generated points.</returns>
        public static PointStruct[] GenerateSphere(int pointCount, float radius = 1f, Vector3 center = default)
        {
            PointStruct[] points = new PointStruct[pointCount];

            for (int i = 0; i < pointCount; i++)
            {
                // Fibonacci sphere distribution for even coverage
                float phi = Mathf.Acos(1f - 2f * (i + 0.5f) / pointCount);
                float theta = Mathf.PI * (1f + Mathf.Sqrt(5f)) * i;

                float x = Mathf.Cos(theta) * Mathf.Sin(phi);
                float y = Mathf.Sin(theta) * Mathf.Sin(phi);
                float z = Mathf.Cos(phi);

                Vector3 position = center + new Vector3(x, y, z) * radius;

                // Color based on position (gradient visualization)
                Color32 color = new Color32(
                    (byte)((x * 0.5f + 0.5f) * 255),
                    (byte)((y * 0.5f + 0.5f) * 255),
                    (byte)((z * 0.5f + 0.5f) * 255),
                    255
                );

                points[i] = new PointStruct(position, color);
            }

            return points;
        }

        /// <summary>
        /// Generate a noisy sphere with random depth variation.
        /// More realistic for testing rendering performance.
        /// </summary>
        /// <param name="pointCount">Number of points to generate.</param>
        /// <param name="radius">Base sphere radius.</param>
        /// <param name="noiseAmount">Amount of radial noise (0-1 range, as fraction of radius).</param>
        /// <returns>Array of generated points with depth-based coloring.</returns>
        public static PointStruct[] GenerateNoisySphere(int pointCount, float radius = 1f, float noiseAmount = 0.2f)
        {
            PointStruct[] points = new PointStruct[pointCount];
            System.Random rand = new System.Random(50); // Deterministic for reproducibility

            for (int i = 0; i < pointCount; i++)
            {
                // Random point on sphere surface
                float u = (float)rand.NextDouble();
                float v = (float)rand.NextDouble();
                float theta = 2f * Mathf.PI * u;
                float phi = Mathf.Acos(2f * v - 1f);

                float r = radius * (1f + noiseAmount * ((float)rand.NextDouble() - 0.5f) * 2f);

                float x = r * Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = r * Mathf.Sin(phi) * Mathf.Sin(theta);
                float z = r * Mathf.Cos(phi);

                // Color by depth/distance from center
                float normalizedDist = (r - radius * (1f - noiseAmount)) / (radius * noiseAmount * 2f);
                normalizedDist = Mathf.Clamp01(normalizedDist); // Ensure [0,1] range for safe byte conversion

                Color32 color = new Color32(
                    (byte)(normalizedDist * 255),
                    (byte)((1f - Mathf.Abs(normalizedDist - 0.5f) * 2f) * 255),
                    (byte)((1f - normalizedDist) * 255),
                    255
                );

                points[i] = new PointStruct(x, y, z, PointStruct.PackColor(color));
            }

            return points;
        }

        /// <summary>
        /// Save point array to binary file.
        /// Format: [uint32 count][PointStruct * count]
        /// </summary>
        /// <param name="points">Points to save.</param>
        /// <param name="filePath">Destination file path. Directory will be created if needed.</param>
        public static void SaveToBinary(PointStruct[] points, string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                writer.Write((uint)points.Length);

                for (int i = 0; i < points.Length; i++)
                {
                    writer.Write(points[i].x);
                    writer.Write(points[i].y);
                    writer.Write(points[i].z);
                    writer.Write(points[i].rgba);
                }
            }

            long fileSize = new FileInfo(filePath).Length;
            string sizeStr = fileSize >= 1024 * 1024
                ? $"{fileSize / (1024f * 1024f):F2} MB"
                : $"{fileSize / 1024f:F1} KB";

            Debug.Log($"Saved {points.Length:N0} points to {filePath} ({sizeStr})");
        }

        /// <summary>
        /// [Unused] Fast save using unsafe memory copy for better performance.
        /// Included for theoretical testing at some point, but not needed for this profiling
        /// Format: [uint32 count][PointStruct * count]
        /// </summary>
        /// <param name="points">Points to save.</param>
        /// <param name="filePath">Destination file path. Directory will be created if needed.</param>
        public static unsafe void SaveToBinaryFast(PointStruct[] points, string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Calculate total file size: 4 bytes (count) + point data
            long dataSize = (long)points.Length * PointStruct.STRIDE;
            long totalSize = 4 + dataSize;

            byte[] fileBytes = new byte[totalSize];

            fixed (byte* pBytes = fileBytes)
            {
                // Write point count header
                *(uint*)pBytes = (uint)points.Length;

                // Write point data using block copy
                fixed (PointStruct* pPoints = points)
                {
                    byte* src = (byte*)pPoints;
                    byte* dst = pBytes + 4; // Skip count header

                    Buffer.MemoryCopy(src, dst, dataSize, dataSize);
                }
            }

            // Write to file
            File.WriteAllBytes(filePath, fileBytes);

            long fileSize = new FileInfo(filePath).Length;
            string sizeStr = fileSize >= 1024 * 1024
                ? $"{fileSize / (1024f * 1024f):F2} MB"
                : $"{fileSize / 1024f:F1} KB";

            Debug.Log($"Fast saved {points.Length:N0} points to {filePath} ({sizeStr})");
        }

        /// <summary>
        /// Generate all standard test files (100k, 500k, 1M point spheres).
        /// </summary>
        /// <param name="outputPath">Directory to save generated files.</param>
        public static void GenerateTestData(string outputPath, string type = "uniform")
        {
            var configs = new[]
            {
                (count: 100_000, name: $"{type}sphere_100k.bin"),
                (count: 500_000, name: $"{type}sphere_500k.bin"),
                (count: 1_000_000, name: $"{type}sphere_1m.bin")
            };

            foreach (var config in configs)
            {
                Debug.Log($"Generating {config.name}...");

                PointStruct[] points = type switch
                {
                    "uniform" => GenerateSphere(config.count, radius: 5f, center: default),
                    "noisy" => GenerateNoisySphere(config.count, radius: 5f, noiseAmount: 0.2f),
                    _ => System.Array.Empty<PointStruct>()
                };

                string filePath = Path.Combine(outputPath, config.name);
                SaveToBinary(points, filePath);
            }

            Debug.Log($"Test data generation complete. Files saved to {outputPath}");

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

#if UNITY_EDITOR

        private static string GetFilePath()
        {
            string path = EditorUtility.SaveFolderPanel("Select Output Folder", "Assets", "SamplePointClouds");

            if (string.IsNullOrEmpty(path)) { return ""; }

            // Convert to relative path if inside Assets
            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }

            return path;
        }

        [MenuItem("Tools/Point Cloud/Generate Uniform Test Data")]
        private static void GenerateUniformTestDataMenu()
        {
            GenerateTestData(DefaultOutputPath, "uniform");
        }

        [MenuItem("Tools/Point Cloud/Generate Uniform Test Data (Custom Path)")]
        private static void GenerateUniformTestDataCustomPath()
        {
            string path = GetFilePath();
            if (!string.IsNullOrEmpty(path)) { GenerateTestData(path, "uniform"); }
        }

        [MenuItem("Tools/Point Cloud/Generate Noisy Test Data")]
        private static void GenerateNoisyTestDataMenu()
        {
            GenerateTestData(DefaultOutputPath, "noisy");
        }

        [MenuItem("Tools/Point Cloud/Generate Noisy Test Data (Custom Path)")]
        private static void GenerateNoisyTestDataCustomPath()
        {
            string path = GetFilePath();
            if (!string.IsNullOrEmpty(path)) { GenerateTestData(path, "noisy"); }
        }

#endif

    }
}