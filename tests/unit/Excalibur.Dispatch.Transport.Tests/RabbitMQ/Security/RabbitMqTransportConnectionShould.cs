// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using FakeItEasy;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Security;

/// <summary>
/// Unit tests for RabbitMqTransportConnection TLS verification.
/// </summary>
[Trait("Category", "Unit")]
public class RabbitMqTransportConnectionShould
{
	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenConnectionFactoryIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new RabbitMqTransportConnection(null!));
	}

	[Fact]
	public async Task AcceptNullSecurityOptionsAndUseDefaults()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();

		// Act
		await using var connection = new RabbitMqTransportConnection(factory, null);

		// Assert - should not throw, defaults to RequireTls = true
		_ = connection.ShouldNotBeNull();
	}

	[Fact]
	public async Task AcceptCustomSecurityOptions()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		var options = new TransportSecurityOptions { RequireTls = false };

		// Act
		await using var connection = new RabbitMqTransportConnection(factory, options);

		// Assert
		_ = connection.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region Connection State Tests

	[Fact]
	public async Task ReturnFalseForIsOpenWhenNotConnected()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		await using var transport = new RabbitMqTransportConnection(factory);

		// Act & Assert
		transport.IsOpen.ShouldBeFalse();
		transport.Connection.ShouldBeNull();
	}

	#endregion Connection State Tests

	#region ConnectAsync Tests

	[Fact]
	public async Task ConnectSuccessfullyWhenTlsIsEnabledAndRequired()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		var connection = A.Fake<IConnection>();
		var endpoint = CreateSecureEndpoint();

		_ = A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(connection));
		_ = A.CallTo(() => connection.IsOpen).Returns(true);
		_ = A.CallTo(() => connection.Endpoint).Returns(endpoint);

		var options = new TransportSecurityOptions { RequireTls = true };
		var transport = new RabbitMqTransportConnection(factory, options);

		// Act
		await transport.ConnectAsync(CancellationToken.None);

		// Assert
		transport.IsOpen.ShouldBeTrue();
		_ = transport.Connection.ShouldNotBeNull();

		await transport.DisposeAsync();
	}

	[Fact]
	public async Task ThrowTransportSecurityExceptionWhenTlsRequiredButNotSecure()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		var connection = A.Fake<IConnection>();
		var endpoint = CreateInsecureEndpoint();

		_ = A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(connection));
		_ = A.CallTo(() => connection.IsOpen).Returns(true);
		_ = A.CallTo(() => connection.Endpoint).Returns(endpoint);

		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new RabbitMqTransportConnection(factory, options);

		// Act & Assert
		var exception = await Should.ThrowAsync<TransportSecurityException>(
			() => transport.ConnectAsync(CancellationToken.None));

		exception.Message.ShouldContain("TLS");
	}

	[Fact]
	public async Task ConnectSuccessfullyWhenTlsNotRequiredAndNotSecure()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		var connection = A.Fake<IConnection>();
		var endpoint = CreateInsecureEndpoint();

		_ = A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(connection));
		_ = A.CallTo(() => connection.IsOpen).Returns(true);
		_ = A.CallTo(() => connection.Endpoint).Returns(endpoint);

		var options = new TransportSecurityOptions { RequireTls = false };
		await using var transport = new RabbitMqTransportConnection(factory, options);

		// Act
		await transport.ConnectAsync(CancellationToken.None);

		// Assert
		transport.IsOpen.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowWhenConnectionIsNotOpenAfterConnect()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		var connection = A.Fake<IConnection>();
		var endpoint = CreateSecureEndpoint();

		_ = A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(connection));
		_ = A.CallTo(() => connection.IsOpen).Returns(false); // Connection not open
		_ = A.CallTo(() => connection.Endpoint).Returns(endpoint);

		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new RabbitMqTransportConnection(factory, options);

		// Act & Assert
		// IsConnectionSecure returns false when IsOpen is false
		_ = await Should.ThrowAsync<TransportSecurityException>(
			() => transport.ConnectAsync(CancellationToken.None));
	}

	#endregion ConnectAsync Tests

	#region CreateChannelAsync Tests

	[Fact]
	public async Task ThrowWhenCreatingChannelBeforeConnect()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		await using var transport = new RabbitMqTransportConnection(factory);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => transport.CreateChannelAsync(CancellationToken.None));

		exception.Message.ShouldContain("Connection has not been established");
	}

	[Fact]
	public async Task CreateChannelSuccessfullyAfterSecureConnect()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		var connection = A.Fake<IConnection>();
		var channel = A.Fake<IChannel>();
		var endpoint = CreateSecureEndpoint();

		_ = A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(connection));
		_ = A.CallTo(() => connection.IsOpen).Returns(true);
		_ = A.CallTo(() => connection.Endpoint).Returns(endpoint);
		_ = A.CallTo(() => connection.CreateChannelAsync(A<CreateChannelOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult(channel));

		var options = new TransportSecurityOptions { RequireTls = true };
		await using var transport = new RabbitMqTransportConnection(factory, options);

		await transport.ConnectAsync(CancellationToken.None);

		// Act
		var createdChannel = await transport.CreateChannelAsync(CancellationToken.None);

		// Assert
		createdChannel.ShouldBe(channel);
	}

	#endregion CreateChannelAsync Tests

	#region Dispose Tests

	[Fact]
	public async Task DisposeConnectionAfterConnect()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		var connection = A.Fake<IConnection>();
		var endpoint = CreateSecureEndpoint();

		_ = A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(connection));
		_ = A.CallTo(() => connection.IsOpen).Returns(true);
		_ = A.CallTo(() => connection.Endpoint).Returns(endpoint);

		var options = new TransportSecurityOptions { RequireTls = false };
		var transport = new RabbitMqTransportConnection(factory, options);
		await transport.ConnectAsync(CancellationToken.None);

		// Act & Assert - dispose should not throw
		await transport.DisposeAsync();

		// After dispose, Connection should be null (internal state cleared)
		transport.Connection.ShouldBeNull();
	}

	[Fact]
	public async Task DisposeCanBeCalledMultipleTimesWithoutThrowing()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		var connection = A.Fake<IConnection>();
		var endpoint = CreateSecureEndpoint();

		_ = A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(connection));
		_ = A.CallTo(() => connection.IsOpen).Returns(true);
		_ = A.CallTo(() => connection.Endpoint).Returns(endpoint);

		var options = new TransportSecurityOptions { RequireTls = false };
		var transport = new RabbitMqTransportConnection(factory, options);
		await transport.ConnectAsync(CancellationToken.None);

		// Act - dispose multiple times should not throw
		await transport.DisposeAsync();
		await transport.DisposeAsync();

		// Assert - no exception means success
		transport.Connection.ShouldBeNull();
	}

	[Fact]
	public async Task DisposeWithoutConnectingDoesNotThrow()
	{
		// Arrange
		var factory = A.Fake<IConnectionFactory>();
		var transport = new RabbitMqTransportConnection(factory);

		// Act & Assert - dispose without connecting should not throw
		await transport.DisposeAsync();
		transport.Connection.ShouldBeNull();
	}

	#endregion Dispose Tests

	#region Helper Methods

	private static AmqpTcpEndpoint CreateSecureEndpoint()
	{
		return new AmqpTcpEndpoint("localhost", 5671, new SslOption { Enabled = true });
	}

	private static AmqpTcpEndpoint CreateInsecureEndpoint()
	{
		return new AmqpTcpEndpoint("localhost", 5672, new SslOption { Enabled = false });
	}

	#endregion Helper Methods
}
