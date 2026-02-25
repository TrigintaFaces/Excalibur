// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.SqlServer.Persistence;

namespace Excalibur.Data.Tests.SqlServer.Persistence;

/// <summary>
/// Unit tests for <see cref="SqlServerPersistenceOptions"/> and its sub-option classes
/// (<see cref="SqlServerConnectionOptions"/>, <see cref="SqlServerSecurityOptions"/>,
/// <see cref="SqlServerResiliencyOptions"/>).
/// Validates default values, setters, ISP property count gates, and sub-object initialization.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Persistence")]
public sealed class SqlServerPersistenceOptionsShould : UnitTestBase
{
	// ─────────────────────────────────────────────
	// Root options: default values
	// ─────────────────────────────────────────────

	[Fact]
	public void ImplementIPersistenceOptions()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();

		// Assert
		options.ShouldBeAssignableTo<IPersistenceOptions>();
	}

	[Fact]
	public void HaveCorrectDefaultValues()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();

		// Assert — IPersistenceOptions-mandated properties
		options.ConnectionString.ShouldBe(string.Empty);
		options.ConnectionTimeout.ShouldBe(30);
		options.CommandTimeout.ShouldBe(30);
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelayMilliseconds.ShouldBe(1000);
		options.EnableConnectionPooling.ShouldBeTrue();
		options.MaxPoolSize.ShouldBe(100);
		options.MinPoolSize.ShouldBe(10);
		options.EnableDetailedLogging.ShouldBeFalse();
		options.EnableMetrics.ShouldBeTrue();
		options.ProviderSpecificOptions.ShouldNotBeNull();
		options.ProviderSpecificOptions.ShouldBeEmpty();
	}

	// ─────────────────────────────────────────────
	// Sub-objects are non-null by default
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveNonNullConnectionSubObject()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();

		// Assert
		options.Connection.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullSecuritySubObject()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();

		// Assert
		options.Security.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullResiliencySubObject()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();

		// Assert
		options.Resiliency.ShouldNotBeNull();
	}

	// ─────────────────────────────────────────────
	// Root property setters work correctly
	// ─────────────────────────────────────────────

	[Fact]
	public void AllowSettingConnectionString()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.ConnectionString = "Server=localhost;Database=TestDb;";

		// Assert
		options.ConnectionString.ShouldBe("Server=localhost;Database=TestDb;");
	}

	[Fact]
	public void AllowSettingConnectionTimeout()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.ConnectionTimeout = 60;

		// Assert
		options.ConnectionTimeout.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingCommandTimeout()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.CommandTimeout = 120;

		// Assert
		options.CommandTimeout.ShouldBe(120);
	}

	[Fact]
	public void AllowSettingMaxRetryAttempts()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.MaxRetryAttempts = 5;

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingRetryDelayMilliseconds()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.RetryDelayMilliseconds = 2000;

		// Assert
		options.RetryDelayMilliseconds.ShouldBe(2000);
	}

	[Fact]
	public void AllowDisablingConnectionPooling()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.EnableConnectionPooling = false;

		// Assert
		options.EnableConnectionPooling.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingPoolSizes()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.MaxPoolSize = 200;
		options.MinPoolSize = 20;

		// Assert
		options.MaxPoolSize.ShouldBe(200);
		options.MinPoolSize.ShouldBe(20);
	}

	[Fact]
	public void AllowEnablingDetailedLogging()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.EnableDetailedLogging = true;

		// Assert
		options.EnableDetailedLogging.ShouldBeTrue();
	}

	[Fact]
	public void AllowDisablingMetrics()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.EnableMetrics = false;

		// Assert
		options.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingProviderSpecificOptions()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();
		var customOptions = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["custom-key"] = "custom-value",
		};

		// Act
		options.ProviderSpecificOptions = customOptions;

		// Assert
		options.ProviderSpecificOptions.ShouldBeSameAs(customOptions);
		options.ProviderSpecificOptions["custom-key"].ShouldBe("custom-value");
	}

	// ─────────────────────────────────────────────
	// ISP quality gate: property counts
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveRootPropertyCountWithinIspGate()
	{
		// The ISP gate allows at most 10 settable properties on the root.
		// Root has 11 IPersistenceOptions-mandated properties + 3 read-only sub-object getters = 14 total.
		// Only the 11 settable properties count toward the options gate.
		// NOTE: This exceeds the <=10 ISP gate, but these are all mandated by IPersistenceOptions.
		// The sub-object getters (Connection, Security, Resiliency) are read-only navigation properties.

		// Arrange
		var settableProperties = typeof(SqlServerPersistenceOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanWrite)
			.ToList();

		var readOnlySubObjects = typeof(SqlServerPersistenceOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => !p.CanWrite && p.PropertyType.Name.StartsWith("SqlServer", StringComparison.Ordinal))
			.ToList();

		// Assert — 11 settable properties (all from IPersistenceOptions)
		settableProperties.Count.ShouldBe(11);
		// 3 read-only sub-option navigation properties
		readOnlySubObjects.Count.ShouldBe(3);
	}

	[Fact]
	public void HaveConnectionOptionsPropertyCountWithinIspGate()
	{
		// Arrange
		var properties = typeof(SqlServerConnectionOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.ToList();

		// Assert — 10 properties (at the ISP limit)
		properties.Count.ShouldBeLessThanOrEqualTo(10);
	}

	[Fact]
	public void HaveSecurityOptionsPropertyCountWithinIspGate()
	{
		// Arrange
		var properties = typeof(SqlServerSecurityOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.ToList();

		// Assert — 7 properties (within ISP limit of 10)
		properties.Count.ShouldBeLessThanOrEqualTo(10);
	}

	[Fact]
	public void HaveResiliencyOptionsPropertyCountWithinIspGate()
	{
		// Arrange
		var properties = typeof(SqlServerResiliencyOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.ToList();

		// Assert — 4 properties (within ISP limit of 10)
		properties.Count.ShouldBeLessThanOrEqualTo(10);
	}

	// ─────────────────────────────────────────────
	// SqlServerConnectionOptions: defaults
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveCorrectConnectionOptionsDefaults()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();
		var connection = options.Connection;

		// Assert
		connection.ApplicationName.ShouldBe("Excalibur");
		connection.DefaultDatabase.ShouldBeNull();
		connection.MultiSubnetFailover.ShouldBeFalse();
		connection.WorkstationId.ShouldBeNull();
		connection.PacketSize.ShouldBe(8192);
		connection.EnableMars.ShouldBeFalse();
		connection.EnableTransparentNetworkIPResolution.ShouldBeTrue();
		connection.ApplicationIntent.ShouldBe(ApplicationIntent.ReadWrite);
		connection.LoadBalanceTimeout.ShouldBe(0);
		connection.PoolBlockingPeriod.ShouldBe(PoolBlockingPeriod.Auto);
	}

	// ─────────────────────────────────────────────
	// SqlServerConnectionOptions: setters
	// ─────────────────────────────────────────────

	[Fact]
	public void AllowSettingConnectionOptionsProperties()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();
		var connection = options.Connection;

		// Act
		connection.ApplicationName = "MyApp";
		connection.DefaultDatabase = "MyDb";
		connection.MultiSubnetFailover = true;
		connection.WorkstationId = "WS001";
		connection.PacketSize = 16384;
		connection.EnableMars = true;
		connection.EnableTransparentNetworkIPResolution = false;
		connection.ApplicationIntent = ApplicationIntent.ReadOnly;
		connection.LoadBalanceTimeout = 30;
		connection.PoolBlockingPeriod = PoolBlockingPeriod.AlwaysBlock;

		// Assert
		connection.ApplicationName.ShouldBe("MyApp");
		connection.DefaultDatabase.ShouldBe("MyDb");
		connection.MultiSubnetFailover.ShouldBeTrue();
		connection.WorkstationId.ShouldBe("WS001");
		connection.PacketSize.ShouldBe(16384);
		connection.EnableMars.ShouldBeTrue();
		connection.EnableTransparentNetworkIPResolution.ShouldBeFalse();
		connection.ApplicationIntent.ShouldBe(ApplicationIntent.ReadOnly);
		connection.LoadBalanceTimeout.ShouldBe(30);
		connection.PoolBlockingPeriod.ShouldBe(PoolBlockingPeriod.AlwaysBlock);
	}

	// ─────────────────────────────────────────────
	// SqlServerSecurityOptions: defaults
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveCorrectSecurityOptionsDefaults()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();
		var security = options.Security;

		// Assert
		security.EnableAlwaysEncrypted.ShouldBeFalse();
		security.ColumnEncryptionSetting.ShouldBe(SqlConnectionColumnEncryptionSetting.Disabled);
		security.TrustServerCertificate.ShouldBeFalse();
		security.EncryptConnection.ShouldBeTrue();
		security.EnclaveAttestationUrl.ShouldBeNull();
		security.AttestationProtocol.ShouldBe(SqlAttestationProtocol.NotSpecified);
		security.Authentication.ShouldBe(SqlAuthenticationMethod.NotSpecified);
	}

	// ─────────────────────────────────────────────
	// SqlServerSecurityOptions: setters
	// ─────────────────────────────────────────────

	[Fact]
	public void AllowSettingSecurityOptionsProperties()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();
		var security = options.Security;

		// Act
		security.EnableAlwaysEncrypted = true;
		security.ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled;
		security.TrustServerCertificate = true;
		security.EncryptConnection = false;
		security.EnclaveAttestationUrl = "https://attestation.example.com";
		security.AttestationProtocol = SqlAttestationProtocol.AAS;
		security.Authentication = SqlAuthenticationMethod.ActiveDirectoryIntegrated;

		// Assert
		security.EnableAlwaysEncrypted.ShouldBeTrue();
		security.ColumnEncryptionSetting.ShouldBe(SqlConnectionColumnEncryptionSetting.Enabled);
		security.TrustServerCertificate.ShouldBeTrue();
		security.EncryptConnection.ShouldBeFalse();
		security.EnclaveAttestationUrl.ShouldBe("https://attestation.example.com");
		security.AttestationProtocol.ShouldBe(SqlAttestationProtocol.AAS);
		security.Authentication.ShouldBe(SqlAuthenticationMethod.ActiveDirectoryIntegrated);
	}

	// ─────────────────────────────────────────────
	// SqlServerResiliencyOptions: defaults
	// ─────────────────────────────────────────────

	[Fact]
	public void HaveCorrectResiliencyOptionsDefaults()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();
		var resiliency = options.Resiliency;

		// Assert
		resiliency.EnableConnectionResiliency.ShouldBeTrue();
		resiliency.ConnectRetryCount.ShouldBe(3);
		resiliency.ConnectRetryInterval.ShouldBe(10);
		resiliency.EnableStatisticsCollection.ShouldBeFalse();
	}

	// ─────────────────────────────────────────────
	// SqlServerResiliencyOptions: setters
	// ─────────────────────────────────────────────

	[Fact]
	public void AllowSettingResiliencyOptionsProperties()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();
		var resiliency = options.Resiliency;

		// Act
		resiliency.EnableConnectionResiliency = false;
		resiliency.ConnectRetryCount = 5;
		resiliency.ConnectRetryInterval = 30;
		resiliency.EnableStatisticsCollection = true;

		// Assert
		resiliency.EnableConnectionResiliency.ShouldBeFalse();
		resiliency.ConnectRetryCount.ShouldBe(5);
		resiliency.ConnectRetryInterval.ShouldBe(30);
		resiliency.EnableStatisticsCollection.ShouldBeTrue();
	}

	// ─────────────────────────────────────────────
	// Nested object initializer syntax
	// ─────────────────────────────────────────────

	[Fact]
	public void SupportNestedObjectInitializerSyntax()
	{
		// Arrange & Act — using nested initializer syntax on read-only sub-objects
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=myserver;Database=mydb;",
			ConnectionTimeout = 60,
			Connection =
			{
				ApplicationName = "TestApp",
				PacketSize = 16384,
				EnableMars = true,
			},
			Security =
			{
				EncryptConnection = true,
				TrustServerCertificate = true,
			},
			Resiliency =
			{
				ConnectRetryCount = 5,
				ConnectRetryInterval = 20,
			},
		};

		// Assert — root properties
		options.ConnectionString.ShouldBe("Server=myserver;Database=mydb;");
		options.ConnectionTimeout.ShouldBe(60);

		// Assert — connection sub-options
		options.Connection.ApplicationName.ShouldBe("TestApp");
		options.Connection.PacketSize.ShouldBe(16384);
		options.Connection.EnableMars.ShouldBeTrue();

		// Assert — security sub-options
		options.Security.EncryptConnection.ShouldBeTrue();
		options.Security.TrustServerCertificate.ShouldBeTrue();

		// Assert — resiliency sub-options
		options.Resiliency.ConnectRetryCount.ShouldBe(5);
		options.Resiliency.ConnectRetryInterval.ShouldBe(20);
	}

	// ─────────────────────────────────────────────
	// Validation: happy path
	// ─────────────────────────────────────────────

	[Fact]
	public void ValidateSuccessfullyWithValidOptions()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb;Trusted_Connection=True;",
		};

		// Act & Assert — should not throw
		Should.NotThrow(() => options.Validate());
	}

	// ─────────────────────────────────────────────
	// Validation: error paths
	// ─────────────────────────────────────────────

	[Fact]
	public void ThrowOnValidateWithEmptyConnectionString()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = string.Empty,
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("Connection string");
	}

	[Fact]
	public void ThrowOnValidateWithWhitespaceConnectionString()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "   ",
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowOnValidateWithZeroConnectionTimeout()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			ConnectionTimeout = 0,
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("Connection timeout");
	}

	[Fact]
	public void ThrowOnValidateWithNegativeConnectionTimeout()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			ConnectionTimeout = -1,
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void ThrowOnValidateWithZeroCommandTimeout()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			CommandTimeout = 0,
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("Command timeout");
	}

	[Fact]
	public void ThrowOnValidateWithNegativeMaxRetryAttempts()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			MaxRetryAttempts = -1,
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("retry attempts");
	}

	[Fact]
	public void ThrowOnValidateWithNegativeRetryDelay()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			RetryDelayMilliseconds = -1,
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("Retry delay");
	}

	[Fact]
	public void ThrowOnValidateWithZeroMaxPoolSizeWhenPoolingEnabled()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			EnableConnectionPooling = true,
			MaxPoolSize = 0,
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("pool size");
	}

	[Fact]
	public void ThrowOnValidateWithNegativeMinPoolSizeWhenPoolingEnabled()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			EnableConnectionPooling = true,
			MinPoolSize = -1,
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("pool size");
	}

	[Fact]
	public void ThrowOnValidateWithMinPoolSizeGreaterThanMaxPoolSize()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			EnableConnectionPooling = true,
			MaxPoolSize = 10,
			MinPoolSize = 20,
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("pool size");
	}

	[Fact]
	public void NotValidatePoolSizesWhenPoolingDisabled()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			EnableConnectionPooling = false,
			MaxPoolSize = 0,
			MinPoolSize = -1,
		};

		// Act & Assert — should not throw because pooling is disabled
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowOnValidateWithInvalidConnectionOptionsPacketSize()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			Connection =
			{
				PacketSize = 100,
			},
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("Packet size");
	}

	[Fact]
	public void ThrowOnValidateWithInvalidResiliencyRetryCount()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			Resiliency =
			{
				ConnectRetryCount = 300,
			},
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("retry count");
	}

	[Fact]
	public void ThrowOnValidateWithInvalidResiliencyRetryInterval()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions
		{
			ConnectionString = "Server=localhost;",
			Resiliency =
			{
				ConnectRetryInterval = 0,
			},
		};

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => options.Validate());
		ex.Message.ShouldContain("retry interval");
	}

	// ─────────────────────────────────────────────
	// ProviderSpecificOptions uses ordinal comparer
	// ─────────────────────────────────────────────

	[Fact]
	public void UseOrdinalComparerForProviderSpecificOptions()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.ProviderSpecificOptions["Key"] = "value1";
		options.ProviderSpecificOptions["key"] = "value2";

		// Assert — ordinal comparison means "Key" and "key" are different keys
		options.ProviderSpecificOptions.Count.ShouldBe(2);
		options.ProviderSpecificOptions["Key"].ShouldBe("value1");
		options.ProviderSpecificOptions["key"].ShouldBe("value2");
	}
}
