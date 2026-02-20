// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Core;

/// <summary>
/// Depth tests for <see cref="ConnectionStringProvider"/>.
/// Covers CRUD operations, validation, building/parsing, refresh, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConnectionStringProviderDepthShould : IDisposable
{
	private readonly ConnectionStringProvider _provider;

	public ConnectionStringProviderDepthShould()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Default"] = "Server=localhost;Database=test",
				["ConnectionStrings:Secondary"] = "Server=secondary;Database=test2",
			})
			.Build();

		var logger = NullLogger<ConnectionStringProvider>.Instance;
		_provider = new ConnectionStringProvider(config, logger);
	}

	[Fact]
	public void GetConnectionStringFromConfiguration()
	{
		// Act
		var result = _provider.GetConnectionString("Default");

		// Assert
		result.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public void ThrowWhenConnectionStringNotFound()
	{
		// Act & Assert
		Should.Throw<InvalidOperationException>(() => _provider.GetConnectionString("NonExistent"));
	}

	[Fact]
	public void ThrowWhenNameIsNullOrWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => _provider.GetConnectionString(null!));
		Should.Throw<ArgumentException>(() => _provider.GetConnectionString(""));
		Should.Throw<ArgumentException>(() => _provider.GetConnectionString("   "));
	}

	[Fact]
	public void TryGetConnectionStringReturnTrueWhenExists()
	{
		// Act
		var found = _provider.TryGetConnectionString("Default", out var connectionString);

		// Assert
		found.ShouldBeTrue();
		connectionString.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public void TryGetConnectionStringReturnFalseWhenNotExists()
	{
		// Act
		var found = _provider.TryGetConnectionString("NonExistent", out var connectionString);

		// Assert
		found.ShouldBeFalse();
		connectionString.ShouldBeNull();
	}

	[Fact]
	public void SetAndRetrieveConnectionString()
	{
		// Act
		_provider.SetConnectionString("CustomDB", "Server=custom;Database=db");
		var result = _provider.GetConnectionString("CustomDB");

		// Assert
		result.ShouldBe("Server=custom;Database=db");
	}

	[Fact]
	public void ThrowWhenSetConnectionStringNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => _provider.SetConnectionString("", "value"));
	}

	[Fact]
	public void ThrowWhenSetConnectionStringValueIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => _provider.SetConnectionString("name", ""));
	}

	[Fact]
	public void RemoveConnectionString()
	{
		// Arrange
		_provider.SetConnectionString("ToRemove", "Server=x");

		// Act
		var removed = _provider.RemoveConnectionString("ToRemove");

		// Assert
		removed.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenRemovingNonExistentConnectionString()
	{
		// Act
		var removed = _provider.RemoveConnectionString("DoesNotExist");

		// Assert
		removed.ShouldBeFalse();
	}

	[Fact]
	public void GetConnectionStringNamesFromCacheAndConfig()
	{
		// Arrange
		_provider.SetConnectionString("Extra", "Server=extra");

		// Act
		var names = _provider.GetConnectionStringNames().ToList();

		// Assert
		names.ShouldContain("Default");
		names.ShouldContain("Secondary");
		names.ShouldContain("Extra");
	}

	[Fact]
	public void ConnectionStringExistsReturnTrueForConfigured()
	{
		// Act & Assert
		_provider.ConnectionStringExists("Default").ShouldBeTrue();
	}

	[Fact]
	public void ConnectionStringExistsReturnFalseForMissing()
	{
		// Act & Assert
		_provider.ConnectionStringExists("Missing").ShouldBeFalse();
	}

	[Fact]
	public void BuildConnectionStringFromParameters()
	{
		// Arrange
		var parameters = new Dictionary<string, string>
		{
			["Server"] = "localhost",
			["Database"] = "mydb",
		};

		// Act
		var result = _provider.BuildConnectionString(parameters);

		// Assert
		result.ShouldNotBeNullOrWhiteSpace();
		result.ShouldContain("server=localhost");
		result.ShouldContain("database=mydb");
	}

	[Fact]
	public void ThrowWhenBuildParametersIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _provider.BuildConnectionString(null!));
	}

	[Fact]
	public void ParseConnectionStringIntoParameters()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=mydb;Timeout=30";

		// Act
		var result = _provider.ParseConnectionString(connectionString);

		// Assert
		result.ShouldContainKey("Server");
		result.ShouldContainKey("Database");
	}

	[Fact]
	public void ThrowWhenParseConnectionStringIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => _provider.ParseConnectionString(""));
	}

	[Fact]
	public void ValidateSqlServerConnectionString()
	{
		// Act & Assert
		_provider.ValidateConnectionString("Server=localhost;Database=test", "SqlServer").ShouldBeTrue();
		_provider.ValidateConnectionString("Data Source=localhost;Database=test", "MSSQL").ShouldBeTrue();
	}

	[Fact]
	public void ValidatePostgresConnectionString()
	{
		// Act & Assert
		_provider.ValidateConnectionString("Host=localhost;Database=test", "Postgres").ShouldBeTrue();
		_provider.ValidateConnectionString("Server=localhost;Database=test", "Postgres").ShouldBeTrue();
	}

	[Fact]
	public void ValidateMongoDbConnectionString()
	{
		// MongoDB URIs are validated by provider-specific logic before DbConnectionStringBuilder
		_provider.ValidateConnectionString("mongodb://localhost:27017/test", "MongoDB").ShouldBeTrue();
		_provider.ValidateConnectionString("invalid", "MongoDB").ShouldBeFalse();
	}

	[Fact]
	public void ValidateRedisConnectionString()
	{
		// Redis host:port strings are validated by provider-specific logic before DbConnectionStringBuilder
		_provider.ValidateConnectionString("localhost:6379", "Redis").ShouldBeTrue();
	}

	[Fact]
	public void ValidateInMemoryConnectionString()
	{
		// Note: DbConnectionStringBuilder throws on non-key=value formats like "anything"
		// causing the catch block to return false. Use key=value format for InMemory validation.
		_provider.ValidateConnectionString("Provider=InMemory", "InMemory").ShouldBeTrue();
		_provider.ValidateConnectionString("Provider=InMemory", "Memory").ShouldBeTrue();
	}

	[Fact]
	public void ValidateUnknownProviderType()
	{
		// Act & Assert - basic validation (builder.Count > 0 for key=value format)
		_provider.ValidateConnectionString("key=value", "UnknownProvider").ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForInvalidConnectionString()
	{
		// Arrange - SQL Server without required keys
		// Act & Assert
		_provider.ValidateConnectionString("InvalidKey=InvalidValue", "SqlServer").ShouldBeFalse();
	}

	[Fact]
	public async Task GetConnectionStringAsyncFromCache()
	{
		// Arrange
		_provider.SetConnectionString("Cached", "Server=cached");

		// Act
		var result = await _provider.GetConnectionStringAsync("Cached", CancellationToken.None);

		// Assert
		result.ShouldBe("Server=cached");
	}

	[Fact]
	public async Task GetConnectionStringAsyncFallbackToSync()
	{
		// Act
		var result = await _provider.GetConnectionStringAsync("Default", CancellationToken.None);

		// Assert
		result.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public async Task RefreshAsyncReloadsFromConfiguration()
	{
		// Act - should not throw
		await _provider.RefreshAsync(CancellationToken.None);

		// Assert - existing config values should still be accessible
		_provider.GetConnectionString("Default").ShouldBe("Server=localhost;Database=test");
	}

	public void Dispose()
	{
		_provider.Dispose();
	}
}
