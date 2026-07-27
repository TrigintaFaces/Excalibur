// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PersistenceProviderFactoryShould : IAsyncDisposable
{
	private readonly PersistenceConfiguration _config;
	private readonly IServiceProvider _serviceProvider;
	private readonly PersistenceProviderFactory _factory;

	public PersistenceProviderFactoryShould()
	{
		_config = new PersistenceConfiguration();
		var services = new ServiceCollection();
		services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
		_serviceProvider = services.BuildServiceProvider();
		var logger = NullLogger<PersistenceProviderFactory>.Instance;
		_factory = new PersistenceProviderFactory(_config, _serviceProvider, logger);
	}

	[Fact]
	public void ThrowForNonPersistenceConfiguration()
	{
		var config = A.Fake<IPersistenceConfiguration>();
		var logger = NullLogger<PersistenceProviderFactory>.Instance;

		Should.Throw<ArgumentException>(
			() => new PersistenceProviderFactory(config, _serviceProvider, logger));
	}

	[Fact]
	public void ThrowForNullServiceProvider()
	{
		var logger = NullLogger<PersistenceProviderFactory>.Instance;

		Should.Throw<ArgumentNullException>(
			() => new PersistenceProviderFactory(_config, null!, logger));
	}

	[Fact]
	public void ThrowForNullLogger()
	{
		Should.Throw<ArgumentNullException>(
			() => new PersistenceProviderFactory(_config, _serviceProvider, null!));
	}

	[Fact]
	public void GetProvider_ThrowsWhenNoDefault()
	{
		Should.Throw<InvalidOperationException>(() => _factory.GetProvider());
	}

	[Fact]
	public void GetProvider_ThrowsForNullName()
	{
		Should.Throw<ArgumentException>(() => _factory.GetProvider(null!));
	}

	[Fact]
	public void GetProvider_ThrowsForUnconfiguredProvider()
	{
		Should.Throw<InvalidOperationException>(() => _factory.GetProvider("nonexistent"));
	}

	[Fact]
	public void TryGetProvider_ReturnsFalseForUnconfiguredProvider()
	{
		var result = _factory.TryGetProvider("nonexistent", out var provider);
		result.ShouldBeFalse();
		provider.ShouldBeNull();
	}

	[Fact]
	public void TryGetProvider_ThrowsForNullName()
	{
		Should.Throw<ArgumentException>(() => _factory.TryGetProvider(null!, out _));
	}

	[Fact]
	public void GetProviderNames_ReturnsConfiguredProviders()
	{
		_config.Providers["test"] = new PersistenceProviderOptions
		{
			Name = "test",
			Type = PersistenceProviderType.InMemory,
			ConnectionString = "Mode=InMemory"
		};

		var names = _factory.GetProviderNames().ToList();
		names.ShouldContain("test");
	}

	[Fact]
	public void RegisterProvider_RegistersSuccessfully()
	{
		var provider = A.Fake<IPersistenceProvider>();
		_factory.RegisterProvider("custom", provider);

		var retrieved = _factory.GetProvider("custom");
		retrieved.ShouldBeSameAs(provider);
	}

	[Fact]
	public void RegisterProvider_ThrowsForDuplicate()
	{
		var provider = A.Fake<IPersistenceProvider>();
		_factory.RegisterProvider("dup", provider);

		Should.Throw<InvalidOperationException>(
			() => _factory.RegisterProvider("dup", provider));
	}

	[Fact]
	public void RegisterProvider_ThrowsForNullName()
	{
		var provider = A.Fake<IPersistenceProvider>();
		Should.Throw<ArgumentException>(() => _factory.RegisterProvider(null!, provider));
	}

	[Fact]
	public void RegisterProvider_ThrowsForNullProvider()
	{
		Should.Throw<ArgumentNullException>(() => _factory.RegisterProvider("test", null!));
	}

	[Fact]
	public void UnregisterProvider_RemovesProvider()
	{
		var provider = A.Fake<IPersistenceProvider>();
		_factory.RegisterProvider("to-remove", provider);

		var result = _factory.UnregisterProvider("to-remove");
		result.ShouldBeTrue();
	}

	[Fact]
	public void UnregisterProvider_ReturnsFalseForNonExistent()
	{
		var result = _factory.UnregisterProvider("nonexistent");
		result.ShouldBeFalse();
	}

	[Fact]
	public void UnregisterProvider_ThrowsForNullName()
	{
		Should.Throw<ArgumentException>(() => _factory.UnregisterProvider(null!));
	}

	[Fact]
	public async Task DisposeAllProvidersAsync_ClearsProviders()
	{
		var provider = A.Fake<IPersistenceProvider>();
		_factory.RegisterProvider("disposable", provider);

		await _factory.DisposeAllProvidersAsync();

		// After disposal, should not find provider
		_factory.TryGetProvider("disposable", out var found).ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeAsync_DoesNotThrow()
	{
		await Should.NotThrowAsync(() => _factory.DisposeAsync().AsTask());
	}

	[Fact]
	public void GetProvider_ResolvesKeyedProviderMatchingConfiguredType()
	{
		// The configured provider type must resolve the keyed IPersistenceProvider registered under the
		// matching key — NOT the ambient/bare IPersistenceProvider. Register a WRONG bare provider plus the
		// correct keyed one so a mis-resolution (the dead-switch bug that resolved typeof(IPersistenceProvider))
		// returns the wrong instance and this lock goes RED.
		var correct = A.Fake<IPersistenceProvider>();
		var wrong = A.Fake<IPersistenceProvider>();

		var services = new ServiceCollection();
		services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
		services.AddSingleton<IPersistenceProvider>(wrong);            // ambient/bare — must NOT be chosen
		services.AddKeyedSingleton<IPersistenceProvider>("inmemory", (_, _) => correct);
		var sp = services.BuildServiceProvider();

		var config = new PersistenceConfiguration();
		config.Providers["mem"] = new PersistenceProviderOptions
		{
			Name = "mem",
			Type = PersistenceProviderType.InMemory,
			ConnectionString = "Mode=InMemory"
		};
		var factory = new PersistenceProviderFactory(config, sp, NullLogger<PersistenceProviderFactory>.Instance);

		var resolved = factory.GetProvider("mem");

		resolved.ShouldBeSameAs(correct);
		resolved.ShouldNotBeSameAs(wrong);
	}

	[Fact]
	public void GetProvider_ResolvesCustomProviderByConfiguredName()
	{
		// A Custom provider resolves by its configured Name key (covers provider packages outside the
		// closed enum, e.g. mysql/opensearch).
		var mysql = A.Fake<IPersistenceProvider>();

		var services = new ServiceCollection();
		services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
		services.AddKeyedSingleton<IPersistenceProvider>("mysql", (_, _) => mysql);
		var sp = services.BuildServiceProvider();

		var config = new PersistenceConfiguration();
		config.Providers["mysql"] = new PersistenceProviderOptions
		{
			Name = "mysql",
			Type = PersistenceProviderType.Custom,
			ConnectionString = "Server=localhost"
		};
		var factory = new PersistenceProviderFactory(config, sp, NullLogger<PersistenceProviderFactory>.Instance);

		factory.GetProvider("mysql").ShouldBeSameAs(mysql);
	}

	[Fact]
	public void GetProvider_FailsFastWhenProviderKeyIsNotRegistered()
	{
		// Configured type has no keyed registration -> fail fast with a clear message rather than
		// mis-resolving the ambient interface (the dead-switch bug).
		var services = new ServiceCollection();
		services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
		services.AddKeyedSingleton<IPersistenceProvider>("inmemory", (_, _) => A.Fake<IPersistenceProvider>());
		var sp = services.BuildServiceProvider();

		var config = new PersistenceConfiguration();
		config.Providers["sql"] = new PersistenceProviderOptions
		{
			Name = "sql",
			Type = PersistenceProviderType.SqlServer,   // no "sqlserver" keyed registration
			ConnectionString = "Server=."
		};
		var factory = new PersistenceProviderFactory(config, sp, NullLogger<PersistenceProviderFactory>.Instance);

		var ex = Should.Throw<InvalidOperationException>(() => factory.GetProvider("sql"));
		ex.Message.ShouldContain("sqlserver");
	}

	public async ValueTask DisposeAsync()
	{
		await _factory.DisposeAsync();
	}
}
