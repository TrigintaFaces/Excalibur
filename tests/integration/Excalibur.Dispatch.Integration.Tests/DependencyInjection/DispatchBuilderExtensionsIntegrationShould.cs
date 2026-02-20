// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Transport.RabbitMQ;
using Excalibur.Dispatch.Transport.Kafka;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Azure;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Integration.Tests.DependencyInjection;

/// <summary>
/// Integration tests verifying that <c>IDispatchBuilder</c> extension methods
/// for transport and cross-cutting concerns delegate correctly to their
/// underlying service registration methods.
/// </summary>
/// <remarks>
/// Sprint 500 S500.5: Tests for builder-based transport and cross-cutting registration.
/// Validates S500.2 (bd-g87hd) and S500.4 (bd-onl97) acceptance criteria.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "DependencyInjection")]
public sealed class DispatchBuilderExtensionsIntegrationShould
{
	#region Transport Builder Extensions (S500.2 — AC-1, AC-2)

	[Fact]
	public void RegisterRabbitMQTransportServices_WhenUseRabbitMQCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.UseRabbitMQ(rmq => { });

		// Assert — RabbitMQ transport registration adds keyed services and RabbitMqMessageBus
		services.ShouldContain(d => d.ServiceType == typeof(RabbitMqMessageBus));
	}

	[Fact]
	public void RegisterKafkaTransportServices_WhenUseKafkaCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.UseKafka(kafka => { });

		// Assert — Kafka transport registration adds services
		services.Count.ShouldBeGreaterThan(1, "UseKafka() should add transport services to the collection");
	}

	[Fact]
	public void RegisterAwsSqsTransportServices_WhenUseAwsSqsCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.UseAwsSqs(sqs => { });

		// Assert
		services.Count.ShouldBeGreaterThan(1, "UseAwsSqs() should add transport services to the collection");
	}

	[Fact]
	public void RegisterAzureServiceBusTransportServices_WhenUseAzureServiceBusCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.UseAzureServiceBus(sb => { });

		// Assert
		services.Count.ShouldBeGreaterThan(1, "UseAzureServiceBus() should add transport services to the collection");
	}

	[Fact]
	public void RegisterGooglePubSubTransportServices_WhenUseGooglePubSubCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.UseGooglePubSub(pubsub => { });

		// Assert
		services.Count.ShouldBeGreaterThan(1, "UseGooglePubSub() should add transport services to the collection");
	}

	#endregion

	#region Named Transport Support (S500.2 — AC-2)

	[Fact]
	public void RegisterNamedRabbitMQTransport_WhenNamedOverloadUsed()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.UseRabbitMQ("payments", rmq => { });

		// Assert — named transport adds keyed services
		services.Count.ShouldBeGreaterThan(1, "UseRabbitMQ(name, configure) should add named transport services");
	}

	[Fact]
	public void RegisterNamedKafkaTransport_WhenNamedOverloadUsed()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.UseKafka("analytics", kafka => { });

		// Assert
		services.Count.ShouldBeGreaterThan(1, "UseKafka(name, configure) should add named transport services");
	}

	#endregion

	#region Fluent Chaining (S500.2 — AC-3)

	[Fact]
	public void ReturnSameBuilder_WhenUseRabbitMQCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.UseRabbitMQ(rmq => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ReturnSameBuilder_WhenUseKafkaCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.UseKafka(kafka => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ReturnSameBuilder_WhenUseAwsSqsCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.UseAwsSqs(sqs => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ReturnSameBuilder_WhenUseAzureServiceBusCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.UseAzureServiceBus(sb => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ReturnSameBuilder_WhenUseGooglePubSubCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.UseGooglePubSub(pubsub => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Standalone Transport Regression (S500.2 — AC-4)

	[Fact]
	public void ContinueToWork_WhenAddRabbitMQTransportCalledDirectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act — standalone registration (pre-Sprint 500 API)
		_ = services.AddRabbitMQTransport(rmq => { });

		// Assert — transport services should be registered without IDispatchBuilder
		services.ShouldContain(d => d.ServiceType == typeof(RabbitMqMessageBus));
	}

	[Fact]
	public void ContinueToWork_WhenAddKafkaTransportCalledDirectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddKafkaTransport(kafka => { });

		// Assert
		services.Count.ShouldBeGreaterThan(1, "AddKafkaTransport() standalone should add transport services");
	}

	#endregion

	#region Cross-Cutting Builder Extensions (S500.4 — AC-1 through AC-5)

	[Fact]
	public void RegisterObservabilityServices_WhenAddObservabilityCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddObservability();

		// Assert — observability adds context flow tracking services
		services.ShouldContain(d => d.ServiceType == typeof(IContextFlowTracker));
	}

	[Fact]
	public void ReturnSameBuilder_WhenAddObservabilityCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.AddObservability();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterResilienceServices_WhenAddResilienceCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddResilience();

		// Assert — resilience adds Polly services (circuit breaker factory, timeout manager, etc.)
		services.ShouldContain(d => d.ServiceType == typeof(ICircuitBreakerFactory));
	}

	[Fact]
	public void ReturnSameBuilder_WhenAddResilienceCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.AddResilience();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterCachingServices_WhenAddCachingCalledViaBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		_ = builder.AddCaching();

		// Assert — caching adds middleware and cache services
		services.ShouldContain(d => d.ServiceType == typeof(ICacheKeyBuilder));
	}

	[Fact]
	public void ReturnSameBuilder_WhenAddCachingCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.AddCaching();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Null Guard Tests

	[Fact]
	public void ThrowArgumentNullException_WhenNullBuilderCallsUseRabbitMQ()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseRabbitMQ(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNullBuilderCallsUseKafka()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseKafka(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNullBuilderCallsAddObservability()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).AddObservability());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNullBuilderCallsAddResilience()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).AddResilience());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNullBuilderCallsAddCaching()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).AddCaching());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenNullConfigurePassedToUseRabbitMQ()
	{
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseRabbitMQ((Action<IRabbitMQTransportBuilder>)null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenEmptyNamePassedToUseRabbitMQ()
	{
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		_ = Should.Throw<ArgumentException>(() =>
			builder.UseRabbitMQ("", _ => { }));
	}

	#endregion

	#region Helpers

	private static IDispatchBuilder CreateFakeDispatchBuilder(IServiceCollection services)
	{
		var builder = A.Fake<IDispatchBuilder>();
		_ = A.CallTo(() => builder.Services).Returns(services);
		return builder;
	}

	#endregion
}
