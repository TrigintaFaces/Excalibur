// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider;

/// <summary>
///     Base conformance test class for transport adapter implementations.
/// </summary>
/// <remarks>
///     This abstract class provides a comprehensive test suite for validating that transport adapter implementations conform to the
///     expected behavior and contracts defined by the ITransportAdapter interface. Concrete test classes should inherit from this class and
///     implement the factory methods to provide the specific transport adapter under test.
/// </remarks>
public abstract class TransportAdapterConformanceTests
{
	/// <summary>
	///     Gets the expected transport type for the adapter under test.
	/// </summary>
	protected abstract string ExpectedTransportType { get; }

	/// <summary>
	///     Gets the expected destination format for the adapter under test.
	/// </summary>
	protected abstract string TestDestination { get; }

	[Fact]
	public void NameShouldNotBeNullOrEmpty()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		adapter.Name.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void TransportTypeShouldMatchExpectedValue()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		adapter.TransportType.ShouldBe(ExpectedTransportType);
	}

	[Fact]
	public void IsRunningShouldBeFalseInitially()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		adapter.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public async Task StartAsyncShouldSetIsRunningToTrue()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		adapter.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task StopAsyncShouldSetIsRunningToFalse()
	{
		// Arrange
		var adapter = CreateAdapter();
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act
		await adapter.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		adapter.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public async Task StartAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.StartAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task StopAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.StopAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SendAsyncShouldAcceptValidMessage()
	{
		// Arrange
		var adapter = CreateAdapter();
		var message = CreateTestMessage();
		var destination = TestDestination;

		// Act & Assert
		await adapter.SendAsync(message, destination, CancellationToken.None).ConfigureAwait(false);
		// Should not throw
	}

	[Fact]
	public async Task SendAsyncShouldHandleNullMessage()
	{
		// Arrange
		var adapter = CreateAdapter();
		var destination = TestDestination;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => adapter.SendAsync(null!, destination, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SendAsyncShouldHandleNullDestination()
	{
		// Arrange
		var adapter = CreateAdapter();
		var message = CreateTestMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => adapter.SendAsync(message, null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SendAsyncShouldHandleEmptyDestination()
	{
		// Arrange
		var adapter = CreateAdapter();
		var message = CreateTestMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => adapter.SendAsync(message, "", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SendAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		var message = CreateTestMessage();
		var destination = TestDestination;
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.SendAsync(message, destination, cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReceiveAsyncShouldReturnValidResult()
	{
		// Arrange
		var adapter = CreateAdapter();
		var transportMessage = CreateTestTransportMessage();
		var dispatcher = CreateTestDispatcher();

		// Act
		var result = await adapter.ReceiveAsync(transportMessage, dispatcher, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReceiveAsyncShouldHandleNullTransportMessage()
	{
		// Arrange
		var adapter = CreateAdapter();
		var dispatcher = CreateTestDispatcher();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => adapter.ReceiveAsync(null!, dispatcher, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReceiveAsyncShouldHandleNullDispatcher()
	{
		// Arrange
		var adapter = CreateAdapter();
		var transportMessage = CreateTestTransportMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => adapter.ReceiveAsync(transportMessage, null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReceiveAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		var transportMessage = CreateTestTransportMessage();
		var dispatcher = CreateTestDispatcher();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.ReceiveAsync(transportMessage, dispatcher, cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task MultipleStartCallsShouldBeIdempotent()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false); // Second call should not throw

		// Assert
		adapter.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task MultipleStopCallsShouldBeIdempotent()
	{
		// Arrange
		var adapter = CreateAdapter();
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act
		await adapter.StopAsync(CancellationToken.None).ConfigureAwait(false);
		await adapter.StopAsync(CancellationToken.None).ConfigureAwait(false); // Second call should not throw

		// Assert
		adapter.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public async Task StopWithoutStartShouldNotThrow()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		await adapter.StopAsync(CancellationToken.None).ConfigureAwait(false);
		// Should not throw
		adapter.IsRunning.ShouldBeFalse();
	}

	/// <summary>
	///     Creates an instance of the transport adapter to be tested.
	/// </summary>
	/// <returns> The transport adapter instance under test. </returns>
	protected abstract ITransportAdapter CreateAdapter();

	/// <summary>
	///     Creates a test dispatcher for validation and message routing.
	/// </summary>
	/// <returns> Valid dispatcher for testing. </returns>
	protected abstract IDispatcher CreateTestDispatcher();

	/// <summary>
	///     Creates a test message for testing send operations.
	/// </summary>
	/// <returns> Valid dispatch message for testing. </returns>
	protected abstract IDispatchMessage CreateTestMessage();

	/// <summary>
	///     Creates test transport message for testing receive operations.
	/// </summary>
	/// <returns> Valid transport message for testing. </returns>
	protected abstract object CreateTestTransportMessage();
}
