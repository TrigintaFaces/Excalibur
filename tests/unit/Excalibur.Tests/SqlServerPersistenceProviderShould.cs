// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;
using Excalibur.Data.SqlServer.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

using IPersistenceProvider = Excalibur.Data.Persistence.IPersistenceProvider;

namespace Excalibur.Tests;

/// <summary>
/// Unit tests for the SQL Server persistence provider, resolved through the production
/// <c>AddSqlServerPersistence</c> registration so that the contract is asserted on the instance a
/// consumer actually gets rather than on a hand-assembled one.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerPersistenceProviderShould : IDisposable
{
	private const string TestConnectionString =
		"Server=localhost;Database=test;User Id=sa;Password=Test123!;TrustServerCertificate=true"; // pragma: allowlist secret

	private readonly ServiceProvider _services;
	private readonly SqlServerPersistenceProvider _provider;

	public SqlServerPersistenceProviderShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerPersistence(options =>
		{
			options.ConnectionString = TestConnectionString;
			options.CommandTimeout = 30;
			options.Connection.ApplicationName = "TestApp";
			options.Connection.EnableMars = true;
			options.Pooling.EnableConnectionPooling = true;
			options.Pooling.MinPoolSize = 2;
			options.Pooling.MaxPoolSize = 20;
		});

		_services = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = false,
			ValidateScopes = true,
		});

		_provider = (SqlServerPersistenceProvider)_services.GetRequiredService<ISqlPersistenceProvider>();
	}

	[Fact]
	public void HonourTheConfiguredInstanceNameInsteadOfAFixedLiteral()
	{
		// Liveness arm for the identity contract: the default-name assertion above is also satisfied by a
		// provider that ignores its options entirely, so this proves the configured value actually reaches
		// the reported Name. Together they pin both halves: unset falls back, set is honoured.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerPersistence(options =>
		{
			options.Name = "orders-primary";
			options.ConnectionString = TestConnectionString;
		});

		using var provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = false,
			ValidateScopes = true,
		});

		var sut = (SqlServerPersistenceProvider)provider.GetRequiredService<ISqlPersistenceProvider>();

		sut.Name.ShouldBe("orders-primary");
	}

	[Fact]
	public void ExposeTheExpectedIdentityAndCapabilityFlags()
	{
		// The registration above configures no name, so the provider falls back to the engine default.
		// "Name" is the configured instance name, not the class name it used to report.
		_provider.Name.ShouldBe("sqlserver");
		_provider.ProviderType.ShouldBe("SQL");
		_provider.SupportsBulkOperations.ShouldBeTrue();
		_provider.SupportsStoredProcedures.ShouldBeTrue();
		_provider.DatabaseType.ShouldBe("SqlServer");
	}

	[Fact]
	public void ImplementTheSqlAndBasePersistenceContracts()
	{
		_ = _provider.ShouldBeAssignableTo<ISqlPersistenceProvider>();
		_ = _provider.ShouldBeAssignableTo<IPersistenceProvider>();
	}

	[Fact]
	public void ImplementBothDisposalContracts()
	{
		_ = _provider.ShouldBeAssignableTo<IDisposable>();
		_ = _provider.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void AnswerBothOptionalCapabilityQueries()
	{
		_provider.GetService(typeof(IPersistenceProviderHealth)).ShouldNotBeNull();
		_provider.GetService(typeof(IPersistenceProviderTransaction)).ShouldNotBeNull();
	}

	[Fact]
	public void DeclineACapabilityItDoesNotOffer() => _provider.GetService(typeof(IServiceProvider)).ShouldBeNull();

	[Fact]
	public void ReportTheConfiguredConnectionStringVerbatim()
	{
		// The property is the configured value, not an enriched one; enrichment is applied per connection.
		var builder = new SqlConnectionStringBuilder(_provider.ConnectionString);

		builder.DataSource.ShouldBe("localhost");
		builder.InitialCatalog.ShouldBe("test");
		builder.UserID.ShouldBe("sa");
	}

	[Fact]
	public void ApplyTheConfiguredConnectionSettingsToTheConnectionsItCreates()
	{
		using var connection = _provider.CreateConnection();

		var builder = new SqlConnectionStringBuilder(connection.ConnectionString);

		builder.DataSource.ShouldBe("localhost");
		builder.InitialCatalog.ShouldBe("test");
		builder.ApplicationName.ShouldBe("TestApp");
		builder.MultipleActiveResultSets.ShouldBeTrue();
		builder.Pooling.ShouldBeTrue();
		builder.MinPoolSize.ShouldBe(2);
		builder.MaxPoolSize.ShouldBe(20);
	}

	[Fact]
	public void ExposeTheRetryPolicySuppliedByTheRegistration() => _ = _provider.RetryPolicy.ShouldNotBeNull();

	[Fact]
	public void CreateATransactionScope() => _ = _provider.CreateTransactionScope().ShouldNotBeNull();

	[Fact]
	public void CreateATransactionScopeWithAnExplicitIsolationLevelAndTimeout() =>
		_ = _provider.CreateTransactionScope(System.Data.IsolationLevel.Snapshot, TimeSpan.FromMinutes(1))
			.ShouldNotBeNull();

	[Fact]
	public void NotThrowOnDispose() => Should.NotThrow(_provider.Dispose);

	[Fact]
	public void NotThrowWhenDisposedTwice() =>
		Should.NotThrow(() =>
		{
			_provider.Dispose();
			_provider.Dispose();
		});

	[Fact]
	public async Task NotThrowOnAsyncDispose() =>
		await Should.NotThrowAsync(() => _provider.DisposeAsync().AsTask()).ConfigureAwait(false);

	[Fact]
	public void ValidateOptionsAndRejectAnEmptyConnectionString()
	{
		var options = new SqlServerPersistenceOptions { ConnectionString = string.Empty };

		_ = Should.Throw<Exception>(options.Validate);
	}

	[Fact]
	public void ValidateOptionsAndRejectANegativeCommandTimeout()
	{
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = TestConnectionString,
			CommandTimeout = -1,
		};

		_ = Should.Throw<Exception>(options.Validate);
	}

	[Fact]
	public void ValidateOptionsAndAcceptAWellFormedConfiguration()
	{
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = TestConnectionString,
			CommandTimeout = 30,
		};

		Should.NotThrow(options.Validate);
	}

	/// <inheritdoc/>
	// The container owns the provider singleton and disposes it too; Dispose is idempotent, so
	// releasing it here as well is safe and keeps this class's own field ownership explicit.
	public void Dispose()
	{
		_provider.Dispose();
		_services.Dispose();
	}
}
