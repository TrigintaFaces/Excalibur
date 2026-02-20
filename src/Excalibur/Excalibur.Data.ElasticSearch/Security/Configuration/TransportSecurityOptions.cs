// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures transport security including SSL/TLS settings.
/// </summary>
public sealed class TransportSecurityOptions
{
	/// <summary>
	/// Gets a value indicating whether transport security is enforced.
	/// </summary>
	/// <value> True to enforce encrypted transport, false to allow unencrypted connections. </value>
	public bool EnforceEncryption { get; init; } = true;

	/// <summary>
	/// Gets the minimum TLS version required.
	/// </summary>
	/// <value> The minimum TLS protocol version to accept. Defaults to TLS 1.2. </value>
	public string MinimumTlsVersion { get; init; } = "1.2";

	/// <summary>
	/// Gets the allowed cipher suites.
	/// </summary>
	/// <value> List of acceptable cipher suites for TLS connections. </value>
	public List<string> AllowedCipherSuites { get; init; } = [];

	/// <summary>
	/// Gets a value indicating whether to require perfect forward secrecy.
	/// </summary>
	/// <value> True to require PFS cipher suites, false to allow any allowed cipher. </value>
	public bool RequirePerfectForwardSecrecy { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to validate certificate hostnames.
	/// </summary>
	/// <value> True to validate certificate Subject Alternative Names, false to skip hostname verification. </value>
	public bool ValidateHostnames { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to pin certificate fingerprints.
	/// </summary>
	/// <value> True to use certificate pinning for additional security, false for standard validation. </value>
	public bool UseCertificatePinning { get; init; }

	/// <summary>
	/// Gets the pinned certificate fingerprints.
	/// </summary>
	/// <value> List of certificate fingerprints to pin for connections. </value>
	public List<string> PinnedCertificates { get; init; } = [];
}
