// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.CosmosDb;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="EventSourcingBuilderCosmosDbExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingBuilderCosmosDbExtensionsShould
{
	private static IEventSourcingBuilder CreateBuilder(ServiceCollection? services = null)
	{
		var svc = services ?? new ServiceCollection();
		return new ExcaliburEventSourcingBuilder(svc);
	}

	#region UseCosmosDb(Action<CosmosDbEventStoreOptions>) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConfigureOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).UseCosmosDb((Action<CosmosDbEventStoreOptions>)(_ => { })));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCosmosDb((Action<CosmosDbEventStoreOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_ConfigureOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseCosmosDb(opts => { opts.EventsContainerName = "events"; });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterOptions_WhenCalledWithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UseCosmosDb(opts => { opts.EventsContainerName = "my-events"; });

		// Assert -- options are registered via deferred configuration
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<Microsoft.Extensions.Options.IOptions<CosmosDbEventStoreOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterEventStore_WhenCalledWithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UseCosmosDb(opts => { opts.EventsContainerName = "events"; });

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
	}

	#endregion

	#region UseCosmosDb(IConfiguration) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConfigurationOverload()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).UseCosmosDb(config));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCosmosDb((IConfiguration)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_ConfigurationOverload()
	{
		// Arrange
		var builder = CreateBuilder();
		var config = new ConfigurationBuilder().Build();

		// Act
		var result = builder.UseCosmosDb(config);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterEventStore_WhenCalledWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);
		var config = new ConfigurationBuilder().Build();

		// Act
		builder.UseCosmosDb(config);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFluentChaining_WithOtherBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act -- verify chaining compiles and returns builder
		var result = builder
			.UseCosmosDb(opts => { opts.EventsContainerName = "events"; })
			.UseIntervalSnapshots(100);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
