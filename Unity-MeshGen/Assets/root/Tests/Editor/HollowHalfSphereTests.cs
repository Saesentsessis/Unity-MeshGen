using System;
using System.Collections.Generic;
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
	public class HollowHalfSphereTests
	{
		/// <summary>
		/// Band layouts that reach every loop-bridging path: equal neighbours, halving, doubling and arbitrary counts.
		/// </summary>
		private static readonly int[][] BandLayouts =
		{
			new[] { 3 },
			new[] { 6, 6, 6 },
			new[] { 8, 4 },
			new[] { 4, 8 },
			new[] { 7, 5, 3 },
			new[] { 8, 6, 4 },
			new[] { 100, 50, 25, 12, 6, 3 },
			new[] { 64, 32, 16, 8, 4 }
		};

		// ---------- Descriptor ----------

		[Test]
		public void Descriptor_FromBands_CountsVerticesAndTriangles()
		{
			using var descriptor = Descriptor(8, 6, 4);

			// 8 + 6 + 4 ring vertices and the apex; triangles: (8 + 6) and (6 + 4) between rings, 4 in the apex fan.
			Assert.AreEqual(19, descriptor.VertexCount);
			Assert.AreEqual((14 + 10 + 4) * 3, descriptor.IndexCount);
			Assert.AreEqual(8, descriptor.Resolution);
			CollectionAssert.AreEqual(new[] { 8, 6, 4 }, descriptor.BandVertexCounts.ToArray());
		}

		[Test]
		public void Descriptor_FromBands_RejectsBandsBelowThreeVertices_IncludingTheFirst()
		{
			foreach (var bands in new[] { new[] { 2 }, new[] { 2, 8 }, new[] { 8, 2 }, new[] { 8, 6, 0 } })
			{
				using var counts = new NativeArray<int>(bands, Allocator.Temp);
				Assert.Throws<ArgumentOutOfRangeException>(() => MeshGenerator.CreateHollowHalfSphereMeshDescriptor(counts),
					$"Bands {string.Join(", ", bands)}");
			}
		}

		[Test]
		public void Descriptor_FromBands_RejectsAnEmptyBandList()
		{
			using var counts = new NativeArray<int>(0, Allocator.Temp);
			Assert.Throws<ArgumentException>(() => MeshGenerator.CreateHollowHalfSphereMeshDescriptor(counts));
		}

		[TestCase(1, new[] { 3 })]
		[TestCase(3, new[] { 3 })]
		[TestCase(4, new[] { 4 })]
		[TestCase(5, new[] { 5 })]
		[TestCase(6, new[] { 6, 3 })]
		[TestCase(8, new[] { 8, 4 })]
		[TestCase(16, new[] { 16, 8, 4 })]
		[TestCase(64, new[] { 64, 32, 16, 8, 4 })]
		[TestCase(100, new[] { 100, 50, 25, 12, 6, 3 })]
		public void Descriptor_FromResolution_HalvesTheBands_AndCountsLikeTheBandList(int resolution, int[] expectedBands)
		{
			using var descriptor = MeshGenerator.CreateHollowHalfSphereMeshDescriptor(resolution, Allocator.Temp);
			CollectionAssert.AreEqual(expectedBands, descriptor.BandVertexCounts.ToArray());

			using var fromBands = Descriptor(expectedBands);
			Assert.AreEqual(fromBands.VertexCount, descriptor.VertexCount, "Vertex count");
			Assert.AreEqual(fromBands.IndexCount, descriptor.IndexCount, "Index count");
		}

		[Test]
		public void Descriptor_TakesOwnershipOfTheBands_UnlessCopied()
		{
			var owned = new NativeArray<int>(new[] { 8, 4 }, Allocator.Persistent);
			var descriptor = MeshGenerator.CreateHollowHalfSphereMeshDescriptor(owned);
			descriptor.Dispose();
			Assert.Catch(() => _ = owned[0], "Disposing the descriptor disposes the array it was given.");

			using var kept = new NativeArray<int>(new[] { 8, 4 }, Allocator.Persistent);
			var copy = MeshGenerator.CreateHollowHalfSphereMeshDescriptorWithCopy(kept, Allocator.Persistent);
			copy.Dispose();
			Assert.AreEqual(8, kept[0], "The copying overload leaves the caller's array alone.");
		}

		[Test]
		public void Descriptor_WithoutChecks_CountsLikeTheCheckedOne()
		{
			using var counts = new NativeArray<int>(new[] { 100, 50, 25, 12, 6, 3 }, Allocator.Temp);
			using var copy = new NativeArray<int>(counts, Allocator.Temp);

			MeshGenerator.CreateHollowHalfSphereMeshDescriptor(counts, out var fast);
			var safe = MeshGenerator.CreateHollowHalfSphereMeshDescriptor(copy);

			Assert.AreEqual(safe.VertexCount, fast.VertexCount);
			Assert.AreEqual(safe.IndexCount, fast.IndexCount);
		}

		// ---------- Mesh ----------

		[Test]
		public void Mesh_PutsEveryVertexOnTheUnitHalfSphere([ValueSource(nameof(BandLayouts))] int[] bands)
		{
			using var descriptor = Descriptor(bands);
			var mesh = MeshGenerator.CreateHollowHalfSphereMesh(descriptor);

			try
			{
				var vertices = MeshAssert.Vertices(mesh);
				Assert.AreEqual(bands.Sum() + 1, vertices.Length);
				Assert.AreEqual(new float3(0f, 0f, 1f), vertices[^1], "The apex is the last vertex.");

				var first = 0;
				for (var band = 0; band < bands.Length; band++)
				{
					// Band 0 is the rim (z = 0); the bands step evenly in elevation towards the apex.
					var z = math.sin(math.PIHALF / bands.Length * band);

					for (var i = 0; i < bands[band]; i++)
					{
						var v = vertices[first + i];
						Assert.AreEqual(1f, math.length(v), 1e-4f, $"Band {band}, vertex {i}: distance from the centre");
						Assert.AreEqual(z, v.z, 1e-5f, $"Band {band}, vertex {i}: z");
					}

					first += bands[band];
				}
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void Mesh_IsClosedExceptAtTheRim_WithConsistentWinding([ValueSource(nameof(BandLayouts))] int[] bands)
		{
			using var descriptor = Descriptor(bands);
			var mesh = MeshGenerator.CreateHollowHalfSphereMesh(descriptor);

			try
			{
				var vertices = MeshAssert.Vertices(mesh);
				var indices = mesh.triangles;

				Assert.AreEqual(descriptor.IndexCount, indices.Length);
				MeshAssert.IndicesInRange(indices, 0, descriptor.VertexCount);
				MeshAssert.NoDegenerateTriangles(vertices, indices, 1e-6f);
				MeshAssert.ConsistentWinding(indices);

				// Each edge joins two triangles, except the rim's (band 0, z = 0), which belong to one.
				var open = new List<(int, int)>();
				foreach (var (edge, uses) in MeshAssert.EdgeUseCounts(indices))
				{
					Assert.LessOrEqual(uses, 2, $"Edge {edge} is shared by {uses} triangles.");
					if (uses == 1)
						open.Add(edge);
				}

				Assert.AreEqual(bands[0], open.Count, "Open edges");
				Assert.IsTrue(open.All(e => e.Item1 < bands[0] && e.Item2 < bands[0]), "Every open edge lies on the rim.");
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void Mesh_FacesOutwards_LikeTheCone([ValueSource(nameof(BandLayouts))] int[] bands)
		{
			using var descriptor = Descriptor(bands);
			var mesh = MeshGenerator.CreateHollowHalfSphereMesh(descriptor);

			try
			{
				var vertices = MeshAssert.Vertices(mesh);
				var indices = mesh.triangles;

				for (var t = 0; t < indices.Length / 3; t++)
				{
					var centroid = (vertices[indices[t * 3]] + vertices[indices[t * 3 + 1]] + vertices[indices[t * 3 + 2]]) / 3f;
					Assert.Greater(math.dot(MeshAssert.Normal(vertices, indices, t), centroid), 0f, $"Triangle {t} faces the centre.");
				}
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void Mesh_BoundsEncloseEveryVertex()
		{
			using var descriptor = Descriptor(16, 8, 4);
			var mesh = MeshGenerator.CreateHollowHalfSphereMesh(descriptor);

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
		public void Mesh_UsesThirtyTwoBitIndicesOnlyWhenNeeded(int ringVertices, IndexFormat expected)
		{
			using var descriptor = Descriptor(ringVertices);
			var mesh = MeshGenerator.CreateHollowHalfSphereMesh(descriptor);

			try
			{
				Assert.AreEqual(expected, mesh.indexFormat);
				MeshAssert.IndicesInRange(mesh.triangles, 0, descriptor.VertexCount);
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void GenerateMesh_Extension_MatchesCreateHollowHalfSphereMesh()
		{
			var descriptor = Descriptor(12, 6, 3);
			var created = MeshGenerator.CreateHollowHalfSphereMesh(descriptor);
			var extension = descriptor.GenerateMesh();

			try
			{
				CollectionAssert.AreEqual(created.vertices, extension.vertices);
				CollectionAssert.AreEqual(created.triangles, extension.triangles);
			}
			finally
			{
				descriptor.Dispose();
				Object.DestroyImmediate(created);
				Object.DestroyImmediate(extension);
			}
		}

		// ---------- Raw data ----------

		[Test]
		public void GenerateData_AllOverloadsWriteTheSameMesh_ShiftedByTheBaseVertexIndex(
			[ValueSource(nameof(BandLayouts))] int[] bands, [Values(0, 7)] int baseVertex)
		{
			using var descriptor = Descriptor(bands);
			var mesh = MeshGenerator.CreateHollowHalfSphereMesh(descriptor);

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

		// ---------- Helpers ----------

		/// <summary>
		/// A descriptor that owns a copy of <paramref name="bands"/> (dispose it).
		/// </summary>
		private static HollowHalfSphereMeshDescriptor Descriptor(params int[] bands)
		{
			var counts = new NativeArray<int>(bands, Allocator.Persistent);
			return MeshGenerator.CreateHollowHalfSphereMeshDescriptor(counts);
		}

		/// <summary>
		/// Runs every <c>GenerateHollowHalfSphereMeshData</c> overload with <paramref name="baseVertex"/> and returns
		/// what each wrote; the slice overloads write <paramref name="baseVertex"/> entries into a larger vertex buffer.
		/// </summary>
		private static (string name, float3[] vertices, int[] indices)[] GenerateAll(
			in HollowHalfSphereMeshDescriptor descriptor, int baseVertex)
		{
			var vertexCount = descriptor.VertexCount;
			var indexCount = descriptor.IndexCount;

			using var ownVertices = new NativeArray<float3>(vertexCount, Allocator.Temp);
			using var sharedVertices = new NativeArray<float3>(baseVertex + vertexCount, Allocator.Temp);
			using var shortIndices = new NativeArray<ushort>(indexCount, Allocator.Temp);
			using var intIndices = new NativeArray<int>(indexCount, Allocator.Temp);

			var vertexSlice = new NativeSlice<float3>(sharedVertices, baseVertex, vertexCount);
			var results = new (string, float3[], int[])[4];

			MeshGenerator.GenerateHollowHalfSphereMeshData(descriptor, ownVertices, shortIndices, (ushort)baseVertex);
			results[0] = ("NativeArray<ushort>", ownVertices.ToArray(), shortIndices.Select(i => (int)i).ToArray());

			MeshGenerator.GenerateHollowHalfSphereMeshData(descriptor, ownVertices, intIndices, baseVertex);
			results[1] = ("NativeArray<int>", ownVertices.ToArray(), intIndices.ToArray());

			MeshGenerator.GenerateHollowHalfSphereMeshData(descriptor, vertexSlice, new NativeSlice<ushort>(shortIndices),
				(ushort)baseVertex);
			results[2] = ("NativeSlice<ushort>", vertexSlice.ToArray(), shortIndices.Select(i => (int)i).ToArray());

			MeshGenerator.GenerateHollowHalfSphereMeshData(descriptor, vertexSlice, new NativeSlice<int>(intIndices), baseVertex);
			results[3] = ("NativeSlice<int>", vertexSlice.ToArray(), intIndices.ToArray());

			return results;
		}
	}
}
