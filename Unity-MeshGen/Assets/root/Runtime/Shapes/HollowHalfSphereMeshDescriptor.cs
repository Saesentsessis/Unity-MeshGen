using System;
using Unity.Burst;
using Unity.Collections;

namespace Saesentsessis.MeshGeneration.Shapes
{
	public struct HollowHalfSphereMeshDescriptor : IEquatable<HollowHalfSphereMeshDescriptor>, IDisposable
	{
		public readonly int VertexCount;
		public readonly int IndexCount;
		
		private NativeArray<int> _bandVertexCounts;

		public HollowHalfSphereMeshDescriptor(int vertexCount, int indexCount, NativeArray<int> bandVertexCounts)
		{
			VertexCount = vertexCount;
			IndexCount = indexCount;
			_bandVertexCounts = bandVertexCounts;
		}
		
		public readonly int Resolution => _bandVertexCounts[0];

		public NativeArray<int>.ReadOnly BandVertexCounts => _bandVertexCounts.AsReadOnly();

		public bool Equals(HollowHalfSphereMeshDescriptor other)
		{
			return VertexCount == other.VertexCount && IndexCount == other.IndexCount
			       && _bandVertexCounts.Equals(other._bandVertexCounts);
		}

		[BurstDiscard]
		public override bool Equals(object obj)
		{
			return obj is HollowHalfSphereMeshDescriptor other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Resolution, IndexCount, _bandVertexCounts);
		}

		public static bool operator ==(HollowHalfSphereMeshDescriptor left, HollowHalfSphereMeshDescriptor right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(HollowHalfSphereMeshDescriptor left, HollowHalfSphereMeshDescriptor right)
		{
			return !left.Equals(right);
		}

		public void Dispose()
		{
			_bandVertexCounts.Dispose();
			_bandVertexCounts = default;
		}
	}
}