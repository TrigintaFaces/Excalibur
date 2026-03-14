// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="EventSourcingBuilderSqlServerExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingBuilderSqlServerExtensionsShould
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Trusted_Connection=True;";

	private static IEventSourcingBuilder CreateBuilder(ServiceCollection? services = null)
	{
		var svc = services ?? new ServiceCollection();
		return new ExcaliburEventSourcingBuilder(svc);
	}

	#region UseSqlServer(connectionString) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConnectionStringOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).UseSqlServer(TestConnectionString));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConnectionStringIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_ConnectionStringOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseSqlServer(TestConnectionString);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSqlServerEventSourcingOptions_WhenCalledWithConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UseSqlServer(TestConnectionString);

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<SqlServerEventSourcingOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterEventStore_WhenCalledWithConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UseSqlServer(TestConnectionString);

		// Assert -- verify the underlying services were registered
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
	}

	#endregion

	#region UseSqlServer(Action<SqlServerEventSourcingOptions>) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConfigureOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).UseSqlServer((Action<SqlServerEventSourcingOptions>)(_ => { })));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer((Action<SqlServerEventSourcingOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_ConfigureOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseSqlServer(opts =>
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
		builder.UseSqlServer(opts =>
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
		builder.UseSqlServer(opts =>
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

		// Act -- verify chaining compiles and returns builder
		var result = builder
			.UseSqlServer(TestConnectionString)
			.UseIntervalSnapshots(100);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
