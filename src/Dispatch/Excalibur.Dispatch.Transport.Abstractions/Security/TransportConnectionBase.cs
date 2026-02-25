// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Abstract base class for transport connections providing TLS security verification.
/// </summary>
/// <remarks>
/// <para>
/// This base class provides connection-time TLS validation
/// to ensure transport security before any messages are sent.
/// </para>
/// <para>
/// Key security features:
/// </para>
/// <list type="bullet">
/// <item><description>One-time TLS verification at connection establishment</description></item>
/// <item><description>Cached verification result for zero per-message overhead</description></item>
/// <item><description>Fail-fast behavior if TLS is not established</description></item>
/// </list>
/// <para>
/// Implementations must override <see cref="IsConnectionSecure"/> to verify
/// the actual wire protocol security state for their specific transport.
/// </para>
/// </remarks>
public abstract class TransportConnectionBase : IAsyncDisposable
{
	private bool _tlsVerified;
	private volatile bool _disposed;

	/// <summary>
	/// Gets a value indicating whether TLS verification has been performed and passed.
	/// </summary>
	/// <value>True if TLS has been verified; otherwise, false.</value>
	protected bool TlsVerified => _tlsVerified;

	/// <summary>
	/// Gets the transport options for this connection.
	/// </summary>
	protected TransportSecurityOptions SecurityOptions { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportConnectionBase"/> class.
	/// </summary>
	/// <param name="securityOptions">The security options for this transport connection.</param>
	protected TransportConnectionBase(TransportSecurityOptions? securityOptions = null)
	{
		SecurityOptions = securityOptions ?? new TransportSecurityOptions();
	}

	/// <summary>
	/// Establishes a connection to the transport and verifies TLS security.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="TransportSecurityException">
	/// Thrown when <see cref="TransportSecurityOptions.RequireTls"/> is true
	/// and <see cref="IsConnectionSecure"/> returns false.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method performs TLS verification ONCE at connection establishment.
	/// The result is cached to ensure zero per-message overhead.
	/// </para>
	/// <para>
	/// TLS validation happens at connection time because:
	/// </para>
	/// <list type="number">
	/// <item><description>Cloud transports (SQS, Service Bus, Pub/Sub) enforce TLS - nothing to validate</description></item>
	/// <item><description>RabbitMQ/Kafka: config might say TLS but connection could fail/downgrade</description></item>
	/// <item><description>Verifies actual wire protocol, not just settings</description></item>
	/// <item><description>One-time cost during connection setup (no per-message overhead)</description></item>
	/// </list>
	/// </remarks>
	public async Task ConnectAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Establish the underlying connection first
		await EstablishConnectionAsync(cancellationToken).ConfigureAwait(false);

		// Verify TLS ONCE at connection time
		if (SecurityOptions.RequireTls)
		{
			if (!IsConnectionSecure())
			{
				throw new TransportSecurityException(
					"Transport requires a TLS-secured connection but the connection is not secure. " +
					"Ensure TLS is properly configured for your transport.");
			}

			_tlsVerified = true;
		}
	}

	/// <summary>
	/// When overridden in a derived class, establishes the underlying transport connection.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// Implementations should establish the actual connection to the message broker
	/// (e.g., RabbitMQ connection, Kafka producer, cloud service client).
	/// TLS verification happens automatically after this method completes.
	/// </remarks>
	protected abstract Task EstablishConnectionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// When overridden in a derived class, verifies that the connection is secured with TLS.
	/// </summary>
	/// <returns>True if the connection is secured with TLS; otherwise, false.</returns>
	/// <remarks>
	/// <para>
	/// This method is called once at connection establishment to verify the actual
	/// wire protocol security state.
	/// </para>
	/// <para>
	/// Implementation examples:
	/// </para>
	/// <para>
	/// <strong>RabbitMQ:</strong>
	/// <code>
	/// protected override bool IsConnectionSecure()
	///     =&gt; _connection.Endpoint.Ssl.Enabled &amp;&amp; _connection.IsOpen;
	/// </code>
	/// </para>
	/// <para>
	/// <strong>Kafka:</strong>
	/// <code>
	/// protected override bool IsConnectionSecure()
	///     =&gt; _producerConfig.SecurityProtocol == SecurityProtocol.Ssl
	///        || _producerConfig.SecurityProtocol == SecurityProtocol.SaslSsl;
	/// </code>
	/// </para>
	/// <para>
	/// <strong>Cloud Transports (SQS, Service Bus, Pub/Sub):</strong>
	/// <code>
	/// protected override bool IsConnectionSecure() =&gt; true; // Always TLS
	/// </code>
	/// </para>
	/// </remarks>
	protected abstract bool IsConnectionSecure();

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		await DisposeAsyncCore().ConfigureAwait(false);

		_disposed = true;
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// When overridden in a derived class, releases transport-specific resources.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}
