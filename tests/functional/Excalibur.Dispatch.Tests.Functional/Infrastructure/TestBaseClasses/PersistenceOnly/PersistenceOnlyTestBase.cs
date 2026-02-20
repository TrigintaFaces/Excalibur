// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using FluentMigrator.Runner;

using Tests.Shared.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Dispatch.Tests.Functional.Infrastructure.TestBaseClasses.PersistenceOnly;

public abstract class PersistenceOnlyTestBase<TFixture> : IClassFixture<TFixture>, IAsyncLifetime, IDisposable
	where TFixture : class, IDatabaseContainerFixture
{
	private IServiceScope _scope = null!; // Initialized in InitializeAsync
	private bool _disposedValue;

	protected PersistenceOnlyTestBase(TFixture fixture, ITestOutputHelper output)
	{
		ArgumentNullException.ThrowIfNull(fixture);
		ArgumentNullException.ThrowIfNull(output);

		Fixture = fixture;
		Output = output;
		ServiceProvider = Startup.ConfigurePersistenceOnlyServices();
	}

	protected IServiceProvider ServiceProvider { get; }

	/// <summary>
	///     Gets the test fixture.
	/// </summary>
	protected TFixture Fixture { get; }

	/// <summary>
	///     Gets or sets the <see cref="ITestOutputHelper" /> used to output debugging information in tests.
	/// </summary>
	protected ITestOutputHelper Output { get; set; }

	/// <summary>
	///     Gets or sets the tenant identifier used for the tests.
	/// </summary>
	protected string TenantId { get; set; } = "TestTenant";

	/// <inheritdoc/>
	public Task InitializeAsync()
	{
		_scope = ServiceProvider.CreateScope();
		return InitializeDatabaseAsync();
	}

	/// <inheritdoc/>
	public Task DisposeAsync()
	{
		_scope?.Dispose();
		return Task.CompletedTask;
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

	protected virtual void AddServices(IServiceCollection services, IConfiguration configuration)
	{
	}

	/// <summary>
	///     A method used to retrieve a service from the container.
	/// </summary>
	/// <typeparam name="TService"> The type of service to retrieve. </typeparam>
	/// <returns> An instance of <typeparamref name="TService" />. </returns>
	protected TService? GetService<TService>() => _scope.ServiceProvider.GetService<TService>();

	/// <summary>
	///     A method used to retrieve a service from the container.
	/// </summary>
	/// <typeparam name="TService"> The type of service to retrieve. </typeparam>
	/// <returns> An instance of <typeparamref name="TService" />. </returns>
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
			_scope.Dispose();
			(ServiceProvider as IDisposable)?.Dispose();
		}

		_disposedValue = true;
	}
}
