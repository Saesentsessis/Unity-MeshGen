using System;
using System.Linq;
using NUnit.Framework;
using Saesentsessis.MeshGeneration.Shapes;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Saesentsessis.MeshGeneration.Tests.Editor
{
	public class HollowConeTests
	{
		private static readonly int[] Resolutions = { 3, 4, 5, 7, 8, 13, 64, 1000 };

		// ---------- Descriptor ----------

		[TestCase(3)]
		[TestCase(8)]
		[TestCase(100)]
		public void Descriptor_CountsFollowTheResolution(int resolution)
		{
			var descriptor = MeshGenerator.CreateHollowConeMeshDescriptor(resolution, math.radians(30f));

			Assert.AreEqual(resolution, descriptor.Resolution);
			Assert.AreEqual(resolution + 1, descriptor.VertexCount);
			Assert.AreEqual(resolution * 3, descriptor.IndexCount);
		}

		[Test]
		public void Descriptor_Equality()
		{
			var a = MeshGenerator.CreateHollowConeMeshDescriptor(8, 0.5f);

			Assert.AreEqual(a, MeshGenerator.CreateHollowConeMeshDescriptor(8, 0.5f));
			Assert.IsTrue(a == new HollowConeMeshDescriptor(8, 0.5f));
			Assert.IsTrue(a != MeshGenerator.CreateHollowConeMeshDescriptor(9, 0.5f));
			Assert.IsTrue(a != MeshGenerator.CreateHollowConeMeshDescriptor(8, 0.6f));
			Assert.AreEqual(a.GetHashCode(), new HollowConeMeshDescriptor(8, 0.5f).GetHashCode());
		}

		// ---------- Mesh ----------

		[Test]
		public void Mesh_HasTheApexAtTheOrigin_AndAnEvenRingAtZOne([ValueSource(nameof(Resolutions))] int resolution)
		{
			var halfAngle = math.radians(30f);
			var mesh = MeshGenerator.CreateHollowConeMesh(MeshGenerator.CreateHollowConeMeshDescriptor(resolution, halfAngle));

			try
			{
				var vertices = MeshAssert.Vertices(mesh);
				Assert.AreEqual(resolution + 1, vertices.Length);
				Assert.AreEqual(float3.zero, vertices[0]);

				// The ring sits at z = 1 with radius sin(half angle); vertex i at angle i * 2 pi / resolution from +Y
				// towards +X.
				var radius = math.sin(halfAngle);
				for (var i = 0; i < resolution; i++)
				{
					var v = vertices[1 + i];
					Assert.AreEqual(1f, v.z, 1e-6f, $"Ring vertex {i}: z");
					Assert.AreEqual(radius, math.length(v.xy), 1e-4f, $"Ring vertex {i}: radius");

					var expected = math.PI2 * i / resolution;
					var angle = math.atan2(v.x, v.y);
					var difference = math.abs(math.fmod(angle - expected + 3f * math.PI, math.PI2) - math.PI);
					Assert.Less(difference, 1e-4f, $"Ring vertex {i}: angle {angle}, expected {expected}");
				}
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void Mesh_FansFromTheApex_OverEveryRingEdgeOnce([ValueSource(nameof(Resolutions))] int resolution)
		{
			var mesh = MeshGenerator.CreateHollowConeMesh(MeshGenerator.CreateHollowConeMeshDescriptor(resolution, math.radians(30f)));

			try
			{
				var vertices = MeshAssert.Vertices(mesh);
				var indices = mesh.triangles;

				Assert.AreEqual(resolution * 3, indices.Length);
				MeshAssert.IndicesInRange(indices, 0, resolution + 1);
				MeshAssert.NoDegenerateTriangles(vertices, indices, 1e-7f);
				MeshAssert.ConsistentWinding(indices);

				// Open at the ring: ring edges belong to one triangle, the spokes from the apex to two.
				foreach (var (edge, uses) in MeshAssert.EdgeUseCounts(indices))
				{
					var spoke = edge.Item1 == 0;
					Assert.AreEqual(spoke ? 2 : 1, uses, $"Edge {edge}");

					if (spoke == false)
					{
						var gap = edge.Item2 - edge.Item1;
						Assert.IsTrue(gap == 1 || gap == resolution - 1, $"Edge {edge} doesn't join neighbouring ring vertices.");
					}
				}
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void Mesh_FacesOutwards([ValueSource(nameof(Resolutions))] int resolution)
		{
			var mesh = MeshGenerator.CreateHollowConeMesh(MeshGenerator.CreateHollowConeMeshDescriptor(resolution, math.radians(30f)));

			try
			{
				var vertices = MeshAssert.Vertices(mesh);
				var indices = mesh.triangles;

				for (var t = 0; t < resolution; t++)
				{
					var centroid = (vertices[indices[t * 3]] + vertices[indices[t * 3 + 1]] + vertices[indices[t * 3 + 2]]) / 3f;
					var normal = MeshAssert.Normal(vertices, indices, t);
					Assert.Greater(math.dot(normal.xy, centroid.xy), 0f, $"Triangle {t} faces the axis.");
				}
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		[TestCase(3, 10f)]
		[TestCase(16, 45f)]
		[TestCase(64, 89f)]
		public void Mesh_BoundsEncloseEveryVertex(int resolution, float halfAngleDegrees)
		{
			var mesh = MeshGenerator.CreateHollowConeMesh(
				MeshGenerator.CreateHollowConeMeshDescriptor(resolution, math.radians(halfAngleDegrees)));

			try
			{
				Assert.AreEqual(mesh.bounds, mesh.GetSubMesh(0).bounds);

				var bounds = mesh.bounds;
				bounds.Expand(1e-5f);

				foreach (var vertex in mesh.vertices)
					Assert.IsTrue(bounds.Contains(vertex), $"{vertex} is outside {bounds}.");
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		[TestCase(100, IndexFormat.UInt16)]
		[TestCase(70000, IndexFormat.UInt32)]
		public void Mesh_UsesThirtyTwoBitIndicesOnlyWhenNeeded(int resolution, IndexFormat expected)
		{
			var mesh = MeshGenerator.CreateHollowConeMesh(MeshGenerator.CreateHollowConeMeshDescriptor(resolution, math.radians(30f)));

			try
			{
				Assert.AreEqual(expected, mesh.indexFormat);
				MeshAssert.IndicesInRange(mesh.triangles, 0, resolution + 1);
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		[TestCase(2, 30f)]
		[TestCase(0, 30f)]
		[TestCase(-1, 30f)]
		[TestCase(int.MaxValue, 30f)]
		[TestCase(8, 0f)]
		[TestCase(8, 90f)]
		[TestCase(8, -10f)]
		public void Mesh_RejectsInvalidDescriptors(int resolution, float halfAngleDegrees)
		{
			var descriptor = MeshGenerator.CreateHollowConeMeshDescriptor(resolution, math.radians(halfAngleDegrees));

			Assert.Throws<ArgumentOutOfRangeException>(() => MeshGenerator.CreateHollowConeMesh(descriptor));
		}

		[Test]
		public void GenerateMesh_Extension_MatchesCreateHollowConeMesh()
		{
			var descriptor = MeshGenerator.CreateHollowConeMeshDescriptor(12, 0.4f);
			var created = MeshGenerator.CreateHollowConeMesh(descriptor);
			var extension = descriptor.GenerateMesh();

			try
			{
				CollectionAssert.AreEqual(created.vertices, extension.vertices);
				CollectionAssert.AreEqual(created.triangles, extension.triangles);
			}
			finally
			{
				Object.DestroyImmediate(created);
				Object.DestroyImmediate(extension);
			}
		}

		// ---------- Raw data ----------

		[Test]
		public void GenerateData_AllOverloadsWriteTheSameMesh_ShiftedByTheBaseVertexIndex(
			[Values(3, 8, 13)] int resolution, [Values(0, 7)] int baseVertex)
		{
			var descriptor = MeshGenerator.CreateHollowConeMeshDescriptor(resolution, math.radians(25f));
			var mesh = MeshGenerator.CreateHollowConeMesh(descriptor);

			try
			{
				var expectedVertices = MeshAssert.Vertices(mesh);
				var expectedIndices = mesh.triangles.Select(i => i + baseVertex).ToArray();

				foreach (var (name, vertices, indices) in GenerateAll(descriptor, baseVertex))
				{
					CollectionAssert.AreEqual(expectedVertices, vertices, $"{name}: vertices");
					CollectionAssert.AreEqual(expectedIndices, indices, $"{name}: indices");
				}
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		/// <summary>
		/// Runs every <c>GenerateHollowConeMeshData</c> overload with <paramref name="baseVertex"/> and returns what each
		/// wrote; the slice overloads write <paramref name="baseVertex"/> entries into a larger vertex buffer, as when
		/// several shapes share one.
		/// </summary>
		private static (string name, float3[] vertices, int[] indices)[] GenerateAll(in HollowConeMeshDescriptor descriptor,
			int baseVertex)
		{
			var vertexCount = descriptor.VertexCount;
			var indexCount = descriptor.IndexCount;

			using var ownVertices = new NativeArray<float3>(vertexCount, Allocator.Temp);
			using var sharedVertices = new NativeArray<float3>(baseVertex + vertexCount, Allocator.Temp);
			using var shortIndices = new NativeArray<ushort>(indexCount, Allocator.Temp);
			using var intIndices = new NativeArray<int>(indexCount, Allocator.Temp);

			var vertexSlice = new NativeSlice<float3>(sharedVertices, baseVertex, vertexCount);
			var results = new (string, float3[], int[])[4];

			MeshGenerator.GenerateHollowConeMeshData(descriptor, ownVertices, shortIndices, (ushort)baseVertex);
			results[0] = ("NativeArray<ushort>", ownVertices.ToArray(), shortIndices.Select(i => (int)i).ToArray());

			MeshGenerator.GenerateHollowConeMeshData(descriptor, ownVertices, intIndices, baseVertex);
			results[1] = ("NativeArray<int>", ownVertices.ToArray(), intIndices.ToArray());

			MeshGenerator.GenerateHollowConeMeshData(descriptor, vertexSlice, new NativeSlice<ushort>(shortIndices), (ushort)baseVertex);
			results[2] = ("NativeSlice<ushort>", vertexSlice.ToArray(), shortIndices.Select(i => (int)i).ToArray());

			MeshGenerator.GenerateHollowConeMeshData(descriptor, vertexSlice, new NativeSlice<int>(intIndices), baseVertex);
			results[3] = ("NativeSlice<int>", vertexSlice.ToArray(), intIndices.ToArray());

			return results;
		}
	}
}
