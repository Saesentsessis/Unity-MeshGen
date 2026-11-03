using System;
using System.Diagnostics;
using System.Globalization;
using Saesentsessis.MeshGeneration;
using Saesentsessis.MeshGeneration.Benchmarks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal class MeshGenBenchmark : MonoBehaviour
{
    private const float ConeHalfAngleRadians = math.TORADIANS * 45f;

    [SerializeField] private bool useBands;
    [SerializeField, Range(3, 32770)] private int resolution = 64;
    [SerializeField] private int[] bands = { 20, 16, 13, 10, 8, 6, 4 };

    [Space]
    [SerializeField] private int warmupIterationCount = 50;
    [SerializeField] private int iterationCount = 100_000;
    [SerializeField] private int runCount = 10;

    // Keeps managed results reachable so the allocations can't be optimized away.
    private Vector3[] managedSink;

    private void Start()
    {
       ExecuteBenchmark();
    }

    [ContextMenu(nameof(ExecuteBenchmark))]
    private void ExecuteBenchmark()
    {
       if (iterationCount < 1 || runCount < 1)
       {
          Debug.LogError($"{nameof(iterationCount)} and {nameof(runCount)} must be at least 1.");
          return;
       }

       var coneDescriptor = MeshGenerator.CreateHollowConeMeshDescriptor(resolution, ConeHalfAngleRadians);
       var sphereDescriptor = useBands
          ? MeshGenerator.CreateHollowHalfSphereMeshDescriptor(new NativeArray<int>(bands, Allocator.Temp))
          : MeshGenerator.CreateHollowHalfSphereMeshDescriptor(resolution, Allocator.Temp);

       var coneVertices = new NativeArray<float3>(coneDescriptor.Resolution + 1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
       var coneIndices = new NativeArray<ushort>(coneDescriptor.Resolution * 3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
       var sphereVertices = new NativeArray<float3>(sphereDescriptor.VertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
       var sphereIndices = new NativeArray<ushort>(sphereDescriptor.IndexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

       // Average time per call (ns) of every run, per path.
       var nativeCone = new double[runCount];
       var nativeSphere = new double[runCount];
       var managedCone = new double[runCount];
       var managedSphere = new double[runCount];

       long managedConeBytes;
       long managedSphereBytes;

       try
       {
          // Sanity check: the managed baseline must produce the same amount of geometry.
          {
             var (coneCheckVertices, coneCheckIndices) =
                NaiveMeshGenerator.GenerateHollowConeData(resolution, ConeHalfAngleRadians);
             var (sphereCheckVertices, sphereCheckIndices) = GenerateManagedSphere();

             if (coneCheckVertices.Length != coneVertices.Length || coneCheckIndices.Length != coneIndices.Length)
                Debug.LogWarning("Managed cone geometry size differs from the native one, results are not comparable.");

             if (sphereCheckVertices.Length != sphereVertices.Length || sphereCheckIndices.Length != sphereIndices.Length)
                Debug.LogWarning("Managed half-sphere geometry size differs from the native one, results are not comparable.");

             managedConeBytes = coneCheckVertices.Length * 12L + coneCheckIndices.Length * 4L;
             managedSphereBytes = sphereCheckVertices.Length * 12L + sphereCheckIndices.Length * 4L;
          }

          // Warmup
          for (var i = 0; i < warmupIterationCount; i++)
          {
             MeshGenerator.GenerateHollowConeMeshData(in coneDescriptor, coneVertices, coneIndices);
             MeshGenerator.GenerateHollowHalfSphereMeshData(in sphereDescriptor, sphereVertices, sphereIndices);

             managedSink = NaiveMeshGenerator.GenerateHollowConeData(resolution, ConeHalfAngleRadians).Vertices;
             managedSink = GenerateManagedSphere().Vertices;
          }

          // Measurement. Every phase is a hot loop of its own, so the paths don't disturb each other's caches.
          for (var run = 0; run < runCount; run++)
          {
             long coneAccumulator = 0;
             long sphereAccumulator = 0;
             long timestamp;

             // Native cone
             for (var i = 0; i < iterationCount; i++)
             {
                timestamp = Stopwatch.GetTimestamp();

                MeshGenerator.GenerateHollowConeMeshData(in coneDescriptor, coneVertices, coneIndices);

                coneAccumulator += Stopwatch.GetTimestamp() - timestamp;
             }

             nativeCone[run] = TicksToNanoseconds(coneAccumulator) / iterationCount;

             // Native half-sphere
             for (var i = 0; i < iterationCount; i++)
             {
                timestamp = Stopwatch.GetTimestamp();

                MeshGenerator.GenerateHollowHalfSphereMeshData(in sphereDescriptor, sphereVertices, sphereIndices);

                sphereAccumulator += Stopwatch.GetTimestamp() - timestamp;
             }

             nativeSphere[run] = TicksToNanoseconds(sphereAccumulator) / iterationCount;

             // Managed cone. Start from a clean heap, so garbage of the previous phase isn't billed to this one.
             GC.Collect();
             coneAccumulator = 0;

             for (var i = 0; i < iterationCount; i++)
             {
                timestamp = Stopwatch.GetTimestamp();

                var coneData = NaiveMeshGenerator.GenerateHollowConeData(resolution, ConeHalfAngleRadians);

                coneAccumulator += Stopwatch.GetTimestamp() - timestamp;

                managedSink = coneData.Vertices;
             }

             managedCone[run] = TicksToNanoseconds(coneAccumulator) / iterationCount;

             // Managed half-sphere
             GC.Collect();
             sphereAccumulator = 0;

             for (var i = 0; i < iterationCount; i++)
             {
                timestamp = Stopwatch.GetTimestamp();

                var sphereData = GenerateManagedSphere();

                sphereAccumulator += Stopwatch.GetTimestamp() - timestamp;

                managedSink = sphereData.Vertices;
             }

             managedSphere[run] = TicksToNanoseconds(sphereAccumulator) / iterationCount;
          }

          // Report
          Debug.Log(
             $"MeshGen data generation benchmark | {DescribeConfiguration()}\n" +
             FormatResult("Cone", Median(nativeCone), Median(managedCone), managedConeBytes,
                coneVertices.Length, coneIndices.Length / 3) + "\n" +
             FormatResult("Half-sphere", Median(nativeSphere), Median(managedSphere), managedSphereBytes,
                sphereVertices.Length, sphereIndices.Length / 3));
       }
       finally
       {
          managedSink = null;
          sphereDescriptor.Dispose();
          coneVertices.Dispose();
          coneIndices.Dispose();
          sphereVertices.Dispose();
          sphereIndices.Dispose();
       }
    }

    [ContextMenu(nameof(ExecuteFullCycleBenchmark))]
    private void ExecuteFullCycleBenchmark()
    {
       if (iterationCount < 1 || runCount < 1)
       {
          Debug.LogError($"{nameof(iterationCount)} and {nameof(runCount)} must be at least 1.");
          return;
       }

       // Average time per call (ns) of every run, per path.
       var libraryCone = new double[runCount];
       var librarySphere = new double[runCount];
       var baselineCone = new double[runCount];
       var baselineSphere = new double[runCount];

       int coneVertexCount, coneTriangleCount, sphereVertexCount, sphereTriangleCount;
       long baselineConeBytes, baselineSphereBytes;

       // Sanity check: both pipelines must produce meshes of the same size.
       {
          var libraryConeMesh = CreateLibraryConeMesh();
          var librarySphereMesh = CreateLibrarySphereMesh();
          var baselineConeMesh = NaiveMeshGenerator.CreateHollowConeMesh(resolution, ConeHalfAngleRadians);
          var baselineSphereMesh = CreateBaselineSphereMesh();

          coneVertexCount = libraryConeMesh.vertexCount;
          coneTriangleCount = (int)(libraryConeMesh.GetIndexCount(0) / 3);
          sphereVertexCount = librarySphereMesh.vertexCount;
          sphereTriangleCount = (int)(librarySphereMesh.GetIndexCount(0) / 3);

          if (baselineConeMesh.vertexCount != libraryConeMesh.vertexCount ||
              baselineConeMesh.GetIndexCount(0) != libraryConeMesh.GetIndexCount(0))
             Debug.LogWarning("Baseline cone mesh size differs from the library one, results are not comparable.");

          if (baselineSphereMesh.vertexCount != librarySphereMesh.vertexCount ||
              baselineSphereMesh.GetIndexCount(0) != librarySphereMesh.GetIndexCount(0))
             Debug.LogWarning("Baseline half-sphere mesh size differs from the library one, results are not comparable.");

          baselineConeBytes = baselineConeMesh.vertexCount * 12L + (long)baselineConeMesh.GetIndexCount(0) * 4L;
          baselineSphereBytes = baselineSphereMesh.vertexCount * 12L + (long)baselineSphereMesh.GetIndexCount(0) * 4L;

          DestroyImmediate(libraryConeMesh);
          DestroyImmediate(librarySphereMesh);
          DestroyImmediate(baselineConeMesh);
          DestroyImmediate(baselineSphereMesh);
       }

       // Warmup
       for (var i = 0; i < warmupIterationCount; i++)
       {
          var libraryConeMesh = CreateLibraryConeMesh();
          var librarySphereMesh = CreateLibrarySphereMesh();
          var baselineConeMesh = NaiveMeshGenerator.CreateHollowConeMesh(resolution, ConeHalfAngleRadians);
          var baselineSphereMesh = CreateBaselineSphereMesh();

          DestroyImmediate(libraryConeMesh);
          DestroyImmediate(librarySphereMesh);
          DestroyImmediate(baselineConeMesh);
          DestroyImmediate(baselineSphereMesh);
       }

       // Measurement. Timed: descriptor creation + mesh object creation.
       // Not timed: descriptor disposal and mesh destruction.
       for (var run = 0; run < runCount; run++)
       {
          long coneAccumulator;
          long sphereAccumulator;
          long timestamp;

          // Library cone
          GC.Collect();
          coneAccumulator = 0;

          for (var i = 0; i < iterationCount; i++)
          {
             timestamp = Stopwatch.GetTimestamp();

             var coneDescriptor = MeshGenerator.CreateHollowConeMeshDescriptor(resolution, ConeHalfAngleRadians);
             var coneMesh = MeshGenerator.CreateHollowConeMesh(coneDescriptor);

             coneAccumulator += Stopwatch.GetTimestamp() - timestamp;

             DestroyImmediate(coneMesh);
          }

          libraryCone[run] = TicksToNanoseconds(coneAccumulator) / iterationCount;

          // Library half-sphere
          GC.Collect();
          sphereAccumulator = 0;

          for (var i = 0; i < iterationCount; i++)
          {
             timestamp = Stopwatch.GetTimestamp();

             // In bands mode the NativeArray with the bands is created inside the timed region too.
             var sphereDescriptor = useBands
                ? MeshGenerator.CreateHollowHalfSphereMeshDescriptor(new NativeArray<int>(bands, Allocator.Temp))
                : MeshGenerator.CreateHollowHalfSphereMeshDescriptor(resolution, Allocator.Temp);
             var sphereMesh = MeshGenerator.CreateHollowHalfSphereMesh(sphereDescriptor);

             sphereAccumulator += Stopwatch.GetTimestamp() - timestamp;

             sphereDescriptor.Dispose();
             DestroyImmediate(sphereMesh);
          }

          librarySphere[run] = TicksToNanoseconds(sphereAccumulator) / iterationCount;

          // Baseline cone
          GC.Collect();
          coneAccumulator = 0;

          for (var i = 0; i < iterationCount; i++)
          {
             timestamp = Stopwatch.GetTimestamp();

             var coneMesh = NaiveMeshGenerator.CreateHollowConeMesh(resolution, ConeHalfAngleRadians);

             coneAccumulator += Stopwatch.GetTimestamp() - timestamp;

             DestroyImmediate(coneMesh);
          }

          baselineCone[run] = TicksToNanoseconds(coneAccumulator) / iterationCount;

          // Baseline half-sphere
          GC.Collect();
          sphereAccumulator = 0;

          for (var i = 0; i < iterationCount; i++)
          {
             timestamp = Stopwatch.GetTimestamp();

             var sphereMesh = CreateBaselineSphereMesh();

             sphereAccumulator += Stopwatch.GetTimestamp() - timestamp;

             DestroyImmediate(sphereMesh);
          }

          baselineSphere[run] = TicksToNanoseconds(sphereAccumulator) / iterationCount;
       }

       Debug.Log(
          $"MeshGen full-cycle benchmark (descriptor -> mesh object, disposal and destruction not timed) | {DescribeConfiguration()}\n" +
          FormatResult("Cone", Median(libraryCone), Median(baselineCone), baselineConeBytes,
             coneVertexCount, coneTriangleCount) + "\n" +
          FormatResult("Half-sphere", Median(librarySphere), Median(baselineSphere), baselineSphereBytes,
             sphereVertexCount, sphereTriangleCount));
    }

    // Untimed helpers, used by the sanity check and the warmup only.
    private Mesh CreateLibraryConeMesh()
       => MeshGenerator.CreateHollowConeMesh(
          MeshGenerator.CreateHollowConeMeshDescriptor(resolution, ConeHalfAngleRadians));

    private Mesh CreateLibrarySphereMesh()
    {
       var descriptor = useBands
          ? MeshGenerator.CreateHollowHalfSphereMeshDescriptor(new NativeArray<int>(bands, Allocator.Temp))
          : MeshGenerator.CreateHollowHalfSphereMeshDescriptor(resolution, Allocator.Temp);

       try
       {
          return MeshGenerator.CreateHollowHalfSphereMesh(descriptor);
       }
       finally
       {
          descriptor.Dispose();
       }
    }

    // Also used inside the timed region of the baseline half-sphere phase.
    private Mesh CreateBaselineSphereMesh()
       => useBands
          ? NaiveMeshGenerator.CreateHollowHalfSphereMesh(bands)
          : NaiveMeshGenerator.CreateHollowHalfSphereMesh(resolution);

    private string DescribeConfiguration()
    {
       var shape = useBands ? $"bands [{string.Join(", ", bands)}]" : $"resolution {resolution}";

       return $"Unity {Application.unityVersion} | {(Application.isEditor ? "Editor" : "Player")} | {shape} | " +
              $"{iterationCount} iterations x {runCount} runs (median of per-run averages), {warmupIterationCount} warmup";
    }

    private (Vector3[] Vertices, int[] Indices) GenerateManagedSphere()
       => useBands
          ? NaiveMeshGenerator.GenerateHollowHalfSphereData(bands)
          : NaiveMeshGenerator.GenerateHollowHalfSphereData(resolution);

    private static double TicksToNanoseconds(long ticks)
       => ticks * 1_000_000_000d / Stopwatch.Frequency;

    private static double Median(double[] values)
    {
       var sorted = (double[])values.Clone();
       Array.Sort(sorted);

       var middle = sorted.Length / 2;
       return sorted.Length % 2 == 1
          ? sorted[middle]
          : (sorted[middle - 1] + sorted[middle]) * 0.5;
    }

    private static string FormatTime(double nanoseconds)
       => nanoseconds >= 1000d
          ? (nanoseconds / 1000d).ToString("F3", CultureInfo.InvariantCulture) + " us"
          : nanoseconds.ToString("F0", CultureInfo.InvariantCulture) + " ns";

    private static string FormatResult(
       string name, double nativeNs, double managedNs, long managedBytes, int vertexCount, int triangleCount)
       => $"{name}: library {FormatTime(nativeNs)}, baseline {FormatTime(managedNs)} " +
          $"({(managedNs / nativeNs).ToString("F1", CultureInfo.InvariantCulture)}x slower), " +
          $"baseline arrays {(managedBytes / 1024d).ToString("F2", CultureInfo.InvariantCulture)} KiB per call, " +
          $"{vertexCount} verts, {triangleCount} tris";
}
