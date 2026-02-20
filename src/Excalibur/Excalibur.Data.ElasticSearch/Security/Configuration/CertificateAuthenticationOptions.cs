// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures certificate-based mutual TLS authentication.
/// </summary>
public sealed class CertificateAuthenticationOptions
{
	/// <summary>
	/// Gets a value indicating whether certificate authentication is enabled.
	/// </summary>
	/// <value> True to enable mutual TLS certificate authentication, false otherwise. </value>
	public bool Enabled { get; init; }

	/// <summary>
	/// Gets the client certificate store location.
	/// </summary>
	/// <value> The certificate store location for client certificates. </value>
	public string? CertificateStore { get; init; }

	/// <summary>
	/// Gets the client certificate subject name or thumbprint.
	/// </summary>
	/// <value> The certificate identifier for locating the client certificate. </value>
	public string? CertificateIdentifier { get; init; }

	/// <summary>
	/// Gets the path to the client certificate file (PFX/P12).
	/// </summary>
	/// <value> The file path to the client certificate, or null if using certificate store. </value>
	public string? CertificateFilePath { get; init; }

	/// <summary>
	/// Gets a value indicating whether to validate the server certificate chain.
	/// </summary>
	/// <value> True to perform full certificate chain validation, false to allow self-signed certificates. </value>
	public bool ValidateCertificateChain { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to validate certificate revocation status.
	/// </summary>
	/// <value> True to check certificate revocation lists (CRL), false to skip revocation checks. </value>
	public bool CheckCertificateRevocation { get; init; } = true;
}
