// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelsOptionsShould
{
	// --- DispatchChannelOptions ---

	[Fact]
	public void DispatchChannelOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new DispatchChannelOptions();

		// Assert
		options.Mode.ShouldBe(ChannelMode.Unbounded);
		options.Capacity.ShouldBeNull();
		options.FullMode.ShouldBe(BoundedChannelFullMode.Wait);
		options.SingleReader.ShouldBeFalse();
		options.SingleWriter.ShouldBeFalse();
		options.AllowSynchronousContinuations.ShouldBeTrue();
		options.WaitStrategy.ShouldBeNull();
	}

	[Fact]
	public void DispatchChannelOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 500,
			FullMode = BoundedChannelFullMode.DropOldest,
			SingleReader = true,
			SingleWriter = true,
			AllowSynchronousContinuations = false,
		};

		// Assert
		options.Mode.ShouldBe(ChannelMode.Bounded);
		options.Capacity.ShouldBe(500);
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropOldest);
		options.SingleReader.ShouldBeTrue();
		options.SingleWriter.ShouldBeTrue();
		options.AllowSynchronousContinuations.ShouldBeFalse();
	}

	// --- BoundedDispatchChannelOptions ---

	[Fact]
	public void BoundedDispatchChannelOptions_DefaultConstructor_SetsDefaults()
	{
		// Act
		var options = new BoundedDispatchChannelOptions();

		// Assert
		options.Mode.ShouldBe(ChannelMode.Bounded);
		options.Capacity.ShouldBe(100);
		options.WriterWaitStrategy.ShouldBeNull();
		options.ReaderWaitStrategy.ShouldBeNull();
		options.AggressiveSpinning.ShouldBeFalse();
		options.SpinCount.ShouldBe(100);
	}

	[Fact]
	public void BoundedDispatchChannelOptions_CapacityConstructor_SetsCapacity()
	{
		// Act
		var options = new BoundedDispatchChannelOptions(500);

		// Assert
		options.Mode.ShouldBe(ChannelMode.Bounded);
		options.Capacity.ShouldBe(500);
	}

	// --- ChannelMessagePumpOptions ---

	[Fact]
	public void ChannelMessagePumpOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new ChannelMessagePumpOptions();

		// Assert
		options.Capacity.ShouldBe(100);
		options.FullMode.ShouldBe(BoundedChannelFullMode.Wait);
		options.AllowSynchronousContinuations.ShouldBeFalse();
		options.ConcurrentConsumers.ShouldBe(1);
		options.SingleReader.ShouldBeFalse();
		options.SingleWriter.ShouldBeFalse();
		options.BatchSize.ShouldBe(10);
		options.BatchTimeoutMs.ShouldBe(1000);
		options.EnableMetrics.ShouldBeTrue();
		options.PrefetchCount.ShouldBe(20);
	}

	// --- SpinWaitOptions ---

	[Fact]
	public void SpinWaitOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new SpinWaitOptions();

		// Assert
		options.SpinCount.ShouldBe(10);
		options.DelayMilliseconds.ShouldBe(1);
		options.AggressiveSpin.ShouldBeFalse();
		options.SpinIterations.ShouldBe(100);
		options.MaxSpinCycles.ShouldBe(10);
	}

	[Fact]
	public void SpinWaitOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new SpinWaitOptions
		{
			SpinCount = 50,
			DelayMilliseconds = 5,
			AggressiveSpin = true,
			SpinIterations = 200,
			MaxSpinCycles = 20,
		};

		// Assert
		options.SpinCount.ShouldBe(50);
		options.DelayMilliseconds.ShouldBe(5);
		options.AggressiveSpin.ShouldBeTrue();
		options.SpinIterations.ShouldBe(200);
		options.MaxSpinCycles.ShouldBe(20);
	}
}
