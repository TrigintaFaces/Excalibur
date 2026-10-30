// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerProjectionStoreExtensions"/>.
/// Validates DI registration via all 3 overloads: Action, connection string, and connection factory.
/// </summary>
/// <remarks>
/// Sprint 535 (S535.7): Tests for S535.5 — AddSqlServerProjectionStore DI extension.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerProjectionStoreExtensionsShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Integrated Security=true;";

    [Fact]
    public void IsolateDestinationsAndValidateOnlyTheRegistrationOwningTheConnectionString()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSqlServerProjectionStore<TestProjection>(o =>
        {
            o.ConnectionString = TestConnectionString;
            o.TableName = "FirstModel";
        });
        services.AddSqlServerProjectionStore<ProjectionIDbTestRecord>(() => new SqlConnection(TestConnectionString),
            o => o.TableName = "SecondModel");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IProjectionStore<TestProjection>>();
        var second = scope.ServiceProvider.GetRequiredService<IProjectionStore<ProjectionIDbTestRecord>>();
        var options = provider.GetRequiredService<IOptionsMonitor<SqlServerProjectionStoreOptions>>();
        options.Get(typeof(TestProjection).AssemblyQualifiedName!).TableName.ShouldBe("FirstModel");
        options.Get(typeof(ProjectionIDbTestRecord).AssemblyQualifiedName!).TableName.ShouldBe("SecondModel");
        ((IProjectionDestinationIdentity)first.GetService(typeof(IProjectionDestinationIdentity))!).Identity.ShouldContain("FirstModel");
        ((IProjectionDestinationIdentity)second.GetService(typeof(IProjectionDestinationIdentity))!).Identity.ShouldContain("SecondModel");
    }

    [Fact]
    public void KeepTwoOptionsBasedProjectionTablesSeparate()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSqlServerProjections(TestConnectionString, p => p
            .Add<TestProjection>(o => o.TableName = "Customers")
            .Add<ProjectionIDbTestRecord>(o => o.TableName = "Guard"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var customer = scope.ServiceProvider.GetRequiredService<IProjectionStore<TestProjection>>();
        var guard = scope.ServiceProvider.GetRequiredService<IProjectionStore<ProjectionIDbTestRecord>>();
        ((IProjectionDestinationIdentity)customer.GetService(typeof(IProjectionDestinationIdentity))!).Identity.ShouldContain("Customers");
        ((IProjectionDestinationIdentity)guard.GetService(typeof(IProjectionDestinationIdentity))!).Identity.ShouldContain("Guard");
    }

    [Fact]
    public void PreserveUnnamedFactoryDefaultsAndIsolateTypedDatabaseOverrides()
    {
        var services = new ServiceCollection().AddLogging();
        services.Configure<SqlServerProjectionStoreOptions>(o => o.TableName = "SharedDefault");
        services.AddSqlServerProjectionStore<TestProjection>(() => new SqlConnection(TestConnectionString));
        services.AddSqlServerProjectionStore<ProjectionIDbTestRecord, IProjectionTestDb>(o => o.TableName = "TypedOverride");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IProjectionStore<TestProjection>>();
        var typed = scope.ServiceProvider.GetRequiredService<IProjectionStore<ProjectionIDbTestRecord>>();
        ((IProjectionDestinationIdentity)factory.GetService(typeof(IProjectionDestinationIdentity))!).Identity.ShouldContain("SharedDefault");
        ((IProjectionDestinationIdentity)typed.GetService(typeof(IProjectionDestinationIdentity))!).Identity.ShouldContain("TypedOverride");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveExplicitOptionsInstancesForUnconfiguredFactories(bool typedDatabase)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IOptions<SqlServerProjectionStoreOptions>>(Options.Create(new SqlServerProjectionStoreOptions
        {
            TableName = "ExplicitOptions", SchemaName = "custom"
        }));
        if (typedDatabase) services.AddSqlServerProjectionStore<ProjectionIDbTestRecord, IProjectionTestDb>();
        else services.AddSqlServerProjectionStore<ProjectionIDbTestRecord>(() => new SqlConnection(TestConnectionString));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<ProjectionIDbTestRecord>>();
        var identity = ((IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!).Identity;
        identity.ShouldContain("ExplicitOptions");
        identity.ShouldContain("custom");
    }

    [Fact]
    public void ApplySharedDefaultsBeforeIsolatedSerializerAndTableOverrides()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSqlServerProjections(shared =>
        {
            shared.ConnectionString = TestConnectionString;
            shared.TableName = "Shared";
            shared.JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        }, p => p.Add<TestProjection>(o =>
        {
            o.TableName = "Override";
            o.JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseUpper };
        }).Add<ProjectionIDbTestRecord>());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IProjectionStore<TestProjection>>();
        var second = scope.ServiceProvider.GetRequiredService<IProjectionStore<ProjectionIDbTestRecord>>();
        var firstJson = ((ITransactionalProjectionStore<TestProjection>)first.GetService(typeof(ITransactionalProjectionStore<TestProjection>))!).Serialize(new TestProjection { Name = "first" });
        var secondJson = ((ITransactionalProjectionStore<ProjectionIDbTestRecord>)second.GetService(typeof(ITransactionalProjectionStore<ProjectionIDbTestRecord>))!).Serialize(new ProjectionIDbTestRecord { Name = "second" });
        firstJson.ShouldContain("\"NAME\"");
        secondJson.ShouldContain("\"name\"");
        ((IProjectionDestinationIdentity)first.GetService(typeof(IProjectionDestinationIdentity))!).Identity.ShouldContain("Override");
        ((IProjectionDestinationIdentity)second.GetService(typeof(IProjectionDestinationIdentity))!).Identity.ShouldContain("Shared");
    }

	// --- Overload 1: Action<Options> ---

	[Fact]
	public void Register_ProjectionStore_With_Action_Overload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerProjectionStore<TestProjection>(options =>
		{
			options.ConnectionString = TestConnectionString;
		});

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<TestProjection>) &&
			sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void Throw_When_Services_Null_On_Action_Overload()
	{
		IServiceCollection services = null!;

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjectionStore<TestProjection>(options =>
			{
				options.ConnectionString = TestConnectionString;
			}));
	}

	[Fact]
	public void Throw_When_ConfigureOptions_Null_On_Action_Overload()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjectionStore<TestProjection>((Action<SqlServerProjectionStoreOptions>)null!));
	}

	[Fact]
	public void Return_Services_For_Chaining_On_Action_Overload()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerProjectionStore<TestProjection>(options =>
		{
			options.ConnectionString = TestConnectionString;
		});

		result.ShouldBeSameAs(services);
	}

	// --- Overload 2: Connection factory ---

	[Fact]
	public void Register_ProjectionStore_With_ConnectionFactory_Overload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerProjectionStore<TestProjection>(
			() => new SqlConnection(TestConnectionString));

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<TestProjection>) &&
			sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void Throw_When_ConnectionFactory_Null()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjectionStore<TestProjection>((Func<SqlConnection>)null!));
	}

	[Fact]
	public void Return_Services_For_Chaining_On_ConnectionFactory_Overload()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerProjectionStore<TestProjection>(
			() => new SqlConnection(TestConnectionString));

		result.ShouldBeSameAs(services);
	}

	// --- TryAdd semantics ---

	[Fact]
	public void Not_Override_Existing_Registration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSqlServerProjectionStore<TestProjection>(opts => opts.ConnectionString = TestConnectionString);

		// Act — register again with different connection string
		services.AddSqlServerProjectionStore<TestProjection>(opts => opts.ConnectionString = "Server=other;Database=OtherDb;Integrated Security=true;");

		// Assert — should still have exactly one registration (TryAddScoped)
		services.Count(sd => sd.ServiceType == typeof(IProjectionStore<TestProjection>)).ShouldBe(1);
	}

	// --- Options validation ---

	[Fact]
	public void Register_Options_Configuration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerProjectionStore<TestProjection>(options =>
		{
			options.ConnectionString = TestConnectionString;
			options.TableName = "CustomProjections";
		});

		// Assert — Options configuration should be registered
		services.Any(sd =>
			sd.ServiceType == typeof(IConfigureOptions<SqlServerProjectionStoreOptions>)).ShouldBeTrue();
	}

	// --- Projection types ---

	private sealed class TestProjection
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
	}
}
