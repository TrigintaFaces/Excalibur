// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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

	/// <summary>
	/// Builds the refusal raised when the configured connection would present the broker's default
	/// credentials.
	/// </summary>
	/// <returns>The exception to throw.</returns>
	/// <remarks>
	/// <para>
	/// Lives beside the TLS refusal deliberately, and is raised from the same place, because the two
	/// conditions are one question asked of one connection: may this be dialled? The options validator
	/// already refuses <c>guest:guest</c> inside a supplied AMQP URI, but it can only ever see a URI
	/// string -- a transport configured by host parts keeps its credentials in separate properties the
	/// validator never receives. Refusing in only one of those two places means the same credentials are
	/// rejected or accepted according to which spelling the consumer chose, which is not a security
	/// posture but an accident of configuration syntax.
	/// </para>
	/// <para>
	/// Raised where the connection is BUILT rather than at registration, which is the same seam the TLS
	/// refusal uses. A composition that never dials a broker -- a container assembled to check wiring,
	/// an annotation test -- is not made safer by refusing it, and refusing at registration would reject
	/// those while changing nothing about what reaches a broker.
	/// </para>
	/// </remarks>
	internal static TransportSecurityException RefuseDefaultCredentials() =>
		new("Cannot establish the RabbitMQ connection: it would authenticate as 'guest'/'guest', the "
			+ "broker's published default account, which grants anyone who can reach the broker the same "
			+ "access this application has. Call Credentials(username, password) on the transport builder "
			+ "with an account provisioned for this application, or supply them in an 'amqps://' "
			+ "connection string. There is deliberately no option to accept them: the same credentials "
			+ "are already refused when they arrive inside a connection string, and accepting them here "
			+ "would mean the spelling decided the posture.")
		{
			TransportName = TransportLabel,
			FailureReason = TransportSecurityFailureReason.DefaultCredentials,
		};

	/// <summary>
	/// Determines whether the supplied credentials are the broker's well-known default account.
	/// </summary>
	/// <param name="username">The configured user name.</param>
	/// <param name="password">The configured password.</param>
	/// <returns><see langword="true"/> when both are the default; otherwise <see langword="false"/>.</returns>
	internal static bool AreDefaultCredentials(string? username, string? password) =>
		string.Equals(username, "guest", StringComparison.OrdinalIgnoreCase)
		&& string.Equals(password, "guest", StringComparison.OrdinalIgnoreCase);
}
