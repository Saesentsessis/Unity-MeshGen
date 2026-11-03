using System;
using Unity.Burst;

namespace Saesentsessis.MeshGeneration.Shapes
{
	public readonly struct HollowConeMeshDescriptor : IEquatable<HollowConeMeshDescriptor>
	{
		public readonly int Resolution;
		public readonly float HalfAngleRadians;

		public HollowConeMeshDescriptor(int resolution, float halfAngleRadians)
		{
			Resolution = resolution;
			HalfAngleRadians = halfAngleRadians;
		}

		public int VertexCount => Resolution + 1;
		public int IndexCount => Resolution * 3;

		public bool Equals(HollowConeMeshDescriptor other)
		{
			return Resolution == other.Resolution && HalfAngleRadians.Equals(other.HalfAngleRadians);
		}

		[BurstDiscard]
		public override bool Equals(object obj)
		{
			return obj is HollowConeMeshDescriptor other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Resolution, HalfAngleRadians);
		}

		public static bool operator ==(HollowConeMeshDescriptor left, HollowConeMeshDescriptor right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(HollowConeMeshDescriptor left, HollowConeMeshDescriptor right)
		{
			return !left.Equals(right);
		}
	}
}