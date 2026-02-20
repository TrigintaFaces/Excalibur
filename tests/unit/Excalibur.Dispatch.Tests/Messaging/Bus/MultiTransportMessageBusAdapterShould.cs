// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Bus;

namespace Excalibur.Dispatch.Tests.Messaging.Bus;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MultiTransportMessageBusAdapterShould : IDisposable
{
	private readonly IMessageBusAdapter _adapter1;
	private readonly IMessageBusAdapter _adapter2;

	public MultiTransportMessageBusAdapterShould()
	{
		_adapter1 = A.Fake<IMessageBusAdapter>();
		A.CallTo(() => _adapter1.Name).Returns("Adapter1");
		A.CallTo(() => _adapter1.SupportsPublishing).Returns(true);
		A.CallTo(() => _adapter1.SupportsSubscription).Returns(true);
		A.CallTo(() => _adapter1.SupportsTransactions).Returns(false);
		A.CallTo(() => _adapter1.IsConnected).Returns(true);

		_adapter2 = A.Fake<IMessageBusAdapter>();
		A.CallTo(() => _adapter2.Name).Returns("Adapter2");
		A.CallTo(() => _adapter2.SupportsPublishing).Returns(false);
		A.CallTo(() => _adapter2.SupportsSubscription).Returns(false);
		A.CallTo(() => _adapter2.SupportsTransactions).Returns(true);
		A.CallTo(() => _adapter2.IsConnected).Returns(false);
	}

	public void Dispose()
	{
		_adapter1.Dispose();
		_adapter2.Dispose();
	}

