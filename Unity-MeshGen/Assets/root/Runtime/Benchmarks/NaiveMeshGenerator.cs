using System;
using UnityEngine;

namespace Saesentsessis.MeshGeneration.Benchmarks
{
    /// <summary>
    /// Deliberately straightforward managed baseline: plain arrays, Mathf trig in the loops,
    /// no Burst, no jobs, no native memory, no precomputed tables.
    /// Geometry mirrors the library's shapes (vertex/triangle counts match the README benchmarks):
    /// both shapes are built around the +Z axis, front faces are clockwise as seen from outside
    /// (Unity's winding), and only vertex positions are produced.
    /// </summary>
    internal static class NaiveMeshGenerator
    {
        // ------------------------------------------------------------------
        // Hollow cone: apex at the origin, open base at z = 1, no base cap.
        // Vertices: resolution + 1, triangles: resolution.
        // ------------------------------------------------------------------

        public static (Vector3[] Vertices, int[] Indices) GenerateHollowConeData(
            int resolution, float halfAngleDegrees)
        {
            if (resolution < 3)
                throw new ArgumentOutOfRangeException(nameof(resolution), "Must be at least 3.");

            var vertices = new Vector3[resolution + 1];
            var indices = new int[resolution * 3];

            var radius = Mathf.Tan(halfAngleDegrees * Mathf.Deg2Rad);

            vertices[0] = Vector3.zero;
            for (var i = 0; i < resolution; i++)
            {
                var angle = 2f * Mathf.PI * i / resolution;
                vertices[i + 1] = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 1f);
            }

            for (var i = 0; i < resolution; i++)
            {
                indices[i * 3 + 0] = 0;
                indices[i * 3 + 1] = 1 + (i + 1) % resolution;
                indices[i * 3 + 2] = 1 + i;
            }

            return (vertices, indices);
        }

        public static Mesh CreateHollowConeMesh(int resolution, float halfAngleDegrees)
        {
            var (vertices, indices) = GenerateHollowConeData(resolution, halfAngleDegrees);
            return BuildMesh(vertices, indices);
        }

        // ------------------------------------------------------------------
        // Hollow half-sphere (unit radius, equator at z = 0, pole at z = 1, no equator cap).
        //
        // The shape is described by "bands": the vertex count of each ring, equator first,
        // strictly decreasing, followed by a single pole vertex.
        //   vertices  = 1 + sum(bands)
        //   triangles = 2 * sum(bands) - bands[0]
        // A ring with n vertices has radius n / bands[0], which keeps the vertex spacing
        // along every ring equal to the equator's.
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolution -> bands: integer-halve the ring size (fraction dropped) until a band
        /// would have fewer than 3 vertices. A band of exactly 3 vertices is valid.
        /// 64 -> 64,32,16,8,4 (then 2, dropped); 100 -> 100,50,25,12,6,3 (then 1, dropped).
        /// </summary>
        public static int[] BuildBands(int resolution)
        {
            if (resolution < 3)
                throw new ArgumentOutOfRangeException(nameof(resolution), "Must be at least 3.");

            var bands = new System.Collections.Generic.List<int>();
            for (var n = resolution; n >= 3; n /= 2)
                bands.Add(n);
            return bands.ToArray();
        }

        public static (Vector3[] Vertices, int[] Indices) GenerateHollowHalfSphereData(int resolution)
            => GenerateHollowHalfSphereData(BuildBands(resolution));

        public static (Vector3[] Vertices, int[] Indices) GenerateHollowHalfSphereData(int[] bands)
        {
            if (bands == null || bands.Length == 0)
                throw new ArgumentException("At least one band is required.", nameof(bands));

            var sum = 0;
            for (var r = 0; r < bands.Length; r++)
            {
                if (bands[r] < 3)
                    throw new ArgumentException("Every band needs at least 3 vertices.", nameof(bands));
                if (r > 0 && bands[r] >= bands[r - 1])
                    throw new ArgumentException("Bands must be strictly decreasing.", nameof(bands));
                sum += bands[r];
            }

            var vertices = new Vector3[sum + 1];
            var indices = new int[(2 * sum - bands[0]) * 3];
            var ringStart = new int[bands.Length];

            // Rings, equator first.
            var v = 0;
            for (var r = 0; r < bands.Length; r++)
            {
                ringStart[r] = v;
                var n = bands[r];
                var radius = (float)n / bands[0];
                var z = Mathf.Sqrt(1f - radius * radius);

                for (var k = 0; k < n; k++)
                {
                    var angle = 2f * Mathf.PI * k / n;
                    vertices[v++] = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), z);
                }
            }

            // Pole.
            var pole = v;
            vertices[pole] = new Vector3(0f, 0f, 1f);

            // Strips between neighbouring rings.
            var t = 0;
            for (var r = 0; r < bands.Length - 1; r++)
                StitchRings(indices, ref t, ringStart[r], bands[r], ringStart[r + 1], bands[r + 1]);

            // Fan around the pole.
            var last = bands.Length - 1;
            var lastCount = bands[last];
            for (var k = 0; k < lastCount; k++)
            {
                indices[t++] = ringStart[last] + k;
                indices[t++] = ringStart[last] + (k + 1) % lastCount;
                indices[t++] = pole;
            }

            return (vertices, indices);
        }

        public static Mesh CreateHollowHalfSphereMesh(int resolution)
        {
            var (vertices, indices) = GenerateHollowHalfSphereData(resolution);
            return BuildMesh(vertices, indices);
        }

        public static Mesh CreateHollowHalfSphereMesh(int[] bands)
        {
            var (vertices, indices) = GenerateHollowHalfSphereData(bands);
            return BuildMesh(vertices, indices);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        // Zips two rings with different vertex counts into lowerCount + upperCount triangles.
        // Both rings start at angle 0; at every step the ring whose next vertex comes first
        // (angle-wise) advances. Comparison is done in integers: (k+1)/nL <= (j+1)/nU.
        private static void StitchRings(
            int[] indices, ref int t,
            int lowerStart, int lowerCount,
            int upperStart, int upperCount)
        {
            int k = 0, j = 0;
            while (k < lowerCount || j < upperCount)
            {
                var advanceLower = k < lowerCount &&
                                   (j >= upperCount ||
                                    (long)(k + 1) * upperCount <= (long)(j + 1) * lowerCount);

                if (advanceLower)
                {
                    indices[t++] = lowerStart + k;
                    indices[t++] = lowerStart + (k + 1) % lowerCount;
                    indices[t++] = upperStart + j % upperCount;
                    k++;
                }
                else
                {
                    indices[t++] = lowerStart + k % lowerCount;
                    indices[t++] = upperStart + (j + 1) % upperCount;
                    indices[t++] = upperStart + j;
                    j++;
                }
            }
        }

        // Typical "just make a Mesh" path. Assigning triangles also updates the bounds.
        private static Mesh BuildMesh(Vector3[] vertices, int[] indices)
        {
            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = indices;
            return mesh;
        }
    }
}
