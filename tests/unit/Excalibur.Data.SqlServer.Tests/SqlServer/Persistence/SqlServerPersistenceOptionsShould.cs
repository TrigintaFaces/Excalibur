// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.SqlServer.Persistence;

namespace Excalibur.Data.Tests.SqlServer.Persistence;

/// <summary>
/// Unit tests for <see cref="SqlServerPersistenceOptions"/> and its sub-option classes
/// (<see cref="SqlServerConnectionOptions"/>, <see cref="SqlServerSecurityOptions"/>,
/// <see cref="SqlServerResiliencyOptions"/>, <see cref="SqlServerPoolingOptions"/>,
/// <see cref="SqlServerObservabilityOptions"/>).
/// Validates default values, setters, ISP property count gates, and sub-object initialization.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
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

		// Assert — root properties
		options.ConnectionString.ShouldBe(string.Empty);
		options.CommandTimeout.ShouldBe(30);
		options.ProviderSpecificOptions.ShouldNotBeNull();
		options.ProviderSpecificOptions.ShouldBeEmpty();

		// Assert — delegated properties via sub-objects
		options.Connection.ConnectionTimeout.ShouldBe(30);
		options.Resiliency.MaxRetryAttempts.ShouldBe(3);
		options.Resiliency.RetryDelayMilliseconds.ShouldBe(1000);
		options.Pooling.EnableConnectionPooling.ShouldBeTrue();
		options.Pooling.MaxPoolSize.ShouldBe(100);
		options.Pooling.MinPoolSize.ShouldBe(10);
		options.Observability.EnableDetailedLogging.ShouldBeFalse();
		options.Observability.EnableMetrics.ShouldBeTrue();
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

	[Fact]
	public void HaveNonNullPoolingSubObject()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();

		// Assert
		options.Pooling.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullObservabilitySubObject()
	{
		// Arrange & Act
		var options = new SqlServerPersistenceOptions();

		// Assert
		options.Observability.ShouldNotBeNull();
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
		options.Connection.ConnectionTimeout = 60;

		// Assert
		options.Connection.ConnectionTimeout.ShouldBe(60);
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
		options.Resiliency.MaxRetryAttempts = 5;

		// Assert
		options.Resiliency.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingRetryDelayMilliseconds()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.Resiliency.RetryDelayMilliseconds = 2000;

		// Assert
		options.Resiliency.RetryDelayMilliseconds.ShouldBe(2000);
	}

	[Fact]
	public void AllowDisablingConnectionPooling()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.Pooling.EnableConnectionPooling = false;

		// Assert
		options.Pooling.EnableConnectionPooling.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingPoolSizes()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.Pooling.MaxPoolSize = 200;
		options.Pooling.MinPoolSize = 20;

		// Assert
		options.Pooling.MaxPoolSize.ShouldBe(200);
		options.Pooling.MinPoolSize.ShouldBe(20);
	}

	[Fact]
	public void AllowEnablingDetailedLogging()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.Observability.EnableDetailedLogging = true;

		// Assert
		options.Observability.EnableDetailedLogging.ShouldBeTrue();
	}

	[Fact]
	public void AllowDisablingMetrics()
	{
		// Arrange
		var options = new SqlServerPersistenceOptions();

		// Act
		options.Observability.EnableMetrics = false;

		// Assert
		options.Observability.EnableMetrics.ShouldBeFalse();
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
		// After ISP refactoring, the root has 3 settable properties:
		// ConnectionString, CommandTimeout, ProviderSpecificOptions.
		// Plus 5 read-only sub-object navigation properties:
		// Connection, Security, Resiliency, Pooling, Observability.
		// Explicit interface implementations (ConnectionTimeout via IPersistenceOptions,
		// MaxRetryAttempts, RetryDelayMilliseconds, EnableConnectionPooling, MaxPoolSize,
		// MinPoolSize, EnableDetailedLogging, EnableMetrics) are NOT public instance properties.

		// Arrange
		var settableProperties = typeof(SqlServerPersistenceOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanWrite)
			.ToList();

		var readOnlySubObjects = typeof(SqlServerPersistenceOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => !p.CanWrite && p.PropertyType.Name.StartsWith("SqlServer", StringComparison.Ordinal))
			.ToList();

		// Assert — 3 settable properties (ConnectionString, CommandTimeout, ProviderSpecificOptions)
		settableProperties.Count.ShouldBe(3);
		// 5 read-only sub-option navigation properties (Connection, Security, Resiliency, Pooling, Observability)
		readOnlySubObjects.Count.ShouldBe(5);
	}

	[Fact]
	public void HaveConnectionOptionsPropertyCountWithinIspGate()
	{
		// Arrange
		var properties = typeof(SqlServerConnectionOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.ToList();

		// Assert — 11 properties (ConnectionTimeout + 10 original connection properties)
		properties.Count.ShouldBeLessThanOrEqualTo(12);
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

		// Assert — 6 properties (MaxRetryAttempts, RetryDelayMilliseconds + 4 original)
		properties.Count.ShouldBeLessThanOrEqualTo(10);
	}

	[Fact]
	public void HavePoolingOptionsPropertyCountWithinIspGate()
	{
		// Arrange
		var properties = typeof(SqlServerPoolingOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.ToList();

		// Assert — 3 properties (EnableConnectionPooling, MaxPoolSize, MinPoolSize)
		properties.Count.ShouldBeLessThanOrEqualTo(10);
	}

	[Fact]
	public void HaveObservabilityOptionsPropertyCountWithinIspGate()
	{
		// Arrange
		var properties = typeof(SqlServerObservabilityOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.ToList();

		// Assert — 2 properties (EnableDetailedLogging, EnableMetrics)
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
		connection.ConnectionTimeout.ShouldBe(30);
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
		resiliency.MaxRetryAttempts.ShouldBe(3);
		resiliency.RetryDelayMilliseconds.ShouldBe(1000);
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
		resiliency.MaxRetryAttempts = 5;
		resiliency.RetryDelayMilliseconds = 2000;
		resiliency.EnableConnectionResiliency = false;
		resiliency.ConnectRetryCount = 5;
		resiliency.ConnectRetryInterval = 30;
		resiliency.EnableStatisticsCollection = true;

		// Assert
		resiliency.MaxRetryAttempts.ShouldBe(5);
		resiliency.RetryDelayMilliseconds.ShouldBe(2000);
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
			Connection =
			{
				ConnectionTimeout = 60,
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
				MaxRetryAttempts = 5,
				RetryDelayMilliseconds = 2000,
				ConnectRetryCount = 5,
				ConnectRetryInterval = 20,
			},
			Pooling =
			{
				EnableConnectionPooling = true,
				MaxPoolSize = 200,
				MinPoolSize = 20,
			},
			Observability =
			{
				EnableDetailedLogging = true,
				EnableMetrics = false,
			},
		};

		// Assert — root properties
		options.ConnectionString.ShouldBe("Server=myserver;Database=mydb;");

		// Assert — connection sub-options
		options.Connection.ConnectionTimeout.ShouldBe(60);
		options.Connection.ApplicationName.ShouldBe("TestApp");
		options.Connection.PacketSize.ShouldBe(16384);
		options.Connection.EnableMars.ShouldBeTrue();

		// Assert — security sub-options
		options.Security.EncryptConnection.ShouldBeTrue();
		options.Security.TrustServerCertificate.ShouldBeTrue();

		// Assert — resiliency sub-options
		options.Resiliency.MaxRetryAttempts.ShouldBe(5);
		options.Resiliency.RetryDelayMilliseconds.ShouldBe(2000);
		options.Resiliency.ConnectRetryCount.ShouldBe(5);
		options.Resiliency.ConnectRetryInterval.ShouldBe(20);

		// Assert — pooling sub-options
		options.Pooling.EnableConnectionPooling.ShouldBeTrue();
		options.Pooling.MaxPoolSize.ShouldBe(200);
		options.Pooling.MinPoolSize.ShouldBe(20);

		// Assert — observability sub-options
		options.Observability.EnableDetailedLogging.ShouldBeTrue();
		options.Observability.EnableMetrics.ShouldBeFalse();
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
			Connection =
			{
				ConnectionTimeout = 0,
			},
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
			Connection =
			{
				ConnectionTimeout = -1,
			},
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
			Resiliency =
			{
				MaxRetryAttempts = -1,
			},
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
			Resiliency =
			{
				RetryDelayMilliseconds = -1,
			},
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
			Pooling =
			{
				EnableConnectionPooling = true,
				MaxPoolSize = 0,
			},
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
			Pooling =
			{
				EnableConnectionPooling = true,
				MinPoolSize = -1,
			},
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
			Pooling =
			{
				EnableConnectionPooling = true,
				MaxPoolSize = 10,
				MinPoolSize = 20,
			},
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
			Pooling =
			{
				EnableConnectionPooling = false,
				MaxPoolSize = 0,
				MinPoolSize = -1,
			},
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
