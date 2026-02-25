// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using FluentMigrator.Runner;

using Microsoft.AspNetCore.Builder;

using Tests.Shared.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Dispatch.Tests.Functional.Infrastructure.TestBaseClasses.Host;

public abstract class HostTestBase<TFixture> : IClassFixture<TFixture>, IAsyncLifetime, IDisposable
	where TFixture : class, IDatabaseContainerFixture
{
	private readonly TestOutputSink _sink;
	private IServiceScope _scope = null!; // Initialized in InitializeAsync
	private bool _disposedValue;

	protected HostTestBase(TFixture fixture, ITestOutputHelper output)
	{
		ArgumentNullException.ThrowIfNull(fixture);
		ArgumentNullException.ThrowIfNull(output);

		_sink = new TestOutputSink(output);

		var builder = Startup
			.CreateHostBuilder()
			.BaseConfigureHostServices(fixture, ConfigureHostServices);

		TestHost = builder.StartAlbaAsync(ConfigureHostApplication).GetAwaiter().GetResult();
		Fixture = fixture;
		Output = output;
	}

	public IAlbaHost? TestHost { get; }

	/// <summary>
	///     Gets the test fixture.
	/// </summary>
	protected TFixture Fixture { get; }

	/// <summary>
	///     Gets or sets the test output helper.
	/// </summary>
	protected ITestOutputHelper Output { get; set; }

	/// <summary>
	///     Gets or sets the tenant identifier for tests.
	/// </summary>
	protected string TenantId { get; set; } = WellKnownId.TestTenant;

	/// <inheritdoc/>
	public Task InitializeAsync()
	{
		_scope = TestHost.Services.CreateScope();
		return InitializeDatabaseAsync();
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		_sink.Dispose(); // TestOutputSink only implements IDisposable, not IAsyncDisposable
		_scope?.Dispose();
		TestHost?.Dispose();
		await Task.CompletedTask.ConfigureAwait(true);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Allows derived tests to run additional database setup.
	/// </summary>
	protected virtual Task OnDatabaseInitialized(IDbConnection connection) => Task.CompletedTask;

	/// <summary>
	///     Allows derived classes to add additional services.
	/// </summary>
	protected virtual void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		// Default: No additional services
	}

	/// <summary>
	///     Allows derived classes to configure the application.
	/// </summary>
	protected virtual void ConfigureHostApplication(WebApplication app)
	{
		// Default: No additional configuration
	}

	/// <summary>
	///     Retrieves a service from the container.
	/// </summary>
	protected TService? GetService<TService>() => _scope.ServiceProvider.GetService<TService>();

	/// <summary>
	///     Retrieves a service from the container.
	/// </summary>
	protected TService GetRequiredService<TService>() where TService : notnull => _scope.ServiceProvider.GetRequiredService<TService>();

	/// <summary>
	///     Initializes the database before tests run. Applies FluentMigrator migrations and calls test-specific initialization.
	/// </summary>
	protected Task InitializeDatabaseAsync() => InitializePersistenceAsync();

	protected virtual Task InitializePersistenceAsync() => InitializeRelationalDatabaseAsync();

	protected virtual async Task InitializeRelationalDatabaseAsync()
	{
		using var connection = Fixture.CreateDbConnection();
		connection.Open();

		var runner = GetService<IMigrationRunner>();

		runner?.MigrateUp();

		await OnDatabaseInitialized(connection).ConfigureAwait(true);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposedValue)
		{
			return;
		}

		if (disposing)
		{
			TestHost?.Dispose();
			_scope.Dispose();
			_sink.Dispose(); // TestOutputSink only implements IDisposable, not IAsyncDisposable
		}

		_disposedValue = true;
	}
}
