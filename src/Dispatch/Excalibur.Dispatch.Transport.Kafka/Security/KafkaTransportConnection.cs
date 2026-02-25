// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Confluent.Kafka;


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka transport connection with TLS security verification.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps Kafka producer/consumer configuration and provides TLS verification
/// at connection time. The verification ensures that the security protocol is configured
/// to use SSL or SASL_SSL.
/// </para>
/// <para>
/// Usage example:
/// <code>
/// var producerConfig = new ProducerConfig
/// {
///     BootstrapServers = "kafka.example.com:9093",
///     SecurityProtocol = SecurityProtocol.Ssl,
///     SslCaLocation = "/path/to/ca.crt"
/// };
///
/// var securityOptions = new TransportSecurityOptions { RequireTls = true };
/// await using var transport = new KafkaTransportConnection(producerConfig, securityOptions);
/// await transport.ConnectAsync(cancellationToken);
///
/// // Connection is now verified as TLS-secured
/// var producer = transport.CreateProducer&lt;string, byte[]&gt;();
/// </code>
/// </para>
/// </remarks>
public class KafkaTransportConnection : TransportConnectionBase
{
	private readonly ProducerConfig _producerConfig;
	private bool _connected;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaTransportConnection"/> class.
	/// </summary>
	/// <param name="producerConfig">The Kafka producer configuration.</param>
	/// <param name="securityOptions">Optional security options. Defaults to requiring TLS.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="producerConfig"/> is null.</exception>
	public KafkaTransportConnection(
		ProducerConfig producerConfig,
		TransportSecurityOptions? securityOptions = null)
		: base(securityOptions)
	{
		ArgumentNullException.ThrowIfNull(producerConfig);
		_producerConfig = producerConfig;
	}

	/// <summary>
	/// Gets the underlying Kafka producer configuration.
	/// </summary>
	/// <value>The producer configuration used for this connection.</value>
	public ProducerConfig ProducerConfig => _producerConfig;

	/// <summary>
	/// Gets a value indicating whether the connection has been established.
	/// </summary>
	/// <value>True if connected; otherwise, false.</value>
	public bool IsConnected => _connected;

	/// <summary>
	/// Creates a new Kafka producer with the configured settings.
	/// </summary>
	/// <typeparam name="TKey">The type of the message key.</typeparam>
	/// <typeparam name="TValue">The type of the message value.</typeparam>
	/// <returns>A new Kafka producer.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the connection has not been established or TLS has not been verified.
	/// </exception>
	public IProducer<TKey, TValue> CreateProducer<TKey, TValue>()
	{
		if (!_connected)
		{
			throw new InvalidOperationException(
				"Cannot create producer: Connection has not been established. Call ConnectAsync first.");
		}

		if (SecurityOptions.RequireTls && !TlsVerified)
		{
			throw new InvalidOperationException(
				"Cannot create producer: TLS verification has not been completed. Call ConnectAsync first.");
		}

		return new ProducerBuilder<TKey, TValue>(_producerConfig).Build();
	}

	/// <summary>
	/// Creates a Kafka consumer configuration derived from the producer configuration.
	/// </summary>
	/// <param name="groupId">The consumer group ID.</param>
	/// <returns>A new consumer configuration with security settings inherited from the producer config.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the connection has not been established or TLS has not been verified.
	/// </exception>
	public ConsumerConfig CreateConsumerConfig(string groupId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

		if (!_connected)
		{
			throw new InvalidOperationException(
				"Cannot create consumer config: Connection has not been established. Call ConnectAsync first.");
		}

		if (SecurityOptions.RequireTls && !TlsVerified)
		{
			throw new InvalidOperationException(
				"Cannot create consumer config: TLS verification has not been completed. Call ConnectAsync first.");
		}

		return new ConsumerConfig
		{
			BootstrapServers = _producerConfig.BootstrapServers,
			GroupId = groupId,
			SecurityProtocol = _producerConfig.SecurityProtocol,
			SslCaLocation = _producerConfig.SslCaLocation,
			SslCertificateLocation = _producerConfig.SslCertificateLocation,
			SslKeyLocation = _producerConfig.SslKeyLocation,
			SslKeyPassword = _producerConfig.SslKeyPassword,
			SaslMechanism = _producerConfig.SaslMechanism,
			SaslUsername = _producerConfig.SaslUsername,
			SaslPassword = _producerConfig.SaslPassword,
		};
	}

	/// <inheritdoc/>
	protected override Task EstablishConnectionAsync(CancellationToken cancellationToken)
	{
		// Kafka doesn't have a traditional "connect" - the producer/consumer
		// lazily establishes connections when needed. We mark as connected
		// to indicate configuration validation is complete.
		_connected = true;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// For Kafka, this verifies that the security protocol is configured to use TLS:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="SecurityProtocol.Ssl"/> - TLS without SASL authentication</description></item>
	/// <item><description><see cref="SecurityProtocol.SaslSsl"/> - TLS with SASL authentication</description></item>
	/// </list>
	/// <para>
	/// Note: Unlike RabbitMQ, Kafka TLS verification is configuration-based because:
	/// </para>
	/// <list type="number">
	/// <item><description>Kafka connections are lazily established</description></item>
	/// <item><description>The client library enforces the security protocol at connection time</description></item>
	/// <item><description>There's no public API to check actual connection security state</description></item>
	/// </list>
	/// </remarks>
	protected override bool IsConnectionSecure()
	{
		if (!_connected)
		{
			return false;
		}

		// Check that the security protocol is configured for TLS
		// Kafka enforces this at the wire protocol level
		return _producerConfig.SecurityProtocol is SecurityProtocol.Ssl or SecurityProtocol.SaslSsl;
	}

	/// <inheritdoc/>
	protected override async ValueTask DisposeAsyncCore()
	{
		_connected = false;
		await base.DisposeAsyncCore().ConfigureAwait(false);
	}
}
