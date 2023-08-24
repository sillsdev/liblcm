using System;
using Newtonsoft.Json;
using SIL.Providers;

namespace SIL.LCModel.Utils
{
	public class HybridLogicalClock : IComparable
	{
		public readonly string Id;
		public readonly long DateTime;
		public readonly int EventCount;

		[JsonConstructor]
		public HybridLogicalClock()
		{
		}
		public HybridLogicalClock(string id, long dateTime)
		{
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("Node should be unique per client like a GUID", nameof(id));
			}
			Id = id;
			DateTime = dateTime;
			EventCount = 0;
		}
		public HybridLogicalClock(string id, long dateTime, int count) : this(id, dateTime)
		{
			EventCount = count;
		}

		public static HybridLogicalClock operator ++(HybridLogicalClock source)
		{
			var now = DateTimeProvider.Current.UtcNow.Ticks;
			if (now > source.DateTime)
			{
				return new HybridLogicalClock(source.Id, System.DateTime.Now.Ticks);
			}
			return new HybridLogicalClock(source.Id, now, source.EventCount + 1);
		}

		public static HybridLogicalClock Sync(HybridLogicalClock local, HybridLogicalClock remote)
		{
			var now = DateTimeProvider.Current.UtcNow.Ticks;
			if (now > local.DateTime && now > remote.DateTime)
			{
				return new HybridLogicalClock(local.Id, now);
			}

			if (local.DateTime == remote.DateTime)
			{
				return new HybridLogicalClock(local.Id, Math.Max(local.EventCount, remote.EventCount) + 1);
			}
			if (local.DateTime > remote.DateTime)
			{
				return new HybridLogicalClock(local.Id, local.DateTime, local.EventCount + 1);
			}

			return new HybridLogicalClock(local.Id, remote.DateTime, remote.EventCount + 1);
		}

		public int CompareTo(object obj)
		{
			var other = obj as HybridLogicalClock;
			if (other == null)
			{
				return 1;
			}
			if (DateTime == other.DateTime)
			{
				if (EventCount == other.EventCount)
				{
					return Id.CompareTo(other.Id);
				}

				return EventCount - other.EventCount;
			}

			return (int)(DateTime - other.DateTime);
		}

		public override string ToString()
		{
			return $"{Id}_{DateTime}_{EventCount}";
		}
	}
}
