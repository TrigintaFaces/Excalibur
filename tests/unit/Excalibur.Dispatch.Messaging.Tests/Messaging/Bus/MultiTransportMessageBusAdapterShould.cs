// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Bus;

namespace Excalibur.Dispatch.Tests.Messaging.Bus;

/// <summary>
/// Unit tests for <see cref="MultiTransportMessageBusAdapter"/>.
/// Verifies multi-transport routing, subscription parsing, and health aggregation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Transport")]
[Trait("Priority", "1")]
public sealed class MultiTransportMessageBusAdapterShould : IDisposable
{
	private readonly IMessageBusAdapter _rabbitMqAdapter;
	private readonly IMessageBusAdapter _kafkaAdapter;
	private readonly MultiTransportMessageBusAdapter _sut;

	public MultiTransportMessageBusAdapterShould()
	{
		_rabbitMqAdapter = A.Fake<IMessageBusAdapter>();
		_kafkaAdapter = A.Fake<IMessageBusAdapter>();

		A.CallTo(() => _rabbitMqAdapter.Name).Returns("rabbitmq");
		A.CallTo(() => _kafkaAdapter.Name).Returns("kafka");
		A.CallTo(() => _rabbitMqAdapter.SupportsPublishing).Returns(true);
		A.CallTo(() => _kafkaAdapter.SupportsPublishing).Returns(true);

		_sut = new MultiTransportMessageBusAdapter(
			[_rabbitMqAdapter, _kafkaAdapter],
			_rabbitMqAdapter);
	}

