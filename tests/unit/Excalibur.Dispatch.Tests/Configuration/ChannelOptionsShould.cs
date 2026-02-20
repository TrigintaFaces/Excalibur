// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelOptionsShould
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
		// Arrange
		using var strategy = new SpinWaitStrategy();

		// Act
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 500,
			FullMode = BoundedChannelFullMode.DropOldest,
			SingleReader = true,
			SingleWriter = true,
			AllowSynchronousContinuations = false,
			WaitStrategy = strategy,
		};

		// Assert
		options.Mode.ShouldBe(ChannelMode.Bounded);
		options.Capacity.ShouldBe(500);
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropOldest);
		options.SingleReader.ShouldBeTrue();
		options.SingleWriter.ShouldBeTrue();
		options.AllowSynchronousContinuations.ShouldBeFalse();
		options.WaitStrategy.ShouldBe(strategy);
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

	[Fact]
	public void ChannelMessagePumpOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new ChannelMessagePumpOptions
		{
			Capacity = 200,
			FullMode = BoundedChannelFullMode.DropNewest,
			AllowSynchronousContinuations = true,
			ConcurrentConsumers = 4,
			SingleReader = true,
			SingleWriter = true,
			BatchSize = 50,
			BatchTimeoutMs = 5000,
			EnableMetrics = false,
			PrefetchCount = 100,
		};

		// Assert
		options.Capacity.ShouldBe(200);
		options.FullMode.ShouldBe(BoundedChannelFullMode.DropNewest);
		options.AllowSynchronousContinuations.ShouldBeTrue();
		options.ConcurrentConsumers.ShouldBe(4);
		options.SingleReader.ShouldBeTrue();
		options.SingleWriter.ShouldBeTrue();
		options.BatchSize.ShouldBe(50);
		options.BatchTimeoutMs.ShouldBe(5000);
		options.EnableMetrics.ShouldBeFalse();
		options.PrefetchCount.ShouldBe(100);
	}
}
