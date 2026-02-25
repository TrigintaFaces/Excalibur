// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider;

/// <summary>
///     Base conformance test class for message bus adapter implementations.
/// </summary>
/// <remarks>
///     This abstract class provides a comprehensive test suite for validating that message bus adapter implementations conform to the
///     expected behavior and contracts defined by the IMessageBusAdapter interface. Concrete test classes should inherit from this class
///     and implement the factory methods to provide the specific message bus adapter under test.
/// </remarks>
public abstract class MessageBusAdapterConformanceTests
{
	/// <summary>
	///     Gets the expected adapter name for the adapter under test.
	/// </summary>
	protected abstract string ExpectedAdapterName { get; }

	/// <summary>
	///     Gets a value indicating whether the adapter should support publishing operations.
	/// </summary>
	protected virtual bool ExpectedSupportsPublishing => true;

	/// <summary>
	///     Gets a value indicating whether the adapter should support subscription operations.
	/// </summary>
	protected virtual bool ExpectedSupportsSubscription => true;

	/// <summary>
	///     Gets a value indicating whether the adapter should support transactional operations.
	/// </summary>
	protected virtual bool ExpectedSupportsTransactions => false;

	/// <summary>
	///     Gets the test subscription name to use in tests.
	/// </summary>
	protected virtual string TestSubscriptionName => "test-subscription";

