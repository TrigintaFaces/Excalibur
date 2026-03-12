// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="EventSourcingBuilderPostgresExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingBuilderPostgresExtensionsShould
{
	private const string TestConnectionString = "Host=localhost;Database=TestDb;Username=test;Password=test;";

	private static IEventSourcingBuilder CreateBuilder(ServiceCollection? services = null)
	{
		var svc = services ?? new ServiceCollection();
		return new ExcaliburEventSourcingBuilder(svc);
	}

	#region UsePostgres(connectionString) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConnectionStringOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).UsePostgres(TestConnectionString));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConnectionStringIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UsePostgres(null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_ConnectionStringOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UsePostgres(TestConnectionString);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterPostgresEventSourcingOptions_WhenCalledWithConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UsePostgres(TestConnectionString);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<PostgresEventSourcingOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterEventStore_WhenCalledWithConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UsePostgres(TestConnectionString);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
	}

	#endregion

	#region UsePostgres(Action<PostgresEventSourcingOptions>) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConfigureOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).UsePostgres((Action<PostgresEventSourcingOptions>)(_ => { })));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UsePostgres((Action<PostgresEventSourcingOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_ConfigureOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UsePostgres(opts =>
		{
			opts.ConnectionString = TestConnectionString;
		});

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void InvokeConfigureAction_WhenCalledWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);
		var configureInvoked = false;

		// Act
		builder.UsePostgres(opts =>
		{
			opts.ConnectionString = TestConnectionString;
			configureInvoked = true;
		});

		// Assert
		configureInvoked.ShouldBeTrue();
	}

	[Fact]
	public void RegisterEventStore_WhenCalledWithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UsePostgres(opts =>
		{
			opts.ConnectionString = TestConnectionString;
		});

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

		// Act
		var result = builder
			.UsePostgres(TestConnectionString)
			.UseIntervalSnapshots(100);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
