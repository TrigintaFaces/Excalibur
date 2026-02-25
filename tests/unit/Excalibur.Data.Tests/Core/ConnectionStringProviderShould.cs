// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConnectionStringProviderShould : UnitTestBase
{
	private readonly ConnectionStringProvider _provider;

	public ConnectionStringProviderShould()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Default"] = "Server=localhost;Database=test",
				["ConnectionStrings:Secondary"] = "Server=remote;Database=test2",
			})
			.Build();

		_provider = new ConnectionStringProvider(config, NullLogger<ConnectionStringProvider>.Instance);
	}

	[Fact]
	public void GetConnectionString_ReturnsConfiguredValue()
	{
		var result = _provider.GetConnectionString("Default");
		result.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public void GetConnectionString_ThrowsForUnknownName()
	{
		Should.Throw<InvalidOperationException>(() => _provider.GetConnectionString("NonExistent"));
	}

	[Fact]
	public void GetConnectionString_ThrowsForNullName()
	{
		Should.Throw<ArgumentException>(() => _provider.GetConnectionString(null!));
	}

	[Fact]
	public void GetConnectionString_ThrowsForEmptyName()
	{
		Should.Throw<ArgumentException>(() => _provider.GetConnectionString(""));
	}

	[Fact]
	public async Task GetConnectionStringAsync_ReturnsConfiguredValue()
	{
		var result = await _provider.GetConnectionStringAsync("Default", CancellationToken.None);
		result.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public async Task GetConnectionStringAsync_ThrowsForUnknownName()
	{
		await Should.ThrowAsync<InvalidOperationException>(
			() => _provider.GetConnectionStringAsync("NonExistent", CancellationToken.None));
	}

	[Fact]
	public void TryGetConnectionString_ReturnsTrueForKnownName()
	{
		var result = _provider.TryGetConnectionString("Default", out var cs);
		result.ShouldBeTrue();
		cs.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public void TryGetConnectionString_ReturnsFalseForUnknownName()
	{
		var result = _provider.TryGetConnectionString("NonExistent", out var cs);
		result.ShouldBeFalse();
		cs.ShouldBeNull();
	}

	[Fact]
	public void SetConnectionString_StoresValue()
	{
		_provider.SetConnectionString("NewConn", "Server=new;Database=db");
		var result = _provider.GetConnectionString("NewConn");
		result.ShouldBe("Server=new;Database=db");
	}

	[Fact]
	public void SetConnectionString_ThrowsForNullName()
	{
		Should.Throw<ArgumentException>(() => _provider.SetConnectionString(null!, "conn"));
	}

	[Fact]
	public void SetConnectionString_ThrowsForNullValue()
	{
		Should.Throw<ArgumentException>(() => _provider.SetConnectionString("name", null!));
	}

	[Fact]
	public void RemoveConnectionString_ReturnsTrueForExisting()
	{
		_provider.SetConnectionString("ToRemove", "Server=x");
		var result = _provider.RemoveConnectionString("ToRemove");
		result.ShouldBeTrue();
	}

	[Fact]
	public void RemoveConnectionString_ReturnsFalseForNonExisting()
	{
		var result = _provider.RemoveConnectionString("NonExistent");
		result.ShouldBeFalse();
	}

	[Fact]
	public void GetConnectionStringNames_ReturnsAllNames()
	{
		var names = _provider.GetConnectionStringNames().ToList();
		names.ShouldContain("Default");
		names.ShouldContain("Secondary");
	}

	[Fact]
	public async Task RefreshAsync_ReloadsFromConfiguration()
	{
		await _provider.RefreshAsync(CancellationToken.None);
		var result = _provider.GetConnectionString("Default");
		result.ShouldBe("Server=localhost;Database=test");
	}

	[Fact]
	public void ConnectionStringExists_ReturnsTrueForKnown()
	{
		_provider.ConnectionStringExists("Default").ShouldBeTrue();
	}

	[Fact]
	public void ConnectionStringExists_ReturnsFalseForUnknown()
	{
		_provider.ConnectionStringExists("NonExistent").ShouldBeFalse();
	}

	[Fact]
	public void BuildConnectionString_BuildsFromParameters()
	{
		var parameters = new Dictionary<string, string>
		{
			["Server"] = "localhost",
			["Database"] = "mydb"
		};
		var result = _provider.BuildConnectionString(parameters);
		result.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void BuildConnectionString_ThrowsForNull()
	{
		Should.Throw<ArgumentNullException>(() => _provider.BuildConnectionString(null!));
	}

	[Fact]
	public void ParseConnectionString_ParsesCorrectly()
	{
		var result = _provider.ParseConnectionString("Server=localhost;Database=mydb");
		result.ShouldContainKey("server");
		result.ShouldContainKey("database");
	}

	[Fact]
	public void ParseConnectionString_ThrowsForNull()
	{
		Should.Throw<ArgumentException>(() => _provider.ParseConnectionString(null!));
	}

	[Fact]
	public void ValidateConnectionString_ReturnsTrueForValidSqlServer()
	{
		_provider.ValidateConnectionString("Server=localhost;Database=test", "SQLSERVER").ShouldBeTrue();
	}

	[Fact]
	public void ValidateConnectionString_ReturnsTrueForValidPostgres()
	{
		_provider.ValidateConnectionString("Host=localhost;Database=test", "Postgres").ShouldBeTrue();
	}

	[Fact]
	public void ValidateConnectionString_ReturnsTrueForInMemory()
	{
		// InMemory connections need key=value format to pass DbConnectionStringBuilder
		_provider.ValidateConnectionString("Mode=InMemory", "INMEMORY").ShouldBeTrue();
	}

	[Fact]
	public void ValidateConnectionString_ReturnsTrueForMongoDbUri()
	{
		// MongoDB URIs are validated by provider-specific logic before DbConnectionStringBuilder
		_provider.ValidateConnectionString("mongodb://localhost:27017", "MONGODB").ShouldBeTrue();
	}

	[Fact]
	public void ValidateConnectionString_ReturnsTrueForRedisHostPort()
	{
		// Redis host:port strings are validated by provider-specific logic before DbConnectionStringBuilder
		_provider.ValidateConnectionString("localhost:6379", "REDIS").ShouldBeTrue();
	}

	[Fact]
	public void ValidateConnectionString_HandlesMssqlAlias()
	{
		_provider.ValidateConnectionString("Data Source=localhost;Database=test", "MSSQL").ShouldBeTrue();
	}

	[Fact]
	public void ValidateConnectionString_HandlesPostgresAlias()
	{
		_provider.ValidateConnectionString("Server=localhost;Database=test", "POSTGRES").ShouldBeTrue();
	}

	[Fact]
	public void ValidateConnectionString_HandlesPgsqlAlias()
	{
		_provider.ValidateConnectionString("Server=localhost;Database=test", "PGSQL").ShouldBeTrue();
	}

	[Fact]
	public void ValidateConnectionString_ReturnsTrueForUnknownProviderWithValidFormat()
	{
		// Unknown provider type - basic validation (builder.Count > 0)
		_provider.ValidateConnectionString("Key=Value", "UNKNOWN").ShouldBeTrue();
	}

	[Fact]
	public void ValidateConnectionString_ThrowsForNullConnectionString()
	{
		Should.Throw<ArgumentException>(() => _provider.ValidateConnectionString(null!, "SQLSERVER"));
	}

	[Fact]
	public void ValidateConnectionString_ThrowsForNullProviderType()
	{
		Should.Throw<ArgumentException>(() => _provider.ValidateConnectionString("Server=localhost", null!));
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		Should.NotThrow(() => _provider.Dispose());
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_provider.Dispose();
		}

		base.Dispose(disposing);
	}
}
