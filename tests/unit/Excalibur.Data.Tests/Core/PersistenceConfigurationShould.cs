// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PersistenceConfigurationShould
{
	private readonly PersistenceConfiguration _config = new();

	[Fact]
	public void HaveDefaultProvider()
	{
		_config.DefaultProvider.ShouldBe("default");
	}

	[Fact]
	public void HaveEmptyProvidersCollection()
	{
		_config.Providers.ShouldBeEmpty();
	}

	[Fact]
	public void HaveGlobalOptions()
	{
		_config.GlobalOptions.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnDefaultProviderName()
	{
		_config.DefaultProviderName.ShouldBe("default");
	}

	[Fact]
	public void ThrowWhenAccessingConfigurationSectionBeforeSet()
	{
		Should.Throw<InvalidOperationException>(() => _ = _config.ConfigurationSection);
	}

	[Fact]
	public void RegisterProviderConfiguration_WithProviderConfig()
	{
		var providerConfig = new ProviderConfiguration
		{
			Name = "sql",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		};

		_config.RegisterProviderConfiguration("sql", providerConfig);

		_config.Providers.ShouldContainKey("sql");
		_config.Providers["sql"].Type.ShouldBe(PersistenceProviderType.SqlServer);
	}

	[Fact]
	public void RegisterProviderConfiguration_ThrowsForNullName()
	{
		var providerConfig = new ProviderConfiguration
		{
			Name = "x",
			Type = PersistenceProviderType.InMemory,
			ConnectionString = "x"
		};

		Should.Throw<ArgumentException>(() => _config.RegisterProviderConfiguration(null!, providerConfig));
	}

	[Fact]
	public void RegisterProviderConfiguration_ThrowsForNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => _config.RegisterProviderConfiguration("test", null!));
	}

	[Fact]
	public void RemoveProviderConfiguration_ReturnsTrueForExisting()
	{
		_config.RegisterProviderConfiguration("test", new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.InMemory,
			ConnectionString = "test"
		});

		_config.RemoveProviderConfiguration("test").ShouldBeTrue();
	}

	[Fact]
	public void RemoveProviderConfiguration_ReturnsFalseForNonExisting()
	{
		_config.RemoveProviderConfiguration("nonexistent").ShouldBeFalse();
	}

	[Fact]
	public void GetProviderOptions_ReturnsRegisteredOptions()
	{
		_config.RegisterProviderConfiguration("test", new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.InMemory,
			ConnectionString = "inmem"
		});

		var opts = _config.GetProviderOptions("test");
		opts.ShouldNotBeNull();
		opts.ConnectionString.ShouldBe("inmem");
	}

	[Fact]
	public void GetProviderOptions_ThrowsForUnknownProvider()
	{
		Should.Throw<ArgumentException>(() => _config.GetProviderOptions("unknown"));
	}

	[Fact]
	public void GetConfiguredProviders_ReturnsRegisteredNames()
	{
		_config.RegisterProviderConfiguration("p1", new ProviderConfiguration
		{
			Name = "p1",
			Type = PersistenceProviderType.InMemory,
			ConnectionString = "x"
		});

		_config.RegisterProviderConfiguration("p2", new ProviderConfiguration
		{
			Name = "p2",
			Type = PersistenceProviderType.Redis,
			ConnectionString = "y"
		});

		var providers = _config.GetConfiguredProviders().ToList();
		providers.ShouldContain("p1");
		providers.ShouldContain("p2");
	}

	[Fact]
	public void Validate_ReturnsSuccessForValidConfig()
	{
		_config.DefaultProvider = "sql";
		_config.RegisterProviderConfiguration("sql", new ProviderConfiguration
		{
			Name = "sql",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		});

		var results = _config.Validate().ToList();
		results.ShouldAllBe(r => r.IsValid);
	}

	[Fact]
	public void Validate_ReportsInvalidDefaultProvider()
	{
		_config.DefaultProvider = "nonexistent";

		var results = _config.Validate().ToList();
		results.ShouldContain(r => !r.IsValid);
	}

	[Fact]
	public void Validate_ReportsInvalidProviderConfig()
	{
		_config.DefaultProvider = "";
		_config.RegisterProviderConfiguration("bad", new ProviderConfiguration
		{
			Name = "",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "",
			MaxPoolSize = 0,
			ConnectionTimeout = 0,
			CommandTimeout = 0
		});

		var results = _config.Validate().ToList();
		results.ShouldContain(r => !r.IsValid);
	}

	[Fact]
	public void Reload_ThrowsForInvalidConfig()
	{
		_config.DefaultProvider = "nonexistent";

		Should.Throw<InvalidOperationException>(() => _config.Reload());
	}

	[Fact]
	public void Reload_SucceedsForValidConfig()
	{
		_config.DefaultProvider = "sql";
		_config.RegisterProviderConfiguration("sql", new ProviderConfiguration
		{
			Name = "sql",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		});

		Should.NotThrow(() => _config.Reload());
	}
}
