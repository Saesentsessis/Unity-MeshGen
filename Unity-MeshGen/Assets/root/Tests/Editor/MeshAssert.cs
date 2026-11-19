using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Saesentsessis.MeshGeneration.Tests.Editor
{
	/// <summary>
	/// Geometry checks shared by the shape tests.
	/// </summary>
	internal static class MeshAssert
	{
		/// <summary>
		/// Every index addresses a vertex of the range [first, first + count).
		/// </summary>
		public static void IndicesInRange(IReadOnlyList<int> indices, int first, int count)
		{
			for (var i = 0; i < indices.Count; i++)
			{
				if (indices[i] < first || indices[i] >= first + count)
					Assert.Fail($"Index {i} is {indices[i]}, outside [{first}, {first + count}).");
			}
		}

		/// <summary>
		/// No triangle repeats a vertex or has (almost) zero area.
		/// </summary>
		public static void NoDegenerateTriangles(IReadOnlyList<float3> vertices, IReadOnlyList<int> indices, float minArea)
		{
			for (var t = 0; t < indices.Count; t += 3)
			{
				int a = indices[t], b = indices[t + 1], c = indices[t + 2];
				Assert.IsTrue(a != b && b != c && a != c, $"Triangle {t / 3} repeats a vertex: {a}, {b}, {c}.");

				var area = 0.5f * math.length(math.cross(vertices[b] - vertices[a], vertices[c] - vertices[a]));
				Assert.Greater(area, minArea, $"Triangle {t / 3} ({a}, {b}, {c}) is degenerate.");
			}
		}

		/// <summary>
		/// The (unnormalized) normal of a triangle, in the winding order of its indices.
		/// </summary>
		public static float3 Normal(IReadOnlyList<float3> vertices, IReadOnlyList<int> indices, int triangle)
		{
			var a = vertices[indices[triangle * 3]];
			var b = vertices[indices[triangle * 3 + 1]];
			var c = vertices[indices[triangle * 3 + 2]];
			return math.cross(b - a, c - a);
		}

		/// <summary>
		/// How many triangles use each undirected edge.
		/// </summary>
		public static Dictionary<(int, int), int> EdgeUseCounts(IReadOnlyList<int> indices)
		{
			var edges = new Dictionary<(int, int), int>();

			for (var t = 0; t < indices.Count; t += 3)
			{
				for (var e = 0; e < 3; e++)
				{
					var a = indices[t + e];
					var b = indices[t + (e + 1) % 3];
					var key = a < b ? (a, b) : (b, a);
					edges.TryGetValue(key, out var uses);
					edges[key] = uses + 1;
				}
			}

			return edges;
		}

		/// <summary>
		/// Every directed edge appears at most once: neighbouring triangles wind the same way.
		/// </summary>
		public static void ConsistentWinding(IReadOnlyList<int> indices)
		{
			var directed = new HashSet<(int, int)>();

			for (var t = 0; t < indices.Count; t += 3)
			{
				for (var e = 0; e < 3; e++)
				{
					var edge = (indices[t + e], indices[t + (e + 1) % 3]);
					Assert.IsTrue(directed.Add(edge), $"Directed edge {edge} appears twice: two triangles wind against each other.");
				}
			}
		}

		public static float3[] Vertices(Mesh mesh)
		{
			var source = mesh.vertices;
			var result = new float3[source.Length];
			for (var i = 0; i < source.Length; i++)
				result[i] = source[i];

			return result;
		}
	}
}
