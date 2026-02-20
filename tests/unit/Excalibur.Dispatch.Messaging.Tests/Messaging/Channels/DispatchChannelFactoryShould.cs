// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="DispatchChannelFactory"/>.
/// </summary>
/// <remarks>
/// Tests the dispatch channel factory.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
[Trait("Priority", "0")]
public sealed class DispatchChannelFactoryShould
{
	#region CreateUnbounded Tests

	[Fact]
	public void CreateUnbounded_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateUnbounded<int>();

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateUnbounded_WithSingleReader_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateUnbounded<string>(singleReader: true);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateUnbounded_WithSingleWriter_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateUnbounded<string>(singleWriter: true);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateUnbounded_WithBothSingle_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateUnbounded<string>(singleReader: true, singleWriter: true);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public async Task CreateUnbounded_AllowsWriteAndRead()
	{
		// Arrange
		using var channel = DispatchChannelFactory.CreateUnbounded<int>();

		// Act
		await channel.Writer.WriteAsync(42, CancellationToken.None);
		var result = await channel.Reader.ReadAsync(CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	#endregion

	#region CreateBounded Tests

	[Fact]
	public void CreateBounded_WithCapacity_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateBounded<int>(100);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithFullModeWait_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateBounded<int>(50, BoundedChannelFullMode.Wait);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithFullModeDropNewest_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateBounded<int>(50, BoundedChannelFullMode.DropNewest);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithFullModeDropOldest_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateBounded<int>(50, BoundedChannelFullMode.DropOldest);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithFullModeDropWrite_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateBounded<int>(50, BoundedChannelFullMode.DropWrite);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithSingleReader_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateBounded<int>(100, singleReader: true);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithSingleWriter_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateBounded<int>(100, singleWriter: true);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithAllOptions_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateBounded<int>(
			capacity: 100,
			fullMode: BoundedChannelFullMode.Wait,
			singleReader: true,
			singleWriter: true);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public async Task CreateBounded_AllowsWriteAndRead()
	{
		// Arrange
		using var channel = DispatchChannelFactory.CreateBounded<string>(10);

		// Act
		await channel.Writer.WriteAsync("test", CancellationToken.None);
		var result = await channel.Reader.ReadAsync(CancellationToken.None);

		// Assert
		result.ShouldBe("test");
	}

	[Fact]
	public void CreateBounded_WithOptions_ReturnsChannel()
	{
		// Arrange
		var options = new BoundedDispatchChannelOptions
		{
			Capacity = 50,
			FullMode = BoundedChannelFullMode.DropOldest,
		};

		// Act
		using var channel = DispatchChannelFactory.CreateBounded<int>(options);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	#endregion

	#region CreateSingleProducerConsumer Tests

	[Fact]
	public void CreateSingleProducerConsumer_WithoutCapacity_ReturnsUnboundedChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateSingleProducerConsumer<int>();

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSingleProducerConsumer_WithCapacity_ReturnsBoundedChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateSingleProducerConsumer<int>(capacity: 100);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public async Task CreateSingleProducerConsumer_AllowsWriteAndRead()
	{
		// Arrange
		using var channel = DispatchChannelFactory.CreateSingleProducerConsumer<string>(50);

		// Act
		await channel.Writer.WriteAsync("message", CancellationToken.None);
		var result = await channel.Reader.ReadAsync(CancellationToken.None);

		// Assert
		result.ShouldBe("message");
	}

	#endregion

	#region CreateCustom Tests

	[Fact]
	public void CreateCustom_WithOptions_ReturnsChannel()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
			SingleReader = true,
			SingleWriter = true,
		};

		// Act
		using var channel = DispatchChannelFactory.CreateCustom<int>(options);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCustom_WithBoundedOptions_ReturnsChannel()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 100,
			FullMode = BoundedChannelFullMode.Wait,
		};

		// Act
		using var channel = DispatchChannelFactory.CreateCustom<int>(options);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCustom_WithSpinWaitOptions_ReturnsChannel()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
		};
		var spinWaitOptions = new SpinWaitOptions
		{
			SpinCount = 10,
			DelayMilliseconds = 1,
		};

		// Act
		using var channel = DispatchChannelFactory.CreateCustom<int>(options, spinWaitOptions);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCustom_WithNullSpinWaitOptions_ReturnsChannel()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
		};

		// Act
		using var channel = DispatchChannelFactory.CreateCustom<int>(options, null);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCustom_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => DispatchChannelFactory.CreateCustom<int>(null!, null));
	}

	[Fact]
	public void CreateCustom_WithBoundedDispatchChannelOptions_ReturnsChannel()
	{
		// Arrange
		var boundedOptions = new BoundedDispatchChannelOptions
		{
			Capacity = 100,
			FullMode = BoundedChannelFullMode.DropOldest,
			SingleReader = true,
			SingleWriter = true,
		};
		var spinWaitOptions = new SpinWaitOptions
		{
			SpinCount = 5,
			DelayMilliseconds = 2,
		};

		// Act
		using var channel = DispatchChannelFactory.CreateCustom<string>(boundedOptions, spinWaitOptions);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCustom_WithBoundedOptionsAndNullSpinWait_ReturnsChannel()
	{
		// Arrange
		var boundedOptions = new BoundedDispatchChannelOptions
		{
			Capacity = 50,
		};

		// Act
		using var channel = DispatchChannelFactory.CreateCustom<int>(boundedOptions, null);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCustom_WithNullBoundedOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => DispatchChannelFactory.CreateCustom<int>(null as BoundedDispatchChannelOptions, null));
	}

	#endregion

	#region Type Parameter Tests

	[Fact]
	public void CreateUnbounded_WithReferenceType_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateUnbounded<object>();

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateUnbounded_WithValueType_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateUnbounded<Guid>();

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithCustomType_ReturnsChannel()
	{
		// Arrange & Act
		using var channel = DispatchChannelFactory.CreateBounded<TestMessage>(100);

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	#endregion

	private sealed record TestMessage(string Content);
}
