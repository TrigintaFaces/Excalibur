// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// The single place the RabbitMQ transport decides whether a configured connection carries TLS.
/// </summary>
/// <remarks>
/// <para>
/// Every AMQP client this package builds -- the connection, every channel, the senders, the receivers,
/// the subscribers, the dead-letter queue manager and both health checks -- is reached through one
/// <see cref="IConnectionFactory"/> registration, and that registration passes its factory through
/// <see cref="Apply"/> before returning it. That is what makes the posture reachable rather than merely
/// implemented: no client can be constructed without the factory, and the factory cannot leave its
/// registration without this check having run.
/// </para>
/// <para>
/// TLS has two spellings here -- the <c>amqps</c> scheme on a connection string, and
/// <see cref="RabbitMQConnectionOptions.UseSsl"/> with its certificate settings. Rather than re-derive
/// the rule from the options that fed the factory, the posture reads the driver's own resolved
/// <see cref="SslOption.Enabled"/>. That is the value the wire is actually built from, so it is
/// exhaustive over both spellings and over any that may be added later: a spelling that does not reach
/// <see cref="SslOption.Enabled"/> does not encrypt anything, whatever it was called.
/// </para>
/// </remarks>
internal static class RabbitMqSecurityPosture
{
	/// <summary>
	/// The transport this posture speaks for, stamped onto every refusal it raises so a host wiring more
	/// than one transport can tell which one refused.
	/// </summary>
	internal const string TransportLabel = "RabbitMQ";

	/// <summary>
	/// Gets a value indicating whether the supplied TLS settings encrypt the wire.
	/// </summary>
	/// <param name="ssl">The resolved TLS settings, or <see langword="null"/> when none are present.</param>
	/// <returns><see langword="true"/> when the connection is encrypted; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// Absent settings are plaintext at the wire and are treated as such. "Not configured" is never
	/// given the benefit of the doubt.
	/// </remarks>
	internal static bool CarriesTls(SslOption? ssl) => ssl?.Enabled == true;

	/// <summary>
	/// Enforces the configured TLS posture on a connection factory before any client is built from it.
	/// </summary>
	/// <param name="factory">The factory about to be handed to the container.</param>
	/// <param name="requireTls">Whether an unencrypted connection is to be refused.</param>
	/// <returns>The same factory, once the posture has been satisfied.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
	/// <exception cref="TransportSecurityException">
	/// Thrown when TLS is required and the resolved factory would connect in the clear.
	/// </exception>
	internal static ConnectionFactory Apply(ConnectionFactory factory, bool requireTls)
	{
		ArgumentNullException.ThrowIfNull(factory);

		if (requireTls && !CarriesTls(factory.Ssl))
		{
			throw Refuse();
		}

		return factory;
	}

	/// <summary>
	/// Builds the refusal raised when TLS is required and the configured connection cannot carry it.
	/// </summary>
	/// <returns>The exception to throw.</returns>
	/// <remarks>
	/// Shared so that every refusal in this package -- whichever client is being built -- says the same
	/// thing about the same condition.
	/// </remarks>
	internal static TransportSecurityException Refuse() =>
		new("Cannot establish the RabbitMQ connection: TLS is required but the configured connection is "
			+ "unencrypted, which carries credentials and message payloads in the clear. Use an 'amqps://' "
			+ "connection string, or call UseSsl() on the transport builder, or set "
			+ "RabbitMQSslOptions.RequireTls to false to accept an unencrypted broker connection.")
		{
			TransportName = TransportLabel,
			FailureReason = TransportSecurityFailureReason.TlsNotEnabled,
		};
}
