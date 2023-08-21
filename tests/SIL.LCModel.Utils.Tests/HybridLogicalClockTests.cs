using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using SIL.Providers;
using SIL.TestUtilities.Providers;

namespace SIL.LCModel.Utils
{
	public class HybridLogicalClockTests
	{

		[Test]
		public void IncrementOnSameDateBumps()
		{
			DateTimeProvider.SetProvider(new ReproducibleDateTimeProvider(DateTime.UtcNow));
			try
			{
				var hlc = new HybridLogicalClock("a", DateTimeProvider.Current.UtcNow.Ticks);
				Assert.That(hlc.EventCount, Is.EqualTo(0));
				hlc++;
				Assert.That(hlc.EventCount, Is.EqualTo(1));
			}
			finally
			{
				DateTimeProvider.ResetToDefault();;
			}
		}

		[Test]
		public void IncrementOnNewDateSetsCountToZero()
		{
			var now = DateTime.UtcNow;
			DateTimeProvider.SetProvider(new ReproducibleDateTimeProvider(now));
			try
			{
				var hlc = new HybridLogicalClock("a", DateTimeProvider.Current.UtcNow.Ticks);
				Assert.That(hlc.EventCount, Is.EqualTo(0));
				// tick the clock the minimum possible time interval
				DateTimeProvider.SetProvider(new ReproducibleDateTimeProvider(now + TimeSpan.FromTicks(1)));
				hlc++;
				Assert.That(hlc.EventCount, Is.EqualTo(0));
			}
			finally
			{
				DateTimeProvider.ResetToDefault(); ;
			}
		}
		[Test]
		public void CompareToDate()
		{
			var now = DateTime.UtcNow;
			DateTimeProvider.SetProvider(new ReproducibleDateTimeProvider(now));
			try
			{
				var hlc = new HybridLogicalClock("a", DateTimeProvider.Current.UtcNow.Ticks);
				// tick the clock the minimum possible time interval
				DateTimeProvider.SetProvider(new ReproducibleDateTimeProvider(now + TimeSpan.FromTicks(1)));
				var hlcNext = new HybridLogicalClock("a", DateTimeProvider.Current.UtcNow.Ticks);
				Assert.That(hlc.CompareTo(hlcNext), Is.EqualTo(-1));
			}
			finally
			{
				DateTimeProvider.ResetToDefault(); ;
			}
		}
		[Test]
		public void CompareToCount()
		{
			var now = DateTime.UtcNow;
			DateTimeProvider.SetProvider(new ReproducibleDateTimeProvider(now));
			try
			{
				var hlc = new HybridLogicalClock("a", DateTimeProvider.Current.UtcNow.Ticks);
				var hlcNext = new HybridLogicalClock("a", DateTimeProvider.Current.UtcNow.Ticks, 1);
				Assert.That(hlc.CompareTo(hlcNext), Is.EqualTo(-1));
			}
			finally
			{
				DateTimeProvider.ResetToDefault(); ;
			}
		}
		[Test]
		public void CompareToId()
		{
			var now = DateTime.UtcNow;
			DateTimeProvider.SetProvider(new ReproducibleDateTimeProvider(now));
			try
			{
				var hlc = new HybridLogicalClock("a", DateTimeProvider.Current.UtcNow.Ticks);
				var hlcNext = new HybridLogicalClock("b", DateTimeProvider.Current.UtcNow.Ticks);
				Assert.That(hlc.CompareTo(hlcNext), Is.EqualTo(-1));
			}
			finally
			{
				DateTimeProvider.ResetToDefault(); ;
			}
		}
		[Test]
		public void CompareToNull()
		{
			var now = DateTime.UtcNow;
			DateTimeProvider.SetProvider(new ReproducibleDateTimeProvider(now));
			try
			{
				var hlc = new HybridLogicalClock("a", DateTimeProvider.Current.UtcNow.Ticks);
				Assert.That(hlc.CompareTo(null), Is.EqualTo(1));
			}
			finally
			{
				DateTimeProvider.ResetToDefault(); ;
			}
		}

		[Test]
		public void NullOrEmptyIdThrows()
		{
			Assert.Throws<ArgumentException>(() => new HybridLogicalClock(null, DateTimeProvider.Current.UtcNow.Ticks));
			Assert.Throws<ArgumentException>(() => new HybridLogicalClock(string.Empty, DateTimeProvider.Current.UtcNow.Ticks));
		}

		[Test]
		public void SyncReturnsLocalIdOnIdenticalTime()
		{
			var now = DateTime.UtcNow;
			DateTimeProvider.SetProvider(new ReproducibleDateTimeProvider(now));
			try
			{
				var hlc = HybridLogicalClock.Sync(
					new HybridLogicalClock("a", DateTimeProvider.Current.UtcNow.Ticks),
					new HybridLogicalClock("b", DateTimeProvider.Current.UtcNow.Ticks));
				Assert.That(hlc.Id, Is.EqualTo("a"));
			}
			finally
			{
				DateTimeProvider.ResetToDefault(); ;
			}
		}
	}
}
