// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

using Microsoft.Extensions.Options;

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Persistence;

/// <summary>
/// Unit tests for SqlServerPersistenceProvider test stub.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServerPersistenceProvider")]
public sealed class SqlServerPersistenceProviderShould : UnitTestBase
{
	private readonly ILogger<SqlServerPersistenceProvider> _logger;
	private readonly IOptions<SqlServerProviderOptions> _options;
	private readonly SqlServerPersistenceProvider _provider;
	private readonly SqlServerProviderOptions _optionsValue;

	public SqlServerPersistenceProviderShould()
	{
		_logger = A.Fake<ILogger<SqlServerPersistenceProvider>>();
		_optionsValue = new SqlServerProviderOptions
		{
			ConnectionString = "Server=localhost;Database=test;User Id=sa;Password=Test123!;",
			EnablePooling = true,
			MinPoolSize = 2,
			MaxPoolSize = 20,
			EnableMars = true,
			ApplicationName = "TestApp",
			CommandTimeout = 30,
		};
		_options = Microsoft.Extensions.Options.Options.Create(_optionsValue);
		_provider = new SqlServerPersistenceProvider(_options, _logger);
	}

	[Fact]
	public void InitializeWithCorrectProperties()
	{
		// Assert
		_ = _provider.ShouldNotBeNull();
		_provider.Name.ShouldBe("SqlServer");
		_provider.ProviderType.ShouldBe("SQL");
	}

	[Fact]
	public void ProviderImplementsIPersistenceProvider()
	{
		// Assert
		_ = _provider.ShouldBeAssignableTo<IPersistenceProvider>();
	}

	[Fact]
	public void ProviderIsDisposable()
	{
		// Assert
		_ = _provider.ShouldBeAssignableTo<IDisposable>();
		_ = _provider.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void ProviderHasCorrectProperties()
	{
		// Assert
		_provider.Name.ShouldBe("SqlServer");
		_provider.ProviderType.ShouldBe("SQL");
		_provider.ConnectionString.ShouldBe(_optionsValue.ConnectionString);
	}

	[Fact]
	public void DisposeDoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(_provider.Dispose);
	}

	[Fact]
	public async Task DisposeAsyncDoesNotThrow()
	{
		// Act & Assert
		await Should.NotThrowAsync(() => _provider.DisposeAsync().AsTask()).ConfigureAwait(true);
	}

	[Fact]
	public void RetryPolicyIsConfigured()
	{
		// Assert
		_ = _provider.RetryPolicy.ShouldNotBeNull();
	}

	[Fact]
	public void SqlServerSpecificPropertiesAreConfiguredCorrectly()
	{
		// Assert
		_optionsValue.EnableMars.ShouldBeTrue();
		_optionsValue.ApplicationName.ShouldBe("TestApp");
		_optionsValue.CommandTimeout.ShouldBe(30);
		_optionsValue.MinPoolSize.ShouldBe(2);
		_optionsValue.MaxPoolSize.ShouldBe(20);
		_optionsValue.EnablePooling.ShouldBeTrue();
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_provider?.Dispose();
		}

		base.Dispose(disposing);
	}
}
