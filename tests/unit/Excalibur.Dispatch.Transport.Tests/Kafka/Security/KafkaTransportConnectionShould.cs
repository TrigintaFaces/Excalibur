// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Security;

/// <summary>
/// Unit tests for KafkaTransportConnection TLS verification.
/// </summary>
[Trait("Category", "Unit")]
public class KafkaTransportConnectionShould
{
	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenProducerConfigIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new KafkaTransportConnection(null!));
	}

	[Fact]
	public async Task AcceptNullSecurityOptionsAndUseDefaults()
	{
		// Arrange
		var config = CreateSslConfig();

		// Act
		await using var connection = new KafkaTransportConnection(config, null);

		// Assert - should not throw, defaults to RequireTls = true
		_ = connection.ShouldNotBeNull();
	}

	[Fact]
	public async Task AcceptCustomSecurityOptions()
	{
		// Arrange
		var config = CreatePlaintextConfig();
		var options = new TransportSecurityOptions { RequireTls = false };

		// Act
		await using var connection = new KafkaTransportConnection(config, options);

		// Assert
		_ = connection.ShouldNotBeNull();
	}

	#endregion

	#region Connection State Tests

	[Fact]
	public async Task ReturnFalseForIsConnectedWhenNotConnected()
	{
		// Arrange
		var config = CreateSslConfig();
		await using var transport = new KafkaTransportConnection(config);

		// Act & Assert
		transport.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task ExposeProducerConfig()
	{
		// Arrange
		var config = CreateSslConfig();
		await using var transport = new KafkaTransportConnection(config);

		// Act & Assert
		transport.ProducerConfig.ShouldBe(config);
		transport.ProducerConfig.SecurityProtocol.ShouldBe(SecurityProtocol.Ssl);
	}

	#endregion

	#region ConnectAsync Tests

	[Fact]
	public async Task ConnectSuccessfullyWhenSslProtocolAndTlsRequired()
	{
		// Arrange
		var config = CreateSslConfig();
		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new KafkaTransportConnection(config, options);

		// Act
		await transport.ConnectAsync(CancellationToken.None);

		// Assert
		transport.IsConnected.ShouldBeTrue();
	}

	[Fact]
	public async Task ConnectSuccessfullyWhenSaslSslProtocolAndTlsRequired()
	{
		// Arrange
		var config = CreateSaslSslConfig();
		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new KafkaTransportConnection(config, options);

		// Act
		await transport.ConnectAsync(CancellationToken.None);

		// Assert
		transport.IsConnected.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowTransportSecurityExceptionWhenPlaintextAndTlsRequired()
	{
		// Arrange
		var config = CreatePlaintextConfig();
		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new KafkaTransportConnection(config, options);

		// Act & Assert
		var exception = await Should.ThrowAsync<TransportSecurityException>(
			() => transport.ConnectAsync(CancellationToken.None));

		exception.Message.ShouldContain("TLS");
	}

	[Fact]
	public async Task ThrowTransportSecurityExceptionWhenSaslPlaintextAndTlsRequired()
	{
		// Arrange
		var config = CreateSaslPlaintextConfig();
		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new KafkaTransportConnection(config, options);

		// Act & Assert
		_ = await Should.ThrowAsync<TransportSecurityException>(
			() => transport.ConnectAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ConnectSuccessfullyWhenPlaintextAndTlsNotRequired()
	{
		// Arrange
		var config = CreatePlaintextConfig();
		var options = new TransportSecurityOptions { RequireTls = false };
		await using var transport = new KafkaTransportConnection(config, options);

		// Act
		await transport.ConnectAsync(CancellationToken.None);

		// Assert
		transport.IsConnected.ShouldBeTrue();
	}

	#endregion

	#region CreateProducer Tests

	[Fact]
	public async Task ThrowWhenCreatingProducerBeforeConnect()
	{
		// Arrange
		var config = CreateSslConfig();
		await using var transport = new KafkaTransportConnection(config);

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(
			() => transport.CreateProducer<string, byte[]>());

		exception.Message.ShouldContain("Connection has not been established");
	}

	[Fact]
	public async Task CreateProducerSuccessfullyAfterSecureConnect()
	{
		// Arrange
		var config = CreateSslConfig();
		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new KafkaTransportConnection(config, options);

		await transport.ConnectAsync(CancellationToken.None);

		// Act
		using var producer = transport.CreateProducer<string, byte[]>();

		// Assert
		_ = producer.ShouldNotBeNull();
	}

	#endregion

	#region CreateConsumerConfig Tests

	[Fact]
	public async Task ThrowWhenCreatingConsumerConfigBeforeConnect()
	{
		// Arrange
		var config = CreateSslConfig();
		await using var transport = new KafkaTransportConnection(config);

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(
			() => transport.CreateConsumerConfig("test-group"));

		exception.Message.ShouldContain("Connection has not been established");
	}

	[Fact]
	public async Task ThrowWhenCreatingConsumerConfigWithNullGroupId()
	{
		// Arrange
		var config = CreateSslConfig();
		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new KafkaTransportConnection(config, options);
		await transport.ConnectAsync(CancellationToken.None);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => transport.CreateConsumerConfig(null!));
	}

	[Fact]
	public async Task CreateConsumerConfigWithSecuritySettingsInherited()
	{
		// Arrange
		var config = CreateSslConfig();
		config.SslCaLocation = "/path/to/ca.crt";
		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new KafkaTransportConnection(config, options);
		await transport.ConnectAsync(CancellationToken.None);

		// Act
		var consumerConfig = transport.CreateConsumerConfig("test-group");

		// Assert
		_ = consumerConfig.ShouldNotBeNull();
		consumerConfig.GroupId.ShouldBe("test-group");
		consumerConfig.SecurityProtocol.ShouldBe(SecurityProtocol.Ssl);
		consumerConfig.BootstrapServers.ShouldBe(config.BootstrapServers);
		consumerConfig.SslCaLocation.ShouldBe("/path/to/ca.crt");
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public async Task DisposeConnectionAfterConnect()
	{
		// Arrange
		var config = CreateSslConfig();
		var options = new TransportSecurityOptions { RequireTls = true };
		var transport = new KafkaTransportConnection(config, options);
		await transport.ConnectAsync(CancellationToken.None);

		// Act
		await transport.DisposeAsync();

		// Assert - IsConnected should be false after dispose
		transport.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeCanBeCalledMultipleTimesWithoutThrowing()
	{
		// Arrange
		var config = CreateSslConfig();
		var options = new TransportSecurityOptions { RequireTls = true };
		var transport = new KafkaTransportConnection(config, options);
		await transport.ConnectAsync(CancellationToken.None);

		// Act - dispose multiple times should not throw
		await transport.DisposeAsync();
		await transport.DisposeAsync();

		// Assert
		transport.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeWithoutConnectingDoesNotThrow()
	{
		// Arrange
		var config = CreateSslConfig();
		var transport = new KafkaTransportConnection(config);

		// Act & Assert - dispose without connecting should not throw
		await transport.DisposeAsync();
		transport.IsConnected.ShouldBeFalse();
	}

	#endregion

	#region Helper Methods

	private static ProducerConfig CreateSslConfig()
	{
		return new ProducerConfig
		{
			BootstrapServers = "localhost:9093",
			SecurityProtocol = SecurityProtocol.Ssl
		};
	}

	private static ProducerConfig CreateSaslSslConfig()
	{
		return new ProducerConfig
		{
			BootstrapServers = "localhost:9093",
			SecurityProtocol = SecurityProtocol.SaslSsl,
			SaslMechanism = SaslMechanism.Plain,
			SaslUsername = "user",
			SaslPassword = "password"
		};
	}

	private static ProducerConfig CreatePlaintextConfig()
	{
		return new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			SecurityProtocol = SecurityProtocol.Plaintext
		};
	}

	private static ProducerConfig CreateSaslPlaintextConfig()
	{
		return new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			SecurityProtocol = SecurityProtocol.SaslPlaintext,
			SaslMechanism = SaslMechanism.Plain
		};
	}

	#endregion
}
