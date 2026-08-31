// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Configuration options for transport security.
/// </summary>
/// <remarks>
/// <para>
/// Transport security is enforced at connection time rather than per-message to minimize overhead.
/// </para>
/// <para>
/// Certificate material, TLS version floors, and peer-verification behavior are configured on the
/// underlying transport client rather than here, because the broker SDK owns the TLS handshake.
/// Set them through the transport's own configuration - for example the Kafka producer
/// configuration's SSL certificate, key, and CA settings, or the RabbitMQ connection factory's SSL
/// options.
/// </para>
/// </remarks>
public sealed class TransportSecurityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether TLS is required for the transport connection.
	/// </summary>
	/// <value>True to require TLS; false to allow non-TLS connections. Default is true.</value>
	/// <remarks>
	/// <para>
	/// When set to true (default), the transport will throw a <see cref="TransportSecurityException"/>
	/// at connection time if TLS cannot be verified.
	/// </para>
	/// <para>
	/// <strong>SECURITY WARNING:</strong> Setting this to false allows plaintext communication
	/// which may expose sensitive data. Only disable for development/testing environments.
	/// </para>
	/// </remarks>
	public bool RequireTls { get; set; } = true;
}
