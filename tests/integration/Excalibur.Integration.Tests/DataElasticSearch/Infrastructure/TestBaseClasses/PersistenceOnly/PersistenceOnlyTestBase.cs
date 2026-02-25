// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor — field is set in InitializeAsync()

using Tests.Shared.Fixtures;
using Tests.Shared.Helpers;

using Xunit.Abstractions;

namespace Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses.PersistenceOnly;

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

		Configuration = new ConfigurationBuilder()
			.AddEnvironmentVariables()
			.Build();

		var services = new ServiceCollection();
		_ = services.AddSingleton(Configuration);
		_ = services.AddTestLogging(output);
		_ = services.AddSingleton<IDatabaseContainerFixture>(fixture);

#pragma warning disable CA2214
		AddServices(services, Configuration);
#pragma warning restore CA2214

		ServiceProvider = services.BuildServiceProvider();
	}

	protected IServiceProvider ServiceProvider { get; }

	protected IConfiguration Configuration { get; }

	/// <summary>
	///     Gets the test fixture.
	/// </summary>
	protected TFixture Fixture { get; }

	/// <summary>
	///     Gets or sets the test output helper.
	/// </summary>
	protected ITestOutputHelper Output { get; set; }

	/// <inheritdoc />
	public Task InitializeAsync()
	{
		_scope = ServiceProvider.CreateScope();
		return InitializePersistenceAsync();
	}

	/// <inheritdoc />
	public Task DisposeAsync()
	{
		_scope?.Dispose();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Allows derived classes to add additional services.
	/// </summary>
	protected virtual void AddServices(IServiceCollection services, IConfiguration configuration)
	{
		// Default: no additional services.
	}

	/// <summary>
	///     Allows derived tests to run additional persistence setup.
	/// </summary>
	protected virtual Task InitializePersistenceAsync() => Task.CompletedTask;

	/// <summary>
	///     Retrieves a service from the container.
	/// </summary>
	protected TService? GetService<TService>() => _scope.ServiceProvider.GetService<TService>();

	/// <summary>
	///     Retrieves a service from the container.
	/// </summary>
	protected TService GetRequiredService<TService>() where TService : notnull =>
		_scope.ServiceProvider.GetRequiredService<TService>();

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
