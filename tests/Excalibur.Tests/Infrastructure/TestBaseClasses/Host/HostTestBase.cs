using System.Data;

using Alba;

using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Logging;
using Excalibur.Tests.Shared;

using FluentMigrator.Runner;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Xunit.Abstractions;

namespace Excalibur.Tests.Infrastructure.TestBaseClasses.Host;

public abstract class HostTestBase<TFixture> : IClassFixture<TFixture>, IAsyncLifetime, IDisposable
	where TFixture : class, IDatabaseContainerFixture
{
	private readonly TestOutputSink _sink;
	private IServiceScope _scope;
	private bool _disposedValue;

	protected HostTestBase(TFixture fixture, ITestOutputHelper output)
	{
		ArgumentNullException.ThrowIfNull(fixture);
		ArgumentNullException.ThrowIfNull(output);

		_sink = new TestOutputSink(output);

		var builder = Startup
			.CreateHostBuilder(_sink)
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
	///     Gets the test output helper.
	/// </summary>
	protected ITestOutputHelper Output { get; set; }

	/// <summary>
	///     Gets the tenant identifier for tests.
	/// </summary>
	protected string TenantId { get; set; } = WellKnownId.TestTenant;

	public Task InitializeAsync()
	{
		_scope = TestHost!.Services.CreateScope();
		return InitializeDatabaseAsync();
	}

	public async Task DisposeAsync()
	{
		await _sink.DisposeAsync().ConfigureAwait(false);
		_scope?.Dispose();
		TestHost?.Dispose();
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
	protected TService GetService<TService>() => _scope.ServiceProvider.GetService<TService>();

	/// <summary>
	///     Retrieves a service from the container.
	/// </summary>
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
			TestHost?.Dispose();
			_scope.Dispose();
			_sink.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		_disposedValue = true;
	}
}
