using System.Data;

using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Shared;

using FluentMigrator.Runner;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit.Abstractions;

namespace Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;

public abstract class PersistenceOnlyTestBase<TFixture> : IClassFixture<TFixture>, IAsyncLifetime, IDisposable
	where TFixture : class, IDatabaseContainerFixture
{
	private IServiceScope _scope;
	private bool _disposedValue;

	protected PersistenceOnlyTestBase(TFixture fixture, ITestOutputHelper output)
	{
		ArgumentNullException.ThrowIfNull(fixture);
		ArgumentNullException.ThrowIfNull(output);

		Fixture = fixture;
		Output = output;
		ServiceProvider = Startup.ConfigurePersistenceOnlyServices(fixture, AddServices);
	}

	protected IServiceProvider ServiceProvider { get; }

	/// <summary>
	///     Gets the test fixture.
	/// </summary>
	protected TFixture Fixture { get; }

	/// <summary>
	///     Gets the <see cref="ITestOutputHelper" /> used to output debugging information in tests.
	/// </summary>
	protected ITestOutputHelper Output { get; set; }

	/// <summary>
	///     Gets the tenant identifier used for the tests.
	/// </summary>
	protected string TenantId { get; set; } = WellKnownId.TestTenant;

	public Task InitializeAsync()
	{
		_scope = ServiceProvider.CreateScope();
		return InitializeDatabaseAsync();
	}

	public async Task DisposeAsync()
	{
		_scope?.Dispose();
	}

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
	protected TService GetService<TService>() => _scope.ServiceProvider.GetService<TService>();

	/// <summary>
	///     A method used to retrieve a service from the container.
	/// </summary>
	/// <typeparam name="TService"> The type of service to retrieve. </typeparam>
	/// <returns> An instance of <typeparamref name="TService" />. </returns>
	protected TService GetRequiredService<TService>() => _scope.ServiceProvider.GetRequiredService<TService>();

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

		await OnDatabaseInitialized(connection).ConfigureAwait(false);
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
