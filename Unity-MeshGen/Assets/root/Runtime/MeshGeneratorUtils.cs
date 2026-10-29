using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Saesentsessis.MeshGeneration
{
	public static class MeshGeneratorUtils
	{
		#region Bridge Circle Loops

		/// Bridges two loops of vertices, assuming that both of them have equally spaced
		/// vertices in a circular pattern.
		/// <param name="indices">Index buffer pointer.</param>
		/// <param name="startA">Starting vertex index of a loop A.</param>
		/// <param name="countA">Vertex count of a loop A.</param>
		/// <param name="startB">Starting vertex index of a loop B.</param>
		/// <param name="countB">Vertex count of a loop B.</param>
		/// <returns>Index buffer pointer, pointing at the next index after last written.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe ushort* BridgeCircleLoops(ushort* indices, int startA, int countA, int startB, int countB)
		{
			// Fast path for case when vertex count of A equal B 
			if (countA == countB)
				return BridgeCircleLoops_Equal(indices, startA, startB, countA);

			// Fast path for case when A has double the count of B
			if (countA == countB * 2)
				return BridgeCircleLoops_AHalf_B(indices, startA, startB, countB);

			// Fast path for case when B has double the count of A
			if (countB == countA * 2)
				return BridgeCircleLoops_BDouble_A(indices, startA, startB, countA);

			return BridgeCircleLoops_Generic(indices, startA, countA, startB, countB);
		}

		/// Bridges two loops of vertices, assuming that both of them have equally spaced
		/// vertices in a circular pattern.
		/// <param name="indices">Index buffer pointer.</param>
		/// <param name="startA">Starting vertex index of a loop A.</param>
		/// <param name="countA">Vertex count of a loop A.</param>
		/// <param name="startB">Starting vertex index of a loop B.</param>
		/// <param name="countB">Vertex count of a loop B.</param>
		/// <returns>Index buffer pointer, pointing at the next index after last written.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe int* BridgeCircleLoops(int* indices, int startA, int countA, int startB, int countB)
		{
			// Fast path for case when vertex count of A equal B 
			if (countA == countB)
				return BridgeCircleLoops_Equal(indices, startA, startB, countA);

			// Fast path for case when A has double the count of B
			if (countA == countB * 2)
				return BridgeCircleLoops_AHalf_B(indices, startA, startB, countB);

			// Fast path for case when B has double the count of A
			if (countB == countA * 2)
				return BridgeCircleLoops_BDouble_A(indices, startA, startB, countA);

			return BridgeCircleLoops_Generic(indices, startA, countA, startB, countB);
		}

		#region Ushort Indexing

		private static unsafe ushort* BridgeCircleLoops_Generic(ushort* indices, int startA, int countA, int startB,
			int countB)
		{
			int endA = startA + countA, endB = startB + countB;
			int stepA = countB << 1, stepB = countA << 1;
			int err = countA, curA = startA, curB = startB;

			var begin = indices;
			var end = indices + 3 * (countA + countB);

			for (; indices != end; indices += 3)
			{
				var advA = err >= 0;
				indices[0] = (ushort)(advA ? curA : curB);

				curA = advA ? curA + 1 : curA;
				curB = advA ? curB : curB + 1;
				indices[1] = (ushort)curB;
				indices[2] = (ushort)curA;

				err += advA ? -stepA : stepB;
			}

			// Ring ends were emitted as endA/endB.
			// They form a short contiguous suffix, that should be wrapped.
			for (var q = end - 3; q >= begin && q[1] == (ushort)endB; q -= 3)
				q[1] = (ushort)startB;

			for (var q = end - 3; q >= begin && q[2] == (ushort)endA; q -= 3)
				q[2] = (ushort)startA;

			return end;
		}

		private static unsafe ushort* BridgeCircleLoops_Equal(ushort* indices, int startA, int startB, int count)
		{
			int a = startA, b = startB;

			for (var i = 1; i < count; i++)
			{
				// Two consecutive triangles in A B pattern
				indices[0] = (ushort)a;
				indices[1] = (ushort)b;
				indices[2] = (ushort)(a + 1);

				indices[3] = (ushort)b;
				indices[4] = (ushort)(b + 1);
				indices[5] = (ushort)(a + 1);

				a++;
				b++;

				indices += 6;
			}

			// Last quad wraps
			indices[0] = (ushort)a;
			indices[1] = (ushort)b;
			indices[2] = (ushort)startA;

			indices[3] = (ushort)b;
			indices[4] = (ushort)startB;
			indices[5] = (ushort)startA;

			return indices + 6;
		}

		// A A B, countA = 2m, countB = m
		private static unsafe ushort* BridgeCircleLoops_AHalf_B(ushort* indices, int startA, int startB, int count)
		{
			int a = startA, b = startB;

			for (var i = 1; i < count; i++)
			{
				// Three consecutive triangles in A A B pattern
				indices[0] = (ushort)a;
				indices[1] = (ushort)b;
				indices[2] = (ushort)(a + 1);

				indices[3] = (ushort)(a + 1);
				indices[4] = (ushort)b;
				indices[5] = (ushort)(a + 2);

				indices[6] = (ushort)b;
				indices[7] = (ushort)(b + 1);
				indices[8] = (ushort)(a + 2);

				a += 2;
				b++;

				indices += 9;
			}

			// Last period wraps
			indices[0] = (ushort)a;
			indices[1] = (ushort)b;
			indices[2] = (ushort)(a + 1);

			indices[3] = (ushort)(a + 1);
			indices[4] = (ushort)b;
			indices[5] = (ushort)startA;

			indices[6] = (ushort)b;
			indices[7] = (ushort)startB;
			indices[8] = (ushort)startA;

			return indices + 9;
		}

		// A B B, countA = m, countB = 2m
		private static unsafe ushort* BridgeCircleLoops_BDouble_A(ushort* indices, int startA, int startB, int count)
		{
			int a = startA, b = startB;

			for (var i = 1; i < count; i++)
			{
				// Three consecutive triangles in A B B pattern
				indices[0] = (ushort)a;
				indices[1] = (ushort)b;
				indices[2] = (ushort)(a + 1);

				indices[3] = (ushort)b;
				indices[4] = (ushort)(b + 1);
				indices[5] = (ushort)(a + 1);

				indices[6] = (ushort)(b + 1);
				indices[7] = (ushort)(b + 2);
				indices[8] = (ushort)(a + 1);

				a++;
				b += 2;

				indices += 9;
			}

			// Last period wraps
			indices[0] = (ushort)a;
			indices[1] = (ushort)b;
			indices[2] = (ushort)startA;

			indices[3] = (ushort)b;
			indices[4] = (ushort)(b + 1);
			indices[5] = (ushort)startA;

			indices[6] = (ushort)(b + 1);
			indices[7] = (ushort)startB;
			indices[8] = (ushort)startA;

			return indices + 9;
		}

		#endregion

		#region Int Indexing

		private static unsafe int* BridgeCircleLoops_Generic(int* indices, int startA, int countA, int startB, int countB)
		{
			int endA = startA + countA, endB = startB + countB;
			int stepA = countB << 1, stepB = countA << 1;
			int err = countA, curA = startA, curB = startB;

			var begin = indices;
			var end = indices + 3 * (countA + countB);

			for (; indices != end; indices += 3)
			{
				var advA = err >= 0;
				indices[0] = (ushort)(advA ? curA : curB);

				curA = advA ? curA + 1 : curA;
				curB = advA ? curB : curB + 1;
				indices[1] = (ushort)curB;
				indices[2] = (ushort)curA;

				err += advA ? -stepA : stepB;
			}

			// Ring ends were emitted as endA/endB.
			// They form a short contiguous suffix, that should be wrapped.
			for (var q = end - 3; q >= begin && q[1] == (ushort)endB; q -= 3)
				q[1] = (ushort)startB;

			for (var q = end - 3; q >= begin && q[2] == (ushort)endA; q -= 3)
				q[2] = (ushort)startA;

			return end;
		}

		private static unsafe int* BridgeCircleLoops_Equal(int* indices, int startA, int startB, int count)
		{
			int a = startA, b = startB;

			for (var i = 1; i < count; i++)
			{
				// Two consecutive triangles in A B pattern
				indices[0] = (ushort)a;
				indices[1] = (ushort)b;
				indices[2] = (ushort)(a + 1);

				indices[3] = (ushort)b;
				indices[4] = (ushort)(b + 1);
				indices[5] = (ushort)(a + 1);

				a++;
				b++;

				indices += 6;
			}

			// Last quad wraps
			indices[0] = (ushort)a;
			indices[1] = (ushort)b;
			indices[2] = (ushort)startA;

			indices[3] = (ushort)b;
			indices[4] = (ushort)startB;
			indices[5] = (ushort)startA;

			return indices + 6;
		}

		// A A B, countA = 2m, countB = m
		private static unsafe int* BridgeCircleLoops_AHalf_B(int* indices, int startA, int startB, int count)
		{
			int a = startA, b = startB;

			for (var i = 1; i < count; i++)
			{
				// Three consecutive triangles in A A B pattern
				indices[0] = (ushort)a;
				indices[1] = (ushort)b;
				indices[2] = (ushort)(a + 1);

				indices[3] = (ushort)(a + 1);
				indices[4] = (ushort)b;
				indices[5] = (ushort)(a + 2);

				indices[6] = (ushort)b;
				indices[7] = (ushort)(b + 1);
				indices[8] = (ushort)(a + 2);

				a += 2;
				b++;

				indices += 9;
			}

			// Last period wraps
			indices[0] = (ushort)a;
			indices[1] = (ushort)b;
			indices[2] = (ushort)(a + 1);

			indices[3] = (ushort)(a + 1);
			indices[4] = (ushort)b;
			indices[5] = (ushort)startA;

			indices[6] = (ushort)b;
			indices[7] = (ushort)startB;
			indices[8] = (ushort)startA;

			return indices + 9;
		}

		// A B B, countA = m, countB = 2m
		private static unsafe int* BridgeCircleLoops_BDouble_A(int* indices, int startA, int startB, int count)
		{
			int a = startA, b = startB;

			for (var i = 1; i < count; i++)
			{
				// Three consecutive triangles in A B B pattern
				indices[0] = (ushort)a;
				indices[1] = (ushort)b;
				indices[2] = (ushort)(a + 1);

				indices[3] = (ushort)b;
				indices[4] = (ushort)(b + 1);
				indices[5] = (ushort)(a + 1);

				indices[6] = (ushort)(b + 1);
				indices[7] = (ushort)(b + 2);
				indices[8] = (ushort)(a + 1);

				a++;
				b += 2;

				indices += 9;
			}

			// Last period wraps
			indices[0] = (ushort)a;
			indices[1] = (ushort)b;
			indices[2] = (ushort)startA;

			indices[3] = (ushort)b;
			indices[4] = (ushort)(b + 1);
			indices[5] = (ushort)startA;

			indices[6] = (ushort)(b + 1);
			indices[7] = (ushort)startB;
			indices[8] = (ushort)startA;

			return indices + 9;
		}

		#endregion

		#endregion

		#region Populate Circle Vertices
		
		/// <param name="verticesPtr">Reference to a vertex buffer pointer.</param>
		/// <param name="radius">Radius of the circle.</param>
		/// <param name="z">Z axis coordinate.</param>
		/// <param name="angleOffset">Rotational offset of the first vertex along the circle, in radians.</param>
		/// <param name="resolution">Resolution of the circle, i.e. how many vertices to append.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe float3* PopulateCircleVertices(float3* verticesPtr, float radius, float z, float angleOffset, int resolution) 
			=> PopulateCircleVertices(verticesPtr, radius, z, angleOffset, resolution, math.PI2 / resolution);
		
		/// <param name="verticesPtr">Reference to a vertex buffer pointer.</param>
		/// <param name="radius">Radius of the circle.</param>
		/// <param name="z">Z axis coordinate.</param>
		/// <param name="angleOffset">Rotational offset of the first vertex along the circle, in radians.</param>
		/// <param name="resolution">Resolution of the circle, i.e. how many vertices to append.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe float3* PopulateCircleVerticesExact(float3* verticesPtr, float radius, float z, float angleOffset, int resolution)
			=> PopulateCircleVerticesExact(verticesPtr, radius, z, angleOffset, resolution, math.PI2 / resolution);

		/// <param name="verticesPtr">Reference to a vertex buffer pointer.</param>
		/// <param name="radius">Radius of the circle.</param>
		/// <param name="z">Z axis coordinate.</param>
		/// <param name="angleOffset">Rotational offset of the first vertex along the circle, in radians.</param>
		/// <param name="resolution">Resolution of the circle, i.e. how many vertices to append.</param>
		/// <param name="vertexStep">Angular step between two vertices in radians.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe float3* PopulateCircleVertices(float3* verticesPtr, float radius, float z,
			float angleOffset, int resolution, float vertexStep)
		{
			// Every N-th group is computed with a real sincos. The groups in between are produced by
			// rotating the previous group. Bounds accumulated rounding/phase drift to N rotations.
			const int reseedInterval = 48;
			
			var lane = new float4(0, 1, 2, 3);
			
			// Rotation by 4 vertices (one group): every lane advances to the angle it has in the next group.
			// All lanes hold the same value, so these are used directly in vector math.
			math.sincos(new float4(vertexStep * 4f), out var groupStepSin, out var groupStepCos);
			var rotSin = groupStepSin;
			var rotCos = groupStepCos;
			
			// Unscaled sin/cos of the current group; carried across iterations by the recurrence.
			float4 s = default;
			float4 c = default;
			
			// Start at the interval so the very first group is always seeded
			var groupsSinceSeed = reseedInterval;
			var vertexIndex = 0f;
			
			// Vectorize the loop, processing 4 entries at a time.
			for (; vertexIndex + 4 <= resolution; vertexIndex += 4f)
			{
				if (groupsSinceSeed == reseedInterval)
				{
					var vertexAngles = math.mad(vertexStep, vertexIndex + lane, angleOffset);

					math.sincos(vertexAngles, out var seedS, out var seedC);
					s = seedS;
					c = seedC;
					groupsSinceSeed = 0;
				}
				else
				{
					// sin(a + d) = sin(a) * cos(d) + cos(a) * sin(d)
					// cos(a + d) = cos(a) * cos(d) - sin(a) * sin(d)
					var nextS = s * rotCos + c * rotSin;
					c = c * rotCos - s * rotSin;
					s = nextS;
				}

				groupsSinceSeed++;

				var x4 = s * radius;
				var y4 = c * radius;

				verticesPtr = Store4Verts(verticesPtr, x4, y4, z);
			}
			
			// Scalar looping over remaining vertices is slower than doing it by hand
			// with vectorized math.sincos operation, although only tested with the AVX2
			// instruction set
			if (vertexIndex < resolution)
			{
				var vertexAngles = math.mad(vertexStep, lane + vertexIndex, angleOffset);

				math.sincos(vertexAngles, out s, out c);
				var x4 = s * radius;
				var y4 = c * radius;

				var remaining = resolution - vertexIndex;

				*verticesPtr++ = new float3(x4.x, y4.x, z);

				// If statements are required, burst would turn them into mask blend operations
				if (remaining > 1)
					*verticesPtr++ = new float3(x4.y, y4.y, z);

				if (remaining > 2)
					*verticesPtr++ = new float3(x4.z, y4.z, z);
			}

			return verticesPtr;
		}
		
		/// <param name="verticesPtr">Reference to a vertex buffer pointer.</param>
		/// <param name="radius">Radius of the circle.</param>
		/// <param name="z">Z axis coordinate.</param>
		/// <param name="angleOffset">Rotational offset of the first vertex along the circle, in radians.</param>
		/// <param name="resolution">Resolution of the circle, i.e. how many vertices to append.</param>
		/// <param name="vertexStep">Angular step between two vertices in radians.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe float3* PopulateCircleVerticesExact(float3* verticesPtr, float radius, float z,
			float angleOffset, int resolution, float vertexStep)
		{
			var lane = new float4(0, 1, 2, 3);

			var vertexIndex = 0f;

			// Vectorize the loop, processing 4 entries at a time.
			for (; vertexIndex + 4 <= resolution; vertexIndex += 4f)
			{
				var vertexAngles = math.mad(vertexStep, vertexIndex + lane, angleOffset);
				
				math.sincos(vertexAngles, out var s, out var c);
				var x4 = s * radius;
				var y4 = c * radius;

				verticesPtr = Store4Verts(verticesPtr, x4, y4, z);
			}
			
			// Scalar looping over remaining vertices is slower than doing it by hand
			// with vectorized math.sincos operation.
			if (vertexIndex < resolution)
			{
				var vertexAngles = math.mad(vertexStep, lane + vertexIndex, angleOffset);

				math.sincos(vertexAngles, out var s, out var c);
				var x4 = s * radius;
				var y4 = c * radius;
				
				var remaining = resolution - vertexIndex;
				
				*verticesPtr++ = new float3(x4.x, y4.x, z);
				
				// If statements are required, burst would turn them into mask blend operations
				if (remaining > 1)
					*verticesPtr++ = new float3(x4.y, y4.y, z);
				
				if (remaining > 2)
					*verticesPtr++ = new float3(x4.z, y4.z, z);
			}

			return verticesPtr;
		}
		
		#endregion

		/// <summary>
		/// Writes 3 separate 4-wide components (x, y, z) of 4 vertices consecutively,
		/// totaling up 48 bytes written as (x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3).
		/// </summary>
		/// <remarks>
		/// Shuffle instructions are supported virtually on every device that Unity6+ is
		/// targeting, so there should be no issues regarding code portability. Burst
		/// generates really unoptimized code in the specific use cases where vectorization
		/// is involved. This is why this function exists: to guide the compiler to an
		/// optimal store pattern.
		/// </remarks>
		/// <param name="verticesPtr">Reference to a vertex buffer pointer.</param>
		/// <param name="x4">Four coordinates on X axis.</param>
		/// <param name="y4">Four coordinates on Y axis.</param>
		/// <param name="z4">Four coordinates on Z axis.</param>
		/// <returns>Vertex buffer pointer, pointing at the next vertex after last written.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe float3* Store4Verts(float3* verticesPtr, float4 x4, float4 y4, float4 z4)
		{
			var lo = math.shuffle(x4, y4,
				math.ShuffleComponent.LeftX, math.ShuffleComponent.RightX,		// x0, y0
				math.ShuffleComponent.LeftY, math.ShuffleComponent.RightY);	// x1, y1

			var hi = math.shuffle(x4, y4, 
				math.ShuffleComponent.LeftZ, math.ShuffleComponent.RightZ,		// x2, y2
				math.ShuffleComponent.LeftW, math.ShuffleComponent.RightW);		// x3, y3

			var m  = math.shuffle(lo, z4,
				math.ShuffleComponent.LeftW, math.ShuffleComponent.RightX,		// y1, z0
				math.ShuffleComponent.RightX, math.ShuffleComponent.RightX);	// z1, z2

			var o = (float4*)verticesPtr;

			o[0] = math.shuffle(lo, z4, math.ShuffleComponent.LeftX, math.ShuffleComponent.LeftY, // x0, y0
				math.ShuffleComponent.RightX, math.ShuffleComponent.LeftZ); // z0, x1

			o[1] = math.shuffle(m, hi, math.ShuffleComponent.LeftX, math.ShuffleComponent.LeftZ, // y1, z1
				math.ShuffleComponent.RightX, math.ShuffleComponent.RightY); // x2, y2

			o[2] = math.shuffle(z4, hi, math.ShuffleComponent.LeftZ, math.ShuffleComponent.RightZ, // z2, x3
				math.ShuffleComponent.RightW, math.ShuffleComponent.LeftW); // y3, z3

			return verticesPtr + 4;
		}
	}
}
