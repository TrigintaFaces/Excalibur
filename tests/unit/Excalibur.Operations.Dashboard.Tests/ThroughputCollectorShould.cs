// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Operations.Dashboard.Throughput;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ThroughputCollectorShould
{
	[Fact]
	public void ReportZeroRateOnFirstReadToEstablishBaseline()
	{
		var time = new ControllableTimeProvider();
		using var collector = new ThroughputCollector(time);

		var reading = collector.Read("outbox");

		reading.RatePerSecond.ShouldBe(0d);
		reading.WindowSeconds.ShouldBe(0d);
	}

	[Fact]
	public void ComputeRateAsCounterDeltaOverElapsedWindow()
	{
		var time = new ControllableTimeProvider();
		using var collector = new ThroughputCollector(time);
		using var meter = new Meter("Excalibur.Outbox.Store");
		var counter = meter.CreateCounter<long>("excalibur.outbox.store.messages");

		// Baseline read.
		_ = collector.Read("outbox");

		// 10 messages over a 2-second window => 5/s.
		counter.Add(10);
		time.Advance(TimeSpan.FromSeconds(2));

		var reading = collector.Read("outbox");

		reading.RatePerSecond.ShouldBe(5d, 0.0001d);
		reading.WindowSeconds.ShouldBe(2d, 0.0001d);
	}

	[Fact]
	public void AccumulateAcrossMultipleInstrumentsOfTheSameSubsystem()
	{
		var time = new ControllableTimeProvider();
		using var collector = new ThroughputCollector(time);
		using var meter = new Meter("Excalibur.Saga");
		var a = meter.CreateCounter<long>("excalibur.saga.a");
		var b = meter.CreateCounter<long>("excalibur.saga.b");

		_ = collector.Read("saga");

		a.Add(3);
		b.Add(1);
		time.Advance(TimeSpan.FromSeconds(1));

		collector.Read("saga").RatePerSecond.ShouldBe(4d, 0.0001d);
	}

	[Fact]
	public void IgnoreMetersOutsideTheKnownSubsystemSet()
	{
		var time = new ControllableTimeProvider();
		using var collector = new ThroughputCollector(time);
		using var meter = new Meter("Some.Unrelated.Meter");
		var counter = meter.CreateCounter<long>("unrelated.count");

		_ = collector.Read("outbox");
		counter.Add(100);
		time.Advance(TimeSpan.FromSeconds(1));

		collector.Read("outbox").RatePerSecond.ShouldBe(0d);
	}

	[Fact]
	public void ReportZeroWhenElapsedWindowIsZero()
	{
		var time = new ControllableTimeProvider();
		using var collector = new ThroughputCollector(time);
		using var meter = new Meter("Excalibur.Inbox");
		var counter = meter.CreateCounter<long>("excalibur.inbox.messages");

		_ = collector.Read("inbox");
		counter.Add(50);
		// No time advance => window is zero => no divide-by-zero, rate 0.

		collector.Read("inbox").RatePerSecond.ShouldBe(0d);
	}

	[Theory]
	[InlineData("outbox", true)]
	[InlineData("inbox", true)]
	[InlineData("saga", true)]
	[InlineData("dlq", false)]
	[InlineData("bogus", false)]
	public void RecognizeOnlyBridgedSubsystems(string subsystem, bool expected) =>
		ThroughputCollector.IsKnownSubsystem(subsystem).ShouldBe(expected);

	/// <summary>A <see cref="TimeProvider"/> whose timestamp only advances when the test tells it to.</summary>
	private sealed class ControllableTimeProvider : TimeProvider
	{
		private long _timestamp = 1_000_000;

		public override long TimestampFrequency => TimeSpan.TicksPerSecond;

		public override long GetTimestamp() => _timestamp;

		public void Advance(TimeSpan by) => _timestamp += by.Ticks;
	}
}