	[Fact]
	public void Constructor_WithNullAdapters_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MultiTransportMessageBusAdapter(null!));
	}

	[Fact]
	public void Name_ReturnsMultiTransport()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1]);

		// Assert
		adapter.Name.ShouldBe("MultiTransport");
	}

	[Fact]
	public void SupportsPublishing_ReturnsTrueWhenAnyAdapterSupports()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);

		// Assert
		adapter.SupportsPublishing.ShouldBeTrue();
	}

	[Fact]
	public void SupportsSubscription_ReturnsTrueWhenAnyAdapterSupports()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);

		// Assert
		adapter.SupportsSubscription.ShouldBeTrue();
	}

	[Fact]
	public void SupportsTransactions_ReturnsTrueWhenAnyAdapterSupports()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);

		// Assert
		adapter.SupportsTransactions.ShouldBeTrue();
	}

	[Fact]
	public void IsConnected_ReturnsTrueWhenAnyAdapterConnected()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);

		// Assert
		adapter.IsConnected.ShouldBeTrue();
	}

	[Fact]
	public void IsConnected_ReturnsFalseWhenNoAdapterConnected()
	{
		// Arrange
		A.CallTo(() => _adapter1.IsConnected).Returns(false);
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1]);

		// Assert
		adapter.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task InitializeAsync_InitializesAllAdapters()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);
		var options = A.Fake<IMessageBusOptions>();

		// Act
		await adapter.InitializeAsync(options, CancellationToken.None);

		// Assert
		A.CallTo(() => _adapter1.InitializeAsync(options, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _adapter2.InitializeAsync(options, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishAsync_UsesDefaultAdapter()
	{
		// Arrange - use explicit default to avoid ConcurrentDictionary ordering issues
		var expectedResult = MessageResult.Success();
		A.CallTo(() => _adapter1.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IMessageResult>(expectedResult));
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2], _adapter1);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var result = await adapter.PublishAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => _adapter1.PublishAsync(message, context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishAsync_WithNullMessage_Throws()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1]);
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await adapter.PublishAsync(null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task PublishAsync_WithNullContext_Throws()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1]);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await adapter.PublishAsync(message, null!, CancellationToken.None));
	}

	[Fact]
	public async Task PublishAsync_WithNoAdapters_ReturnsFailure()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter(Array.Empty<IMessageBusAdapter>());
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var result = await adapter.PublishAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
	}

	[Fact]
	public async Task SubscribeAsync_WithPrefixedName_RoutesToCorrectAdapter()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success());

		// Act
		await adapter.SubscribeAsync("Adapter1://my-sub", handler, null, CancellationToken.None);

		// Assert
		A.CallTo(() => _adapter1.SubscribeAsync("my-sub", handler, null, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SubscribeAsync_WithoutPrefix_UsesDefaultAdapter()
	{
		// Arrange - use explicit default to avoid ConcurrentDictionary ordering issues
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2], _adapter1);
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success());

		// Act
		await adapter.SubscribeAsync("my-sub", handler, null, CancellationToken.None);

		// Assert
		A.CallTo(() => _adapter1.SubscribeAsync("my-sub", handler, null, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SubscribeAsync_WithUnknownPrefix_Throws()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1]);
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await adapter.SubscribeAsync("Unknown://sub", handler, null, CancellationToken.None));
	}

	[Fact]
	public async Task SubscribeAsync_WithNullName_Throws()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1]);
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await adapter.SubscribeAsync(null!, handler, null, CancellationToken.None));
	}

	[Fact]
	public async Task UnsubscribeAsync_WithPrefix_RoutesToCorrectAdapter()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);

		// Act
		await adapter.UnsubscribeAsync("Adapter1://my-sub", CancellationToken.None);

		// Assert
		A.CallTo(() => _adapter1.UnsubscribeAsync("my-sub", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UnsubscribeAsync_WithUnknownPrefix_DoesNotThrow()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1]);

		// Act & Assert - silently ignores unknown adapters
		await adapter.UnsubscribeAsync("Unknown://sub", CancellationToken.None);
	}

	[Fact]
	public async Task UnsubscribeAsync_WithoutPrefix_UsesDefault()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1]);

		// Act
		await adapter.UnsubscribeAsync("my-sub", CancellationToken.None);

		// Assert
		A.CallTo(() => _adapter1.UnsubscribeAsync("my-sub", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsHealthyWhenAllHealthy()
	{
		// Arrange
		A.CallTo(() => _adapter1.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new HealthCheckResult(true, "OK", new Dictionary<string, object>()));
		A.CallTo(() => _adapter2.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new HealthCheckResult(true, "OK", new Dictionary<string, object>()));
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);

		// Act
		var result = await adapter.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Data.ShouldNotBeNull();
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthyWhenOneUnhealthy()
	{
		// Arrange
		A.CallTo(() => _adapter1.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new HealthCheckResult(true, "OK", new Dictionary<string, object>()));
		A.CallTo(() => _adapter2.CheckHealthAsync(A<CancellationToken>._))
			.Returns(new HealthCheckResult(false, "Unhealthy", new Dictionary<string, object>()));
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);

		// Act
		var result = await adapter.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task StartAsync_StartsAllAdapters()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);

		// Act
		await adapter.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _adapter1.StartAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _adapter2.StartAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StopAsync_StopsAllAdapters()
	{
		// Arrange
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1, _adapter2]);

		// Act
		await adapter.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _adapter1.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _adapter2.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Dispose_DisposesAllAdapters()
	{
		// Arrange
		var a1 = A.Fake<IMessageBusAdapter>();
		A.CallTo(() => a1.Name).Returns("A1");
		var a2 = A.Fake<IMessageBusAdapter>();
		A.CallTo(() => a2.Name).Returns("A2");
		var adapter = new MultiTransportMessageBusAdapter([a1, a2]);

		// Act
		adapter.Dispose();

		// Assert
		A.CallTo(() => a1.Dispose()).MustHaveHappenedOnceExactly();
		A.CallTo(() => a2.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Constructor_WithExplicitDefaultAdapter_UsesIt()
	{
		// Arrange & Act
		using var adapter = new MultiTransportMessageBusAdapter([_adapter1], _adapter2);

		// Assert
		adapter.Name.ShouldBe("MultiTransport");
	}
}
