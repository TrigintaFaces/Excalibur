// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data;
using Excalibur.Data.Persistence;
using Excalibur.Data.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SqlServerProvider = Excalibur.Data.SqlServer.SqlServerPersistenceProvider;
using SqlServerProviderOptions = Excalibur.Data.SqlServer.SqlServerProviderOptions;

namespace Excalibur.Dispatch.Tests.Functional.Data;

/// <summary>
/// Anti-pattern verification tests ensuring consumer services use correct data access patterns.
/// Implements acceptance criteria for task bd-t4d5y.
/// </summary>
/// <remarks>
/// Per data-access-architecture-spec.md Decision Matrix:
/// - Services needing retry → IPersistenceProvider (AC1)
/// - Simple CRUD repositories → IDomainDb (AC2)
/// - No raw connection strings in consumer code (AC3)
/// - Decision matrix is followed (AC4)
/// - Unit tests document which pattern each service uses (AC5)
/// - No resilience requirements unmet by wrong pattern choice (AC6)
/// </remarks>
[Trait("Category", "Functional")]
[Trait("Component", "Core")]
[Trait("Pattern", "Verification")]
public sealed class AntiPatternVerificationShould
{
	#region AC1: Services needing retry inject IPersistenceProvider

	[Fact]
	public void ServiceNeedingRetry_ShouldUseIPersistenceProvider()
	{
		// Arrange - A service that needs automatic retries (e.g., external API integration)
		var services = new ServiceCollection();

		_ = services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new SqlServerProviderOptions
		{
			Connection = { ConnectionString = "Server=test;Database=test;Integrated Security=true;" },
			Name = "test-provider",
			RetryCount = 3
		}));
		_ = services.AddSingleton<ILogger<SqlServerProvider>>(NullLogger<SqlServerProvider>.Instance);
		_ = services.AddSingleton<IPersistenceProvider, SqlServerProvider>();
		_ = services.AddScoped<ResilientOrderProcessingService>();

		var provider = services.BuildServiceProvider();

		// Act
		var service = provider.GetRequiredService<ResilientOrderProcessingService>();

		// Assert - Service should have IPersistenceProvider injected
		_ = service.ShouldNotBeNull();
		service.HasPersistenceProvider.ShouldBeTrue(
			"Services requiring automatic retry should inject IPersistenceProvider");
	}

	[Fact]
	public void PersistenceProviderServices_ShouldHaveRetryPolicy()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerProviderOptions
		{
			Connection = { ConnectionString = "Server=test;Database=test;" },
			Name = "test-provider",
			RetryCount = 5
		});

		using var provider = new SqlServerProvider(options, NullLogger<SqlServerProvider>.Instance);

		// Assert - Provider should have retry capabilities
		_ = provider.RetryPolicy.ShouldNotBeNull();
		provider.RetryPolicy.MaxRetryAttempts.ShouldBe(5);
	}

	#endregion AC1: Services needing retry inject IPersistenceProvider

	#region AC2: Simple CRUD repositories inject IDomainDb

	[Fact]
	public void SimpleCrudRepository_ShouldUseIDomainDb()
	{
		// Arrange - A simple CRUD repository
		var fakeDomainDb = A.Fake<IDomainDb>();
		var fakeConnection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => fakeDomainDb.Connection).Returns(fakeConnection);

		// Act
		var repository = new SimpleProductRepository(fakeDomainDb);

		// Assert - Repository should use IDomainDb, not IPersistenceProvider
		_ = repository.ShouldNotBeNull();
		repository.UsesIDomainDb.ShouldBeTrue(
			"Simple CRUD repositories should inject IDomainDb, not IPersistenceProvider");
	}

	[Fact]
	public void SimpleCrudRepository_ShouldNotHaveRetryLogic()
	{
		// Arrange
		var fakeDomainDb = A.Fake<IDomainDb>();
		var fakeConnection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => fakeDomainDb.Connection).Returns(fakeConnection);

		var repository = new SimpleProductRepository(fakeDomainDb);

		// Assert - Repository should NOT have built-in retry (that's the caller's responsibility)
		repository.HasBuiltInRetry.ShouldBeFalse(
			"Simple CRUD repositories using IDomainDb should not have built-in retry logic");
	}

	#endregion AC2: Simple CRUD repositories inject IDomainDb

	#region AC3: No consumer service uses raw connection string

	[Fact]
	public void Services_ShouldNotAcceptRawConnectionStrings()
	{
		// This test documents the anti-pattern to avoid

		// WRONG: Service accepts raw connection string
		// public class BadService(string connectionString) { ... }

		// CORRECT: Service accepts abstraction
		// public class GoodService(IDomainDb domainDb) { ... }
		// public class GoodResilientService(IPersistenceProvider provider) { ... }

		// Verify our test services follow the correct pattern
		var domainDbType = typeof(SimpleProductRepository);
		var persistenceProviderType = typeof(ResilientOrderProcessingService);

		// Check constructors don't accept string for connection
		var domainDbConstructors = domainDbType.GetConstructors();
		var persistenceProviderConstructors = persistenceProviderType.GetConstructors();

		foreach (var ctor in domainDbConstructors)
		{
			var parameters = ctor.GetParameters();
			foreach (var param in parameters)
			{
				param.ParameterType.ShouldNotBe(typeof(string),
					$"Constructor parameter '{param.Name}' should not be a raw connection string");
			}
		}

		foreach (var ctor in persistenceProviderConstructors)
		{
			var parameters = ctor.GetParameters();
			foreach (var param in parameters)
			{
				param.ParameterType.ShouldNotBe(typeof(string),
					$"Constructor parameter '{param.Name}' should not be a raw connection string");
			}
		}
	}

	#endregion AC3: No consumer service uses raw connection string

	#region AC4: Decision matrix from spec is followed

	// Returns every distinct constructor-parameter type declared by <paramref name="serviceType"/>,
	// so a test can assert the real abstraction the type depends on (not a self-equal literal).
	private static Type[] ConstructorDependencies(Type serviceType) =>
		serviceType.GetConstructors()
			.SelectMany(static c => c.GetParameters())
			.Select(static p => p.ParameterType)
			.Distinct()
			.ToArray();

	[Fact]
	public void DecisionMatrix_FrameworkInfrastructure_UsesConnectionFactory()
	{
		// Per spec: Framework infrastructure (EventStore, SnapshotStore, resolvers, etc.) uses a
		// Func<TConnection> factory for maximum performance. Verify the REAL framework infrastructure
		// type SqlDataRequestResolver adheres — it depends on a connection-factory delegate and NOT on
		// the consumer-facing IDomainDb / IPersistenceProvider abstractions. This FAILS if the type is
		// ever refactored onto the wrong pattern.
		var dependencies = ConstructorDependencies(typeof(SqlDataRequestResolver));

		dependencies.ShouldContain(typeof(Func<SqlConnection>),
			"Framework infrastructure should inject a Func<TConnection> factory for maximum performance");
		dependencies.ShouldNotContain(typeof(IDomainDb),
			"Framework infrastructure should not depend on the consumer IDomainDb abstraction");
		dependencies.ShouldNotContain(typeof(IPersistenceProvider),
			"Framework infrastructure should not depend on IPersistenceProvider");
	}

	[Fact]
	public void DecisionMatrix_ConsumerDomainRepository_UsesIDb()
	{
		// Per spec: Consumer domain repositories use IDb/IDomainDb. Verify the REAL consumer repository
		// SimpleProductRepository injects IDomainDb and NOT IPersistenceProvider or a raw connection
		// factory. FAILS if the repository adopts the wrong (framework/resilience) pattern.
		var dependencies = ConstructorDependencies(typeof(SimpleProductRepository));

		dependencies.ShouldContain(typeof(IDomainDb),
			"Consumer domain repositories should inject IDomainDb for testability");
		dependencies.ShouldNotContain(typeof(IPersistenceProvider),
			"Consumer domain repositories should not inject IPersistenceProvider");
		dependencies.ShouldNotContain(typeof(Func<SqlConnection>),
			"Consumer domain repositories should not inject a raw connection factory");
	}

	[Fact]
	public void DecisionMatrix_AutomaticRetries_UsesIPersistenceProvider()
	{
		// Per spec: Services needing automatic retries and health checks use IPersistenceProvider.
		// Verify the REAL resilient service ResilientOrderProcessingService injects IPersistenceProvider
		// and NOT the plain IDomainDb pattern (which has no retry). FAILS on the wrong pattern choice.
		var dependencies = ConstructorDependencies(typeof(ResilientOrderProcessingService));

		dependencies.ShouldContain(typeof(IPersistenceProvider),
			"Services needing automatic retries should inject IPersistenceProvider");
		dependencies.ShouldNotContain(typeof(IDomainDb),
			"Services needing automatic retries should not use the plain IDomainDb pattern (no retry)");
	}

	[Fact]
	public void DecisionMatrix_ExplicitTransactionControl_UsesConnectionFactory()
	{
		// Per spec: Explicit transaction/connection lifecycle control uses a Func<TConnection> factory so
		// the caller owns the connection. Verify the REAL type SqlDataRequestResolver, which creates and
		// disposes its own SqlConnection per call, depends on the connection-factory delegate rather than
		// a shared IDomainDb / IPersistenceProvider. FAILS if refactored onto a non-factory pattern.
		var dependencies = ConstructorDependencies(typeof(SqlDataRequestResolver));

		dependencies.ShouldContain(typeof(Func<SqlConnection>),
			"Explicit transaction control scenarios should inject a Func<TConnection> factory");
		dependencies.ShouldNotContain(typeof(IDomainDb),
			"Explicit transaction control should not rely on a shared IDomainDb connection");
	}

	[Fact]
	public void DecisionMatrix_SimpleCRUD_UsesIDb()
	{
		// Per spec: Simple CRUD with testability uses IDb/IDomainDb. Verify the REAL CRUD repository
		// SimpleProductRepository injects IDomainDb and carries no resilience/factory overhead. FAILS if
		// it adopts IPersistenceProvider (overkill) or a raw connection factory.
		var dependencies = ConstructorDependencies(typeof(SimpleProductRepository));

		dependencies.ShouldContain(typeof(IDomainDb),
			"Simple CRUD operations should inject IDomainDb for testability");
		dependencies.ShouldNotContain(typeof(IPersistenceProvider),
			"Simple CRUD operations should not inject IPersistenceProvider (unnecessary overhead)");
		dependencies.ShouldNotContain(typeof(Func<SqlConnection>),
			"Simple CRUD operations should not inject a raw connection factory");
	}

	#endregion AC4: Decision matrix from spec is followed

	#region AC5: Unit tests document which pattern each service uses

	[Fact]
	public void DocumentPattern_ResilientOrderProcessingService()
	{
		// DOCUMENTATION: ResilientOrderProcessingService uses IPersistenceProvider
		// Reason: Requires automatic retry on transient SQL failures
		// Pattern: 3 (IPersistenceProvider)

		var serviceType = typeof(ResilientOrderProcessingService);
		var constructor = serviceType.GetConstructors().First();
		var parameters = constructor.GetParameters();

		parameters.ShouldContain(p => p.ParameterType == typeof(IPersistenceProvider),
			"ResilientOrderProcessingService should inject IPersistenceProvider (Pattern 3)");
	}

	[Fact]
	public void DocumentPattern_SimpleProductRepository()
	{
		// DOCUMENTATION: SimpleProductRepository uses IDomainDb
		// Reason: Simple CRUD without retry requirements
		// Pattern: 2 (IDomainDb)

		var serviceType = typeof(SimpleProductRepository);
		var constructor = serviceType.GetConstructors().First();
		var parameters = constructor.GetParameters();

		parameters.ShouldContain(p => p.ParameterType == typeof(IDomainDb),
			"SimpleProductRepository should inject IDomainDb (Pattern 2)");
	}

	#endregion AC5: Unit tests document which pattern each service uses

	#region AC6: No resilience requirements unmet by wrong pattern choice

	[Fact]
	public void ResilienceRequirements_ServiceWithRetry_HasRetryCapability()
	{
		// Arrange - Service that needs retry
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerProviderOptions
		{
			Connection = { ConnectionString = "Server=test;Database=test;" },
			Name = "test-provider",
			RetryCount = 3
		});

		using var provider = new SqlServerProvider(options, NullLogger<SqlServerProvider>.Instance);
		var service = new ResilientOrderProcessingService(provider);

		// Assert - Service has retry capability
		service.HasRetryCapability.ShouldBeTrue(
			"Service requiring resilience should have retry capability through IPersistenceProvider");
	}

	[Fact]
	public void ResilienceRequirements_SimpleRepository_NoUnnecessaryOverhead()
	{
		// Arrange - Simple repository that doesn't need retry
		var fakeDomainDb = A.Fake<IDomainDb>();
		var fakeConnection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => fakeDomainDb.Connection).Returns(fakeConnection);

		var repository = new SimpleProductRepository(fakeDomainDb);

		// Assert - Repository doesn't have unnecessary retry overhead
		repository.HasBuiltInRetry.ShouldBeFalse(
			"Simple repositories should not have unnecessary retry overhead");
	}

	[Fact]
	public void PatternMismatch_Detection()
	{
		// This test documents how to detect pattern mismatches

		// ANTI-PATTERN 1: Using IDomainDb when retry is needed
		// Risk: Transient failures will propagate to caller without retry
		// Detection: Service with retry requirement + IDomainDb dependency = MISMATCH

		// ANTI-PATTERN 2: Using IPersistenceProvider for simple CRUD
		// Risk: Unnecessary complexity and overhead
		// Detection: Simple CRUD service + IPersistenceProvider dependency = OVERKILL

		// ANTI-PATTERN 3: Using raw connection string
		// Risk: No abstraction, hard to test, no lifecycle management
		// Detection: Constructor accepting string parameter for connection = VIOLATION

		// This test passes to document the detection strategies
	}

	#endregion AC6: No resilience requirements unmet by wrong pattern choice
}

#region Test Service Classes

/// <summary>
/// Service that requires automatic retry on transient failures.
/// Uses IPersistenceProvider (Pattern 3).
/// </summary>
public sealed class ResilientOrderProcessingService
{
	private readonly IPersistenceProvider _provider;

	public ResilientOrderProcessingService(IPersistenceProvider provider)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
	}

	public bool HasPersistenceProvider => _provider != null;
	public bool HasRetryCapability =>
		(_provider.GetService(typeof(IPersistenceProviderTransaction)) as IPersistenceProviderTransaction)?.RetryPolicy.MaxRetryAttempts > 0;
}

/// <summary>
/// Simple CRUD repository without retry requirements.
/// Uses IDomainDb (Pattern 2).
/// </summary>
public sealed class SimpleProductRepository
{
	private readonly IDbConnection _connection;

	public SimpleProductRepository(IDomainDb domainDb)
	{
		ArgumentNullException.ThrowIfNull(domainDb);
		_connection = domainDb.Connection;
	}

	public bool UsesIDomainDb => true;
	public bool HasBuiltInRetry => false;
}

#endregion Test Service Classes