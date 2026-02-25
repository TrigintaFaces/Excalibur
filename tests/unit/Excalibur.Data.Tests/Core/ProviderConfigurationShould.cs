// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProviderConfigurationShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		};

		config.MaxPoolSize.ShouldBe(100);
		config.ConnectionTimeout.ShouldBe(30);
		config.CommandTimeout.ShouldBe(30);
		config.EnableConnectionPooling.ShouldBeTrue();
		config.MinPoolSize.ShouldBe(0);
		config.MaxRetryAttempts.ShouldBe(3);
		config.RetryDelayMilliseconds.ShouldBe(1000);
		config.EnableDetailedLogging.ShouldBeFalse();
		config.EnableMetrics.ShouldBeTrue();
		config.IsReadOnly.ShouldBeFalse();
		config.ProviderSpecificOptions.ShouldNotBeNull();
	}

	[Fact]
	public void Validate_SucceedsForValidConfig()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		};

		Should.NotThrow(() => config.Validate());
	}

	[Fact]
	public void Validate_ThrowsForEmptyConnectionString()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = ""
		};

		Should.Throw<InvalidOperationException>(() => config.Validate());
	}

	[Fact]
	public void Validate_ThrowsForInvalidMaxPoolSize()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost",
			MaxPoolSize = 0
		};

		Should.Throw<InvalidOperationException>(() => config.Validate());
	}

	[Fact]
	public void Validate_ThrowsForInvalidConnectionTimeout()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost",
			ConnectionTimeout = 0
		};

		Should.Throw<InvalidOperationException>(() => config.Validate());
	}

	[Fact]
	public void Validate_ThrowsForInvalidCommandTimeout()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost",
			CommandTimeout = 0
		};

		Should.Throw<InvalidOperationException>(() => config.Validate());
	}

	[Fact]
	public void SetAndGetProviderSpecificOptions()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		};

		config.ProviderSpecificOptions["CustomKey"] = "CustomValue";
		config.ProviderSpecificOptions["CustomKey"].ShouldBe("CustomValue");
	}

	[Fact]
	public void ImplementIPersistenceOptions()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		};

		config.ShouldBeAssignableTo<IPersistenceOptions>();
	}

	[Fact]
	public void ImplementIPersistencePoolingOptions()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		};

		config.ShouldBeAssignableTo<IPersistencePoolingOptions>();
	}

	[Fact]
	public void ImplementIPersistenceResilienceOptions()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		};

		config.ShouldBeAssignableTo<IPersistenceResilienceOptions>();
	}

	[Fact]
	public void ImplementIPersistenceObservabilityOptions()
	{
		var config = new ProviderConfiguration
		{
			Name = "test",
			Type = PersistenceProviderType.SqlServer,
			ConnectionString = "Server=localhost"
		};

		config.ShouldBeAssignableTo<IPersistenceObservabilityOptions>();
	}
}
