// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Tests for <see cref="EventSourcingBuilderExtensions.EnableProjectionProcessing"/>
/// DI extension: service registrations, options configuration, ValidateOnStart,
/// fallback checkpoint store, idempotent hosted service registration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EnableProjectionProcessingShould
{
	[Fact]
	public void ThrowOnNullBuilder()
	{
		Should.Throw<ArgumentNullException>(() =>
			EventSourcingBuilderExtensions.EnableProjectionProcessing(null!));
	}

	[Fact]
	public void RegisterAsyncProjectionProcessingHost_AsHostedService()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.EnableProjectionProcessing();

		// Assert — AsyncProjectionProcessingHost should be registered as IHostedService
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IHostedService) &&
			d.ImplementationType?.Name == "AsyncProjectionProcessingHost");

		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterInMemoryCheckpointStore_AsFallback()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.EnableProjectionProcessing();

		// Assert — ISubscriptionCheckpointStore registered with TryAdd (fallback)
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(ISubscriptionCheckpointStore));

		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void NotReplaceExistingCheckpointStore()
	{
		// Arrange — register a custom checkpoint store before calling EnableProjectionProcessing
		var services = new ServiceCollection();
		var customStore = A.Fake<ISubscriptionCheckpointStore>();
		services.AddSingleton(customStore);

		var builder = CreateBuilder(services);

		// Act
		builder.EnableProjectionProcessing();

		// Assert — the custom store should still be the resolved instance (TryAdd didn't replace)
		var sp = services.BuildServiceProvider();
		var resolved = sp.GetRequiredService<ISubscriptionCheckpointStore>();
		resolved.ShouldBeSameAs(customStore);
	}

	[Fact]
	public void RegisterGlobalStreamProjectionOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.EnableProjectionProcessing();

		// Assert — options should be resolvable
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<GlobalStreamProjectionOptions>>();
		options.Value.ShouldNotBeNull();
		options.Value.BatchSize.ShouldBe(500); // default
	}

	[Fact]
	public void ApplyConfigureCallback_ToOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.EnableProjectionProcessing(opts =>
		{
			opts.BatchSize = 42;
			opts.IdlePollingInterval = TimeSpan.FromSeconds(5);
			opts.CheckpointInterval = 200;
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<GlobalStreamProjectionOptions>>();
		options.Value.BatchSize.ShouldBe(42);
		options.Value.IdlePollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.Value.CheckpointInterval.ShouldBe(200);
	}

	[Fact]
	public void UseDefaultOptions_WhenNoConfigureCallback()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.EnableProjectionProcessing();

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<GlobalStreamProjectionOptions>>();
		options.Value.BatchSize.ShouldBe(500);
		options.Value.IdlePollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
		options.Value.CheckpointInterval.ShouldBe(100);
		options.Value.ProjectionName.ShouldBe("AsyncProjectionProcessingHost");
	}

	[Fact]
	public void ReturnBuilderForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		var result = builder.EnableProjectionProcessing();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterHostedServiceIdempotently()
	{
		// Arrange — call EnableProjectionProcessing twice
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.EnableProjectionProcessing();
		builder.EnableProjectionProcessing();

		// Assert — only one IHostedService registration for AsyncProjectionProcessingHost
		var hostDescriptors = services.Where(d =>
			d.ServiceType == typeof(IHostedService) &&
			d.ImplementationType?.Name == "AsyncProjectionProcessingHost")
			.ToList();

		hostDescriptors.Count.ShouldBe(1);
	}

	// --- Helpers ---

	private static IEventSourcingBuilder CreateBuilder(IServiceCollection services)
	{
		var builder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		return builder;
	}
}
