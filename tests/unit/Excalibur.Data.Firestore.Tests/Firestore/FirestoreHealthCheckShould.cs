// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreHealthCheck"/> constructor validation
/// and healthy/unhealthy scenarios.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirestoreHealthCheckShould : UnitTestBase
{
	private readonly ILogger<FirestoreHealthCheck> _healthCheckLogger;

	public FirestoreHealthCheckShould()
	{
		_healthCheckLogger = A.Fake<ILogger<FirestoreHealthCheck>>();
	}

	#region Constructor Validation

	[Fact]
	public void Constructor_WithNullProvider_ThrowsArgumentNullException()
	{
		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreHealthCheck(provider: null!, _healthCheckLogger));
		exception.ParamName.ShouldBe("provider");
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		var options = Options.Create(new FirestoreOptions { ProjectId = "test" });
		var providerLogger = A.Fake<ILogger<FirestorePersistenceProvider>>();
		var provider = new FirestorePersistenceProvider(options, providerLogger);

		var exception = Should.Throw<ArgumentNullException>(() =>
			new FirestoreHealthCheck(provider, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void Constructor_WithValidParams_CreatesInstance()
	{
		var options = Options.Create(new FirestoreOptions { ProjectId = "test" });
		var providerLogger = A.Fake<ILogger<FirestorePersistenceProvider>>();
		var provider = new FirestorePersistenceProvider(options, providerLogger);

		var healthCheck = new FirestoreHealthCheck(provider, _healthCheckLogger);

		_ = healthCheck.ShouldNotBeNull();
	}

	#endregion Constructor Validation

	#region Health Check Behavior

	[Fact]
	public async Task CheckHealthAsync_WhenProviderNotInitialized_ReturnsUnhealthy()
	{
		// Arrange — provider is not initialized (no FirestoreDb connected)
		var options = Options.Create(new FirestoreOptions { ProjectId = "test" });
		var providerLogger = A.Fake<ILogger<FirestorePersistenceProvider>>();
		var provider = new FirestorePersistenceProvider(options, providerLogger);
		var healthCheck = new FirestoreHealthCheck(provider, _healthCheckLogger);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("Firestore", healthCheck, null, null),
		};

		// Act — TestConnectionAsync will fail because provider is not initialized
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy);
	}

	#endregion Health Check Behavior
}
