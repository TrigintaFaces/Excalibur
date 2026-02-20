// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration.Validators;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for <see cref="KafkaConfigurationValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class KafkaConfigurationValidatorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void SetDefaultConfigurationName()
	{
		// Act
		var validator = new KafkaConfigurationValidator();

		// Assert
		validator.ConfigurationName.ShouldBe("Kafka:Kafka");
	}

	[Fact]
	public void SetCustomConfigurationName()
	{
		// Act
		var validator = new KafkaConfigurationValidator("CustomSection");

		// Assert
		validator.ConfigurationName.ShouldBe("Kafka:CustomSection");
	}

	[Fact]
	public void SetPriorityTo30()
	{
		// Act
		var validator = new KafkaConfigurationValidator();

		// Assert
		validator.Priority.ShouldBe(30);
	}

	#endregion

	#region Bootstrap Servers Tests

	[Fact]
	public async Task ReturnSuccess_WhenBootstrapServersIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenMultipleBootstrapServersAreValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092,localhost:9093,localhost:9094"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenBootstrapServersIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("bootstrap servers are missing"));
	}

	[Fact]
	public async Task ReturnFailure_WhenBootstrapServerLacksPort()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Kafka bootstrap server format"));
	}

	[Fact]
	public async Task ReturnFailure_WhenBootstrapServerHasInvalidPort()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:99999"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid port"));
	}

	[Fact]
	public async Task ReturnFailure_WhenBootstrapServerHasNonNumericPort()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:abc"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid port"));
	}

	#endregion

	#region Consumer Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenConsumerConfigurationIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Consumer:GroupId"] = "my-consumer-group"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenConsumerGroupIdIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Consumer:AutoOffsetReset"] = "earliest"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("group ID is missing"));
	}

	[Theory]
	[InlineData("earliest")]
	[InlineData("latest")]
	[InlineData("none")]
	[InlineData("EARLIEST")]
	public async Task ReturnSuccess_WhenAutoOffsetResetIsValid(string autoOffsetReset)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Consumer:GroupId"] = "my-group",
				["Kafka:Consumer:AutoOffsetReset"] = autoOffsetReset
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenAutoOffsetResetIsInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Consumer:GroupId"] = "my-group",
				["Kafka:Consumer:AutoOffsetReset"] = "invalid"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("not valid"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenSessionTimeoutIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Consumer:GroupId"] = "my-group",
				["Kafka:Consumer:SessionTimeoutMs"] = "10000"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenSessionTimeoutIsTooLow()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Consumer:GroupId"] = "my-group",
				["Kafka:Consumer:SessionTimeoutMs"] = "1000"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("outside the valid range"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenMaxPollRecordsIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Consumer:GroupId"] = "my-group",
				["Kafka:Consumer:MaxPollRecords"] = "500"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenMaxPollRecordsExceedsLimit()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Consumer:GroupId"] = "my-group",
				["Kafka:Consumer:MaxPollRecords"] = "20000"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("outside the valid range"));
	}

	#endregion

	#region Producer Configuration Tests

	[Theory]
	[InlineData("0")]
	[InlineData("1")]
	[InlineData("-1")]
	[InlineData("all")]
	[InlineData("ALL")]
	public async Task ReturnSuccess_WhenProducerAcksIsValid(string acks)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Producer:Acks"] = acks
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenProducerAcksIsInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Producer:Acks"] = "2"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("not valid"));
	}

	[Theory]
	[InlineData("none")]
	[InlineData("gzip")]
	[InlineData("snappy")]
	[InlineData("lz4")]
	[InlineData("zstd")]
	public async Task ReturnSuccess_WhenCompressionTypeIsValid(string compressionType)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Producer:CompressionType"] = compressionType
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenCompressionTypeIsInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Producer:CompressionType"] = "bzip2"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnSuccess_WhenBatchSizeIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Producer:BatchSize"] = "16384"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenLingerMsIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Producer:LingerMs"] = "5"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccess_WhenRetriesIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:Producer:Retries"] = "3"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion

	#region Security Protocol Tests

	[Theory]
	[InlineData("PLAINTEXT")]
	[InlineData("SSL")]
	[InlineData("SASL_PLAINTEXT")]
	[InlineData("SASL_SSL")]
	public async Task ReturnSuccess_WhenSecurityProtocolIsValid(string protocol)
	{
		// Arrange
		var configValues = new Dictionary<string, string?>
		{
			["Kafka:BootstrapServers"] = "localhost:9092",
			["Kafka:SecurityProtocol"] = protocol
		};

		// SASL protocols require additional configuration
		if (protocol.Contains("SASL", StringComparison.OrdinalIgnoreCase))
		{
			configValues["Kafka:SaslMechanism"] = "PLAIN";
			configValues["Kafka:SaslUsername"] = "user";
			configValues["Kafka:SaslPassword"] = "pass";
		}

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(configValues)
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenSecurityProtocolIsInvalid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:SecurityProtocol"] = "INVALID"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("not valid"));
	}

	#endregion

	#region SASL Configuration Tests

	[Fact]
	public async Task ReturnFailure_WhenSaslMechanismIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:SecurityProtocol"] = "SASL_PLAINTEXT"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("SASL mechanism is required"));
	}

	[Theory]
	[InlineData("PLAIN")]
	[InlineData("SCRAM-SHA-256")]
	[InlineData("SCRAM-SHA-512")]
	[InlineData("OAUTHBEARER")]
	public async Task ReturnSuccess_WhenSaslMechanismIsValid(string mechanism)
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:SecurityProtocol"] = "SASL_PLAINTEXT",
				["Kafka:SaslMechanism"] = mechanism,
				["Kafka:SaslUsername"] = "user",
				["Kafka:SaslPassword"] = "pass"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenSaslUsernameIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:SecurityProtocol"] = "SASL_PLAINTEXT",
				["Kafka:SaslMechanism"] = "PLAIN",
				["Kafka:SaslPassword"] = "pass"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("SASL username is required"));
	}

	[Fact]
	public async Task ReturnFailure_WhenSaslPasswordIsMissing()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:SecurityProtocol"] = "SASL_PLAINTEXT",
				["Kafka:SaslMechanism"] = "PLAIN",
				["Kafka:SaslUsername"] = "user"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("SASL password is required"));
	}

	#endregion

	#region SSL Configuration Tests

	[Fact]
	public async Task ReturnFailure_WhenSslCertificateProvidedWithoutKey()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:SecurityProtocol"] = "SSL",
				["Kafka:SslCertificateLocation"] = "/path/to/cert.pem"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("SSL client key is required"));
	}

	[Fact]
	public async Task ReturnFailure_WhenSslKeyProvidedWithoutCertificate()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Kafka:BootstrapServers"] = "localhost:9092",
				["Kafka:SecurityProtocol"] = "SSL",
				["Kafka:SslKeyLocation"] = "/path/to/key.pem"
			})
			.Build();

		var validator = new KafkaConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("SSL client certificate is required"));
	}

	#endregion

	#region Custom Config Section Tests

	[Fact]
	public async Task ValidateCustomConfigSection()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["CustomKafka:BootstrapServers"] = "localhost:9092"
			})
			.Build();

		var validator = new KafkaConfigurationValidator("CustomKafka");

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion
}
