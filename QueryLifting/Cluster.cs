using System;

namespace QueryLifting
{
	public class ClusterAttribute : Attribute
	{
	}

	[Cluster]
	public struct Cluster<T>
	{
		public readonly T Value;

		public Cluster(T value) => Value = value;
	}

	public static class ClusterExtensions
	{
		public static Cluster<T> Cluster<T>(this T it) => new Cluster<T>(it);
	}
}