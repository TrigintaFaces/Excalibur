// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
public class ConnectionStringProviderFunctionalShould
{
	private static (ConnectionStringProvider provider, IConfigurationRoot config) CreateProvider(
		Dictionary<string, string?>? connectionStrings = null)
	{
		var configData = new Dictionary<string, string?>(StringComparer.Ordinal);

		if (connectionStrings != null)
		{
			foreach (var kvp in connectionStrings)
			{
				configData[$"ConnectionStrings:{kvp.Key}"] = kvp.Value;
			}
		}

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		var provider = new ConnectionStringProvider(config, NullLogger<ConnectionStringProvider>.Instance);
		return (provider, config);
	}

	[Fact]
	public void Constructor_WithNullConfiguration_ShouldThrow()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ConnectionStringProvider(null!, NullLogger<ConnectionStringProvider>.Instance));
	}

	[Fact]
	public void Constructor_WithNullLogger_ShouldThrow()
	{
		var config = new ConfigurationBuilder().Build();
		Should.Throw<ArgumentNullException>(() =>
			new ConnectionStringProvider(config, null!));
	}

	[Fact]
	public void GetConnectionString_ShouldReturnConfiguredValue()
	{
		var (provider, _) = CreateProvider(new Dictionary<string, string?>
		{
			["DefaultDb"] = "Server=localhost;Database=test"
		});

		var result = provider.GetConnectionString("DefaultDb");

		result.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public void GetConnectionString_NotFound_ShouldThrow()
	{
		var (provider, _) = CreateProvider();

		Should.Throw<InvalidOperationException>(() =>
			provider.GetConnectionString("NonExistent"));
	}

	[Fact]
	public void GetConnectionString_WithNullName_ShouldThrow()
	{
		var (provider, _) = CreateProvider();

		Should.Throw<ArgumentException>(() => provider.GetConnectionString(null!));
	}

	[Fact]
	public void GetConnectionString_WithEmptyName_ShouldThrow()
	{
		var (provider, _) = CreateProvider();

		Should.Throw<ArgumentException>(() => provider.GetConnectionString(""));
	}

	[Fact]
	public void TryGetConnectionString_Found_ShouldReturnTrue()
	{
		var (provider, _) = CreateProvider(new Dictionary<string, string?>
		{
			["DefaultDb"] = "Server=localhost;Database=test"
		});

		var found = provider.TryGetConnectionString("DefaultDb", out var connectionString);

		found.ShouldBeTrue();
		connectionString.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public void TryGetConnectionString_NotFound_ShouldReturnFalse()
	{
		var (provider, _) = CreateProvider();

		var found = provider.TryGetConnectionString("NonExistent", out var connectionString);

		found.ShouldBeFalse();
		connectionString.ShouldBeNull();
	}

	[Fact]
	public void SetConnectionString_ShouldStoreValue()
	{
		var (provider, _) = CreateProvider();

		provider.SetConnectionString("NewDb", "Server=remote;Database=newdb");

		var result = provider.GetConnectionString("NewDb");
		result.ShouldBe("Server=remote;Database=newdb");
	}

	[Fact]
	public void SetConnectionString_WithNullName_ShouldThrow()
	{
		var (provider, _) = CreateProvider();

		Should.Throw<ArgumentException>(() =>
			provider.SetConnectionString(null!, "Server=localhost"));
	}

	[Fact]
	public void SetConnectionString_WithNullValue_ShouldThrow()
	{
		var (provider, _) = CreateProvider();

		Should.Throw<ArgumentException>(() =>
			provider.SetConnectionString("Test", null!));
	}

	[Fact]
	public void RemoveConnectionString_Existing_ShouldReturnTrue()
	{
		var (provider, _) = CreateProvider(new Dictionary<string, string?>
		{
			["ToRemove"] = "Server=localhost"
		});

		var removed = provider.RemoveConnectionString("ToRemove");

		removed.ShouldBeTrue();
	}

	[Fact]
	public void RemoveConnectionString_NonExisting_ShouldReturnFalse()
	{
		var (provider, _) = CreateProvider();

		var removed = provider.RemoveConnectionString("NonExistent");

		removed.ShouldBeFalse();
	}

	[Fact]
	public void GetConnectionStringNames_ShouldReturnAllNames()
	{
		var (provider, _) = CreateProvider(new Dictionary<string, string?>
		{
			["Db1"] = "Server=localhost;Database=db1",
			["Db2"] = "Server=localhost;Database=db2"
		});

		var names = provider.GetConnectionStringNames().ToList();

		names.ShouldContain("Db1");
		names.ShouldContain("Db2");
	}

	[Fact]
	public void ConnectionStringExists_Existing_ShouldReturnTrue()
	{
		var (provider, _) = CreateProvider(new Dictionary<string, string?>
		{
			["DefaultDb"] = "Server=localhost"
		});

		provider.ConnectionStringExists("DefaultDb").ShouldBeTrue();
	}

	[Fact]
	public void ConnectionStringExists_NonExisting_ShouldReturnFalse()
	{
		var (provider, _) = CreateProvider();

		provider.ConnectionStringExists("NonExistent").ShouldBeFalse();
	}

	[Fact]
	public void BuildConnectionString_ShouldCreateValidString()
	{
		var (provider, _) = CreateProvider();

		var result = provider.BuildConnectionString(new Dictionary<string, string>
		{
			["Server"] = "localhost",
			["Database"] = "testdb",
			["Trusted_Connection"] = "True"
		});

		result.ShouldNotBeNullOrWhiteSpace();
		result.ShouldContain("Server=localhost");
	}

	[Fact]
	public void BuildConnectionString_WithNull_ShouldThrow()
	{
		var (provider, _) = CreateProvider();

		Should.Throw<ArgumentNullException>(() =>
			provider.BuildConnectionString(null!));
	}

	[Fact]
	public void ParseConnectionString_ShouldExtractParameters()
	{
		var (provider, _) = CreateProvider();

		var result = provider.ParseConnectionString("Server=localhost;Database=testdb");

		result.ShouldContainKey("Server");
		result["Server"].ShouldBe("localhost");
		result.ShouldContainKey("Database");
		result["Database"].ShouldBe("testdb");
	}

	[Fact]
	public void ParseConnectionString_WithNull_ShouldThrow()
	{
		var (provider, _) = CreateProvider();

		Should.Throw<ArgumentException>(() =>
			provider.ParseConnectionString(null!));
	}

	[Theory]
	[InlineData("Data Source=localhost;Initial Catalog=test", "SqlServer", true)]
	[InlineData("Server=localhost;Database=test", "SQLSERVER", true)]
	[InlineData("Host=localhost;Database=test", "Postgres", true)]
	[InlineData("Server=localhost;Database=test", "PGSQL", true)]
	[InlineData("mongodb://localhost:27017/test", "MongoDB", true)]
	[InlineData("mongodb+srv://cluster.example.com/test", "MONGO", true)]
	[InlineData("localhost:6379", "Redis", true)]
	[InlineData("anything", "InMemory", true)]
	[InlineData("Server=localhost", "MSSQL", true)]
	public void ValidateConnectionString_ShouldValidateCorrectly(
		string connectionString, string providerType, bool expected)
	{
		var (provider, _) = CreateProvider();

		var result = provider.ValidateConnectionString(connectionString, providerType);

		result.ShouldBe(expected);
	}

	[Fact]
	public void ValidateConnectionString_InvalidMongoDB_ShouldReturnFalse()
	{
		var (provider, _) = CreateProvider();

		var result = provider.ValidateConnectionString("not-a-mongo-string", "MongoDB");

		result.ShouldBeFalse();
	}

	[Fact]
	public void ValidateConnectionString_WithNullConnectionString_ShouldThrow()
	{
		var (provider, _) = CreateProvider();

		Should.Throw<ArgumentException>(() =>
			provider.ValidateConnectionString(null!, "SqlServer"));
	}

	[Fact]
	public void ValidateConnectionString_WithNullProviderType_ShouldThrow()
	{
		var (provider, _) = CreateProvider();

		Should.Throw<ArgumentException>(() =>
			provider.ValidateConnectionString("Server=localhost", null!));
	}

	[Fact]
	public async Task RefreshAsync_ShouldReloadFromConfiguration()
	{
		var (provider, _) = CreateProvider(new Dictionary<string, string?>
		{
			["DefaultDb"] = "Server=localhost;Database=test"
		});

		// Set an additional connection string
		provider.SetConnectionString("Extra", "Server=extra");

		// Refresh should reload from configuration
		await provider.RefreshAsync(CancellationToken.None).ConfigureAwait(false);

		// The config-based one should still exist
		provider.GetConnectionString("DefaultDb").ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public async Task GetConnectionStringAsync_Found_ShouldReturnValue()
	{
		var (provider, _) = CreateProvider(new Dictionary<string, string?>
		{
			["DefaultDb"] = "Server=localhost;Database=test"
		});

		var result = await provider.GetConnectionStringAsync("DefaultDb", CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public void Dispose_ShouldNotThrow()
	{
		var (provider, _) = CreateProvider();

		Should.NotThrow(() => provider.Dispose());
	}

	[Fact]
	public void SetConnectionString_ShouldOverridePreviousValue()
	{
		var (provider, _) = CreateProvider(new Dictionary<string, string?>
		{
			["DefaultDb"] = "Server=original"
		});

		provider.SetConnectionString("DefaultDb", "Server=updated");

		provider.GetConnectionString("DefaultDb").ShouldBe("Server=updated");
	}
}
