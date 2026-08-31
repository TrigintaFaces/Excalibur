// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// The single place the Pulsar transport decides whether a configured broker URL is acceptable.
/// </summary>
/// <remarks>
/// Pulsar addresses a self-hosted broker and both of its wire schemes have a plaintext spelling
/// (<c>pulsar://</c>, <c>http://</c>) and an encrypted one (<c>pulsar+ssl://</c>, <c>https://</c>). The
/// scheme is therefore the whole posture: there is no separate "use TLS" switch to disagree with it.
/// </remarks>
internal static class PulsarSecurityPosture
{
	/// <summary>
	/// The transport this posture speaks for, stamped onto every refusal it raises.
	/// </summary>
	internal const string TransportLabel = "Pulsar";

	/// <summary>
	/// Gets a value indicating whether a service URL names an encrypted broker endpoint.
	/// </summary>
	/// <param name="serviceUrl">The configured service URL.</param>
	/// <returns><see langword="true"/> when the scheme encrypts the wire; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// A URL that does not parse as an absolute URI is not secure. "Cannot tell" is never given the benefit
	/// of the doubt for a security control.
	/// </remarks>
	internal static bool IsSecure(string? serviceUrl) =>
		Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri)
		&& (uri.Scheme.Equals("pulsar+ssl", StringComparison.OrdinalIgnoreCase)
			|| uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Refuses a plaintext broker URL while the secure-by-default posture is in force.
	/// </summary>
	/// <param name="options">The resolved Pulsar options.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
	/// <exception cref="TransportSecurityException">
	/// Thrown when <see cref="PulsarOptions.RequireTls"/> is set and
	/// <see cref="PulsarOptions.ServiceUrl"/> does not name an encrypted endpoint.
	/// </exception>
	internal static void RequireSecureServiceUrl(PulsarOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!options.RequireTls || IsSecure(options.ServiceUrl))
		{
			return;
		}

		throw new TransportSecurityException(
			$"Cannot create the Pulsar client: TLS is required but the service URL is '{options.ServiceUrl}', "
			+ "which is a plaintext scheme, so the authentication token and every payload would cross the wire "
			+ "in the clear. Set PulsarOptions.ServiceUrl to a pulsar+ssl:// (or https://) URL, or set "
			+ "PulsarOptions.RequireTls to false to accept an unencrypted broker connection.")
		{
			TransportName = TransportLabel,
			FailureReason = TransportSecurityFailureReason.TlsNotEnabled,
		};
	}
}