	[Fact]
	public void NameShouldNotBeNullOrEmpty()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		adapter.Name.ShouldNotBeNullOrEmpty();
		adapter.Name.ShouldBe(ExpectedAdapterName);
	}

	[Fact]
	public void SupportsPublishingShouldMatchExpectedValue()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		adapter.SupportsPublishing.ShouldBe(ExpectedSupportsPublishing);
	}

	[Fact]
	public void SupportsSubscriptionShouldMatchExpectedValue()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		adapter.SupportsSubscription.ShouldBe(ExpectedSupportsSubscription);
	}

	[Fact]
	public void SupportsTransactionsShouldMatchExpectedValue()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		adapter.SupportsTransactions.ShouldBe(ExpectedSupportsTransactions);
	}

	[Fact]
	public void IsConnectedShouldBeFalseInitially()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		adapter.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task InitializeAsyncShouldSucceedWithValidOptions()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();

		// Act & Assert
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		// Should not throw
	}

	[Fact]
	public async Task InitializeAsyncShouldHandleNullOptions()
	{
		// Arrange
		var adapter = CreateAdapter();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => adapter.InitializeAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task InitializeAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.InitializeAsync(options, cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task StartAsyncShouldSucceed()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		// Should not throw
	}

	[Fact]
	public async Task StartAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.StartAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task StopAsyncShouldSucceed()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		await adapter.StopAsync(CancellationToken.None).ConfigureAwait(false);
		// Should not throw
	}

	[Fact]
	public async Task StopAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.StopAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task MultipleStartCallsShouldBeIdempotent()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);

		// Act
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false); // Second call should not throw

		// Assert Should not throw
	}

	[Fact]
	public async Task MultipleStopCallsShouldBeIdempotent()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act
		await adapter.StopAsync(CancellationToken.None).ConfigureAwait(false);
		await adapter.StopAsync(CancellationToken.None).ConfigureAwait(false); // Second call should not throw

		// Assert Should not throw
	}

	[Fact]
	public async Task StopWithoutStartShouldNotThrow()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		await adapter.StopAsync(CancellationToken.None).ConfigureAwait(false);
		// Should not throw
	}

	[Fact]
	public async Task PublishAsyncShouldSucceedWhenSupported()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var message = CreateTestMessage();
		var context = CreateTestMessageContext();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		if (adapter.SupportsPublishing)
		{
			var result = await adapter.PublishAsync(message, context, CancellationToken.None).ConfigureAwait(false);
			_ = result.ShouldNotBeNull();
		}
		else
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(() => adapter.PublishAsync(message, context, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task PublishAsyncShouldHandleNullMessage()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var context = CreateTestMessageContext();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		if (adapter.SupportsPublishing)
		{
			_ = await Should.ThrowAsync<ArgumentNullException>(() => adapter.PublishAsync(null!, context, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task PublishAsyncShouldHandleNullContext()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var message = CreateTestMessage();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		if (adapter.SupportsPublishing)
		{
			_ = await Should.ThrowAsync<ArgumentNullException>(() => adapter.PublishAsync(message, null!, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task PublishAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var message = CreateTestMessage();
		var context = CreateTestMessageContext();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		if (adapter.SupportsPublishing)
		{
			_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.PublishAsync(message, context, cts.Token)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task SubscribeAsyncShouldSucceedWhenSupported()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var subscriptionName = TestSubscriptionName;
		var messageHandler = CreateTestMessageHandler();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		if (adapter.SupportsSubscription)
		{
			await adapter.SubscribeAsync(subscriptionName, messageHandler, options, CancellationToken.None).ConfigureAwait(false);
			// Should not throw
		}
		else
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(() =>
				adapter.SubscribeAsync(subscriptionName, messageHandler, options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task SubscribeAsyncShouldHandleNullSubscriptionName()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var messageHandler = CreateTestMessageHandler();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		if (adapter.SupportsSubscription)
		{
			_ = await Should.ThrowAsync<ArgumentException>(() =>
				adapter.SubscribeAsync(null!, messageHandler, options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task SubscribeAsyncShouldHandleEmptySubscriptionName()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var messageHandler = CreateTestMessageHandler();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		if (adapter.SupportsSubscription)
		{
			_ = await Should.ThrowAsync<ArgumentException>(() => adapter.SubscribeAsync("", messageHandler, options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task SubscribeAsyncShouldHandleNullMessageHandler()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var subscriptionName = TestSubscriptionName;
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		if (adapter.SupportsSubscription)
		{
			_ = await Should.ThrowAsync<ArgumentNullException>(() =>
				adapter.SubscribeAsync(subscriptionName, null!, options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task SubscribeAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var subscriptionName = TestSubscriptionName;
		var messageHandler = CreateTestMessageHandler();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		if (adapter.SupportsSubscription)
		{
			_ = await Should.ThrowAsync<OperationCanceledException>(() =>
				adapter.SubscribeAsync(subscriptionName, messageHandler, options, cts.Token)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task UnsubscribeAsyncShouldSucceed()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var subscriptionName = TestSubscriptionName;
		var messageHandler = CreateTestMessageHandler();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		if (adapter.SupportsSubscription)
		{
			await adapter.SubscribeAsync(subscriptionName, messageHandler, options, CancellationToken.None).ConfigureAwait(false);
		}

		// Act & Assert
		await adapter.UnsubscribeAsync(subscriptionName, CancellationToken.None).ConfigureAwait(false);
		// Should not throw
	}

	[Fact]
	public async Task UnsubscribeAsyncShouldHandleNullSubscriptionName()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => adapter.UnsubscribeAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task UnsubscribeAsyncShouldHandleEmptySubscriptionName()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() => adapter.UnsubscribeAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task UnsubscribeAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		var subscriptionName = TestSubscriptionName;
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.UnsubscribeAsync(subscriptionName, cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task CheckHealthAsyncShouldReturnValidResult()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await adapter.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBeOneOf(HealthCheckStatus.Healthy, HealthCheckStatus.Degraded, HealthCheckStatus.Unhealthy);
	}

	[Fact]
	public async Task CheckHealthAsyncShouldHandleCancellation()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => adapter.CheckHealthAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DisposeShouldNotThrow()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		adapter.Dispose();
		// Should not throw
	}

	[Fact]
	public async Task DisposeAfterStopShouldNotThrow()
	{
		// Arrange
		var adapter = CreateAdapter();
		var options = CreateTestOptions();
		await adapter.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		await adapter.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await adapter.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		adapter.Dispose();
		// Should not throw
	}

	/// <summary>
	///     Creates an instance of the message bus adapter to be tested.
	/// </summary>
	/// <returns> The message bus adapter instance under test. </returns>
	protected abstract IMessageBusAdapter CreateAdapter();

	/// <summary>
	///     Creates test message bus options for adapter initialization and configuration.
	/// </summary>
	/// <returns> Valid message bus options for testing. </returns>
	protected abstract IMessageBusOptions CreateTestOptions();

	/// <summary>
	///     Creates a test message for testing publish operations.
	/// </summary>
	/// <returns> Valid dispatch message for testing. </returns>
	protected abstract IDispatchMessage CreateTestMessage();

	/// <summary>
	///     Creates a test message context for testing message operations.
	/// </summary>
	/// <returns> Valid message context for testing. </returns>
	protected abstract IMessageContext CreateTestMessageContext();

	/// <summary>
	///     Creates a test message handler for subscription testing.
	/// </summary>
	/// <returns> A valid message handler function for testing. </returns>
	protected virtual Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> CreateTestMessageHandler() =>
		async (message, context, cancellationToken) =>
		{
			await Task.Delay(10, cancellationToken).ConfigureAwait(false);
			return MessageResult.Success();
		};
}
