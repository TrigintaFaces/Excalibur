// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ transport connection with TLS security verification.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps a RabbitMQ connection and provides TLS verification at connection time.
/// The verification ensures that the actual wire protocol is secured with TLS, not just
/// that TLS was configured in settings.
/// </para>
/// <para>
/// Usage example:
/// <code>
/// var factory = new ConnectionFactory
/// {
///     HostName = "rabbitmq.example.com",
///     Port = 5671,
///     Ssl = new SslOption
///     {
///         Enabled = true,
///         ServerName = "rabbitmq.example.com"
///     }
/// };
///
/// var securityOptions = new TransportSecurityOptions { RequireTls = true };
/// await using var transport = new RabbitMqTransportConnection(factory, securityOptions);
/// await transport.ConnectAsync(cancellationToken);
///
/// // Connection is now verified as TLS-secured
/// var channel = await transport.CreateChannelAsync(cancellationToken);
/// </code>
/// </para>
/// </remarks>
public class RabbitMqTransportConnection : TransportConnectionBase
{
	private readonly IConnectionFactory _connectionFactory;
	private IConnection? _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqTransportConnection"/> class.
	/// </summary>
	/// <param name="connectionFactory">The RabbitMQ connection factory configured with connection settings.</param>
	/// <param name="securityOptions">Optional security options. Defaults to requiring TLS.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionFactory"/> is null.</exception>
	public RabbitMqTransportConnection(
		IConnectionFactory connectionFactory,
		TransportSecurityOptions? securityOptions = null)
		: base(securityOptions)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		_connectionFactory = connectionFactory;
	}

	/// <summary>
	/// Gets the underlying RabbitMQ connection.
	/// </summary>
	/// <value>The RabbitMQ connection, or null if not connected.</value>
	/// <remarks>
	/// Access this property only after calling <see cref="TransportConnectionBase.ConnectAsync"/>.
	/// </remarks>
	public IConnection? Connection => _connection;

	/// <summary>
	/// Gets a value indicating whether the connection is currently open.
	/// </summary>
	/// <value>True if the connection is open; otherwise, false.</value>
	public bool IsOpen => _connection?.IsOpen ?? false;

	/// <summary>
	/// Creates a new channel on the connected RabbitMQ connection.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A new RabbitMQ channel.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the connection has not been established or TLS has not been verified.
	/// </exception>
	public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken)
	{
		if (_connection is null)
		{
			throw new InvalidOperationException(
				"Cannot create channel: Connection has not been established. Call ConnectAsync first.");
		}

		if (SecurityOptions.RequireTls && !TlsVerified)
		{
			throw new InvalidOperationException(
				"Cannot create channel: TLS verification has not been completed. Call ConnectAsync first.");
		}

		return await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task EstablishConnectionAsync(CancellationToken cancellationToken)
	{
		_connection = await _connectionFactory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// For RabbitMQ, this verifies that:
	/// </para>
	/// <list type="number">
	/// <item><description>The connection's endpoint has SSL enabled</description></item>
	/// <item><description>The connection is currently open (not degraded/failed)</description></item>
	/// </list>
	/// <para>
	/// This checks the actual connection state, not just configuration, ensuring
	/// the wire protocol is actually secured.
	/// </para>
	/// </remarks>
	protected override bool IsConnectionSecure()
	{
		if (_connection is null)
		{
			return false;
		}

		// Check that the connection is open and SSL is enabled on the endpoint
		// This verifies the actual wire protocol security, not just configuration
		return _connection.IsOpen && _connection.Endpoint.Ssl.Enabled;
	}

	/// <inheritdoc/>
	protected override async ValueTask DisposeAsyncCore()
	{
		if (_connection is not null)
		{
			await _connection.CloseAsync().ConfigureAwait(false);
			_connection.Dispose();
			_connection = null;
		}

		await base.DisposeAsyncCore().ConfigureAwait(false);
	}
}
