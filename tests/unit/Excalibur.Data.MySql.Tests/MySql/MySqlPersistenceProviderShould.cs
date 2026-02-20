// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.MySql;


namespace Excalibur.Data.Tests.MySql;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MySqlPersistenceProviderShould
{
	private static Microsoft.Extensions.Options.IOptions<MySqlProviderOptions> CreateOptions(
		Action<MySqlProviderOptions>? configure = null)
	{
		var options = new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;",
			ConnectTimeout = 1,
			CommandTimeout = 1,
			MaxRetryCount = 0
		};

		configure?.Invoke(options);
		return Microsoft.Extensions.Options.Options.Create(options);
	}

	private static MySqlPersistenceProvider CreateProvider(Action<MySqlProviderOptions>? configure = null) =>
		new(CreateOptions(configure), EnabledTestLogger.Create<MySqlPersistenceProvider>());

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();

		Should.Throw<ArgumentNullException>(
			() => new MySqlPersistenceProvider(null!, logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});

		Should.Throw<ArgumentNullException>(
			() => new MySqlPersistenceProvider(options, null!));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsEmpty()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = string.Empty
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();

		Should.Throw<ArgumentException>(
			() => new MySqlPersistenceProvider(options, logger));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsWhitespace()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "   "
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();

		Should.Throw<ArgumentException>(
			() => new MySqlPersistenceProvider(options, logger));
	}

	[Fact]
	public void HaveCorrectProviderType()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		provider.ProviderType.ShouldBe("SQL");
	}

	[Fact]
	public void HaveDefaultName()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		provider.Name.ShouldBe("mysql");
	}

	[Fact]
	public void UseCustomName()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;",
			Name = "custom-mysql"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		provider.Name.ShouldBe("custom-mysql");
	}

	[Fact]
	public void NotBeAvailableBeforeInitialization()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public void ReturnSelfForHealthService()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		var health = provider.GetService(typeof(IPersistenceProviderHealth));
		health.ShouldBeSameAs(provider);
	}

	[Fact]
	public void ReturnSelfForTransactionService()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		var transaction = provider.GetService(typeof(IPersistenceProviderTransaction));
		transaction.ShouldBeSameAs(provider);
	}

	[Fact]
	public void ReturnNullForUnknownService()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		var unknown = provider.GetService(typeof(string));
		unknown.ShouldBeNull();
	}

	[Fact]
	public void ThrowForNullServiceType()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		Should.Throw<ArgumentNullException>(() => provider.GetService(null!));
	}

	[Fact]
	public void HaveRetryPolicy()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		provider.RetryPolicy.ShouldNotBeNull();
	}

	[Fact]
	public void HaveConnectionString()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		using var provider = new MySqlPersistenceProvider(options, logger);

		provider.ConnectionString.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void DisposeWithoutThrowingSync()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		var provider = new MySqlPersistenceProvider(options, logger);

		Should.NotThrow(() => provider.Dispose());
	}

	[Fact]
	public async Task DisposeWithoutThrowingAsync()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		var provider = new MySqlPersistenceProvider(options, logger);

		await Should.NotThrowAsync(() => provider.DisposeAsync().AsTask());
	}

	[Fact]
	public void DoubleDisposeWithoutThrowing()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MySqlProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;"
		});
		var logger = EnabledTestLogger.Create<MySqlPersistenceProvider>();
		var provider = new MySqlPersistenceProvider(options, logger);

		provider.Dispose();
		Should.NotThrow(() => provider.Dispose());
	}

	[Fact]
	public void CreateTransactionScope()
	{
		using var provider = CreateProvider();

		using var scope = provider.CreateTransactionScope();
		scope.ShouldNotBeNull();
		scope.ShouldBeAssignableTo<ITransactionScope>();
	}

	[Fact]
	public void CreateTransactionScope_UsesConfiguredIsolationAndTimeout()
	{
		using var provider = CreateProvider();

		using var scope = provider.CreateTransactionScope(
			System.Data.IsolationLevel.Serializable,
			TimeSpan.FromSeconds(45));

		var mySqlScope = scope.ShouldBeOfType<MySqlTransactionScope>();
		mySqlScope.IsolationLevel.ShouldBe(System.Data.IsolationLevel.Serializable);
		mySqlScope.Timeout.ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public async Task ExecuteAsync_ThrowsWhenRequestIsNull()
	{
		using var provider = CreateProvider();
		await Should.ThrowAsync<ArgumentNullException>(() =>
			provider.ExecuteAsync<System.IDisposable, int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_ThrowsWhenDisposed()
	{
		var provider = CreateProvider();
		await provider.DisposeAsync();

		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<System.IDisposable, int>>();
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			provider.ExecuteAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteInTransactionAsync_ThrowsWhenRequestIsNull()
	{
		using var provider = CreateProvider();
		var tx = A.Fake<ITransactionScope>();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			provider.ExecuteInTransactionAsync<System.IDisposable, int>(
				null!,
				tx,
				CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteInTransactionAsync_ThrowsWhenTransactionScopeIsNull()
	{
		using var provider = CreateProvider();
		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<System.IDisposable, int>>();

		await Should.ThrowAsync<ArgumentNullException>(() =>
			provider.ExecuteInTransactionAsync(
				request,
				null!,
				CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteInTransactionAsync_ThrowsWhenDisposed()
	{
		var provider = CreateProvider();
		await provider.DisposeAsync();

		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<System.IDisposable, int>>();
		var tx = A.Fake<ITransactionScope>();
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			provider.ExecuteInTransactionAsync(
				request,
				tx,
				CancellationToken.None));
	}

	[Fact]
	public async Task CreateConnectionAsync_ThrowsWhenDisposed()
	{
		var provider = CreateProvider();
		await provider.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(() =>
			provider.CreateConnectionAsync(CancellationToken.None).AsTask());
	}

	[Fact]
	public void CreateTransactionScope_ThrowsWhenDisposed()
	{
		var provider = CreateProvider();
		provider.Dispose();

		Should.Throw<ObjectDisposedException>(() => provider.CreateTransactionScope());
	}

	[Fact]
	public async Task TestConnectionAsync_ReturnsFalseWhenServerIsUnavailable()
	{
		using var provider = CreateProvider(options =>
		{
			options.ConnectionString = "Server=127.0.0.1;Port=1;Database=test;User ID=test;Password=test;";
			options.ConnectTimeout = 1;
			options.MaxRetryCount = 0;
		});

		var ok = await provider.TestConnectionAsync(CancellationToken.None);
		ok.ShouldBeFalse();
	}

	[Fact]
	public async Task GetMetricsAsync_ReturnsCoreMetricsWhenConnectionFails()
	{
		using var provider = CreateProvider(options =>
		{
			options.ConnectionString = "Server=127.0.0.1;Port=1;Database=test;User ID=test;Password=test;";
			options.ConnectTimeout = 1;
			options.MaxRetryCount = 0;
		});

		var metrics = await provider.GetMetricsAsync(CancellationToken.None);

		metrics.ShouldContainKey("Provider");
		metrics["Provider"].ShouldBe("MySQL");
		metrics.ShouldContainKey("Name");
		metrics.ShouldContainKey("IsAvailable");
		metrics.ShouldContainKey("EnablePooling");
	}

	[Fact]
	public async Task GetConnectionPoolStatsAsync_ReturnsNullWhenConnectionFails()
	{
		using var provider = CreateProvider(options =>
		{
			options.ConnectionString = "Server=127.0.0.1;Port=1;Database=test;User ID=test;Password=test;";
			options.ConnectTimeout = 1;
			options.MaxRetryCount = 0;
		});

		var stats = await provider.GetConnectionPoolStatsAsync(CancellationToken.None);
		stats.ShouldBeNull();
	}

	[Fact]
	public async Task InitializeAsync_ThrowsWhenConnectionTestFails()
	{
		using var provider = CreateProvider(options =>
		{
			options.ConnectionString = "Server=127.0.0.1;Port=1;Database=test;User ID=test;Password=test;";
			options.ConnectTimeout = 1;
			options.MaxRetryCount = 0;
		});

		var persistenceOptions = A.Fake<IPersistenceOptions>();
		await Should.ThrowAsync<InvalidOperationException>(() =>
			provider.InitializeAsync(persistenceOptions, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_PropagatesConnectionFailure_WhenServerUnavailable()
	{
		using var provider = CreateProvider(options =>
		{
			options.ConnectionString = "Server=127.0.0.1;Port=1;Database=test;User ID=test;Password=test;";
			options.ConnectTimeout = 1;
			options.MaxRetryCount = 0;
		});

		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<System.IDisposable, int>>();
		A.CallTo(() => request.ResolveAsync).Returns(new Func<System.IDisposable, Task<int>>(_ => Task.FromResult(1)));

		await Should.ThrowAsync<Exception>(() =>
			provider.ExecuteAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteInTransactionAsync_EnlistsProvider_AndPropagatesConnectionFailure()
	{
		using var provider = CreateProvider(options =>
		{
			options.ConnectionString = "Server=127.0.0.1;Port=1;Database=test;User ID=test;Password=test;";
			options.ConnectTimeout = 1;
			options.MaxRetryCount = 0;
		});

		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<System.IDisposable, int>>();
		A.CallTo(() => request.ResolveAsync).Returns(new Func<System.IDisposable, Task<int>>(_ => Task.FromResult(1)));

		var tx = A.Fake<ITransactionScope>();
		A.CallTo(() => tx.EnlistProviderAsync(provider, A<CancellationToken>._)).Returns(Task.CompletedTask);

		await Should.ThrowAsync<Exception>(() =>
			provider.ExecuteInTransactionAsync(request, tx, CancellationToken.None));

		A.CallTo(() => tx.EnlistProviderAsync(provider, A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task CreateConnectionAsync_Throws_WhenOpenFails()
	{
		using var provider = CreateProvider(options =>
		{
			options.ConnectionString = "Server=127.0.0.1;Port=1;Database=test;User ID=test;Password=test;";
			options.ConnectTimeout = 1;
		});

		await Should.ThrowAsync<Exception>(() =>
			provider.CreateConnectionAsync(CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DisposeAsync_WithClearPoolOnDispose_UsesClearPoolPath()
	{
		var provider = CreateProvider(options =>
		{
			options.ClearPoolOnDispose = true;
		});

		await Should.NotThrowAsync(() => provider.DisposeAsync().AsTask());
	}
}