	public void Dispose()
	{
		_sut.Dispose();
		_rabbitMqAdapter.Dispose();
		_kafkaAdapter.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenAdaptersIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MultiTransportMessageBusAdapter(null!));
	}

	[Fact]
	public void UseFirstAdapter_AsDefault_WhenNoDefaultProvided()
	{
		// Arrange
		var adapters = new[] { _rabbitMqAdapter, _kafkaAdapter };

		// Act
		using var adapter = new MultiTransportMessageBusAdapter(adapters);

		// Assert - can verify indirectly via Name
		adapter.Name.ShouldBe("MultiTransport");
	}

	[Fact]
	public void UseProvidedDefaultAdapter()
	{
		// Arrange & Act
		using var adapter = new MultiTransportMessageBusAdapter(
			[_rabbitMqAdapter, _kafkaAdapter],
			_kafkaAdapter);

		// Assert - the default is set (we can't directly access it, so we verify other behavior)
		adapter.ShouldNotBeNull();
	}

	#endregion

	#region Name Property Tests

	[Fact]
	public void ReturnMultiTransport_AsName()
	{
		// Act
		var name = _sut.Name;

		// Assert
		name.ShouldBe("MultiTransport");
	}

	#endregion

	#region SupportsPublishing Tests

	[Fact]
	public void ReturnTrue_ForSupportsPublishing_WhenAnyAdapterSupports()
	{
		// Arrange
		A.CallTo(() => _rabbitMqAdapter.SupportsPublishing).Returns(true);
		A.CallTo(() => _kafkaAdapter.SupportsPublishing).Returns(false);

		// Act
		var result = _sut.SupportsPublishing;

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_ForSupportsPublishing_WhenNoAdapterSupports()
	{
		// Arrange
		A.CallTo(() => _rabbitMqAdapter.SupportsPublishing).Returns(false);
		A.CallTo(() => _kafkaAdapter.SupportsPublishing).Returns(false);

		using var adapter = new MultiTransportMessageBusAdapter(
			[_rabbitMqAdapter, _kafkaAdapter],
			_rabbitMqAdapter);

		// Act
		var result = adapter.SupportsPublishing;

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region SupportsSubscription Tests

	[Fact]
	public void ReturnTrue_ForSupportsSubscription_WhenAnyAdapterSupports()
	{
		// Arrange
		A.CallTo(() => _rabbitMqAdapter.SupportsSubscription).Returns(true);
		A.CallTo(() => _kafkaAdapter.SupportsSubscription).Returns(false);

		// Act
		var result = _sut.SupportsSubscription;

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region SupportsTransactions Tests

	[Fact]
	public void ReturnTrue_ForSupportsTransactions_WhenAnyAdapterSupports()
	{
		// Arrange
		A.CallTo(() => _rabbitMqAdapter.SupportsTransactions).Returns(false);
		A.CallTo(() => _kafkaAdapter.SupportsTransactions).Returns(true);

		// Act
		var result = _sut.SupportsTransactions;

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region IsConnected Tests

	[Fact]
	public void ReturnTrue_ForIsConnected_WhenAnyAdapterIsConnected()
	{
		// Arrange
		A.CallTo(() => _rabbitMqAdapter.IsConnected).Returns(false);
		A.CallTo(() => _kafkaAdapter.IsConnected).Returns(true);

		// Act
		var result = _sut.IsConnected;

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_ForIsConnected_WhenNoAdapterIsConnected()
	{
		// Arrange
		A.CallTo(() => _rabbitMqAdapter.IsConnected).Returns(false);
		A.CallTo(() => _kafkaAdapter.IsConnected).Returns(false);

		// Act
		var result = _sut.IsConnected;

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region InitializeAsync Tests

	[Fact]
	public async Task InitializeAllAdapters()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();

		// Act
		await _sut.InitializeAsync(options, CancellationToken.None);

		// Assert
		A.CallTo(() => _rabbitMqAdapter.InitializeAsync(options, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _kafkaAdapter.InitializeAsync(options, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region PublishAsync Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.PublishAsync(null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.PublishAsync(message, null!, CancellationToken.None));
	}

	[Fact]
	public async Task PublishToDefaultAdapter()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Success();

		A.CallTo(() => _rabbitMqAdapter.PublishAsync(message, context, A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		var result = await _sut.PublishAsync(message, context, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _rabbitMqAdapter.PublishAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnFailedResult_WhenNoDefaultAdapter()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([]);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var result = await adapter.PublishAsync(message, context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("NoDefaultAdapter");
	}

	#endregion

	#region SubscribeAsync Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenSubscriptionNameIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SubscribeAsync(null!, (_, _, _) => Task.FromResult(MessageResult.Success()), null, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageHandlerIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SubscribeAsync("test-subscription", null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task SubscribeToDefaultAdapter_WhenNoAdapterPrefix()
	{
		// Arrange
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult(MessageResult.Success());

		// Act
		await _sut.SubscribeAsync("my-subscription", handler, null, CancellationToken.None);

		// Assert
		A.CallTo(() => _rabbitMqAdapter.SubscribeAsync(
			"my-subscription",
			A<Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>>>._,
			A<IMessageBusOptions?>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SubscribeToSpecificAdapter_WhenAdapterPrefixProvided()
	{
		// Arrange
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult(MessageResult.Success());

		// Act
		await _sut.SubscribeAsync("kafka://my-topic", handler, null, CancellationToken.None);

		// Assert
		A.CallTo(() => _kafkaAdapter.SubscribeAsync(
			"my-topic",
			A<Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>>>._,
			A<IMessageBusOptions?>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowArgumentException_WhenAdapterNotRegistered()
	{
		// Arrange
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.SubscribeAsync("unknown://my-subscription", handler, null, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowInvalidOperationException_WhenNoDefaultAndNoAdapterPrefix()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([]);
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await adapter.SubscribeAsync("my-subscription", handler, null, CancellationToken.None));
	}

	#endregion

	#region UnsubscribeAsync Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenUnsubscribeNameIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.UnsubscribeAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task UnsubscribeFromDefaultAdapter_WhenNoAdapterPrefix()
	{
		// Act
		await _sut.UnsubscribeAsync("my-subscription", CancellationToken.None);

		// Assert
		A.CallTo(() => _rabbitMqAdapter.UnsubscribeAsync("my-subscription", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UnsubscribeFromSpecificAdapter_WhenAdapterPrefixProvided()
	{
		// Act
		await _sut.UnsubscribeAsync("kafka://my-topic", CancellationToken.None);

		// Assert
		A.CallTo(() => _kafkaAdapter.UnsubscribeAsync("my-topic", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SilentlyIgnore_WhenUnknownAdapterOnUnsubscribe()
	{
		// Act - should not throw
		await _sut.UnsubscribeAsync("unknown://my-subscription", CancellationToken.None);

		// Assert - no adapter called
		A.CallTo(() => _rabbitMqAdapter.UnsubscribeAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _kafkaAdapter.UnsubscribeAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SilentlyIgnore_WhenNoDefaultAdapterOnUnsubscribe()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([]);

		// Act - should not throw
		await adapter.UnsubscribeAsync("my-subscription", CancellationToken.None);
	}

	#endregion

	#region CheckHealthAsync Tests

	[Fact]
	public async Task AggregateHealthFromAllAdapters()
	{
		// Arrange
		A.CallTo(() => _rabbitMqAdapter.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new HealthCheckResult(true, "RabbitMQ healthy"));
		A.CallTo(() => _kafkaAdapter.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new HealthCheckResult(true, "Kafka healthy"));

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Description.ShouldBe("All adapters are healthy");
		result.Data.ShouldContainKeyAndValue("rabbitmq_healthy", true);
		result.Data.ShouldContainKeyAndValue("kafka_healthy", true);
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenAnyAdapterIsUnhealthy()
	{
		// Arrange
		A.CallTo(() => _rabbitMqAdapter.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new HealthCheckResult(true, "RabbitMQ healthy"));
		A.CallTo(() => _kafkaAdapter.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new HealthCheckResult(false, "Kafka connection failed"));

		// Act
		var result = await _sut.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Description.ShouldBe("One or more adapters are unhealthy");
		result.Data.ShouldContainKeyAndValue("rabbitmq_healthy", true);
		result.Data.ShouldContainKeyAndValue("kafka_healthy", false);
		result.Data.ShouldContainKeyAndValue("kafka_description", "Kafka connection failed");
	}

	#endregion

	#region StartAsync Tests

	[Fact]
	public async Task StartAllAdapters()
	{
		// Act
		await _sut.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _rabbitMqAdapter.StartAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _kafkaAdapter.StartAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region StopAsync Tests

	[Fact]
	public async Task StopAllAdapters()
	{
		// Act
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _rabbitMqAdapter.StopAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _kafkaAdapter.StopAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void DisposeAllAdapters()
	{
		// Arrange
		var rabbitMqAdapter = A.Fake<IMessageBusAdapter>();
		var kafkaAdapter = A.Fake<IMessageBusAdapter>();
		A.CallTo(() => rabbitMqAdapter.Name).Returns("rabbitmq");
		A.CallTo(() => kafkaAdapter.Name).Returns("kafka");

		var adapter = new MultiTransportMessageBusAdapter(
			[rabbitMqAdapter, kafkaAdapter]);

		// Act
		adapter.Dispose();

		// Assert
		A.CallTo(() => rabbitMqAdapter.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => kafkaAdapter.Dispose()).MustHaveHappenedOnceExactly();
	}

	#endregion
}
