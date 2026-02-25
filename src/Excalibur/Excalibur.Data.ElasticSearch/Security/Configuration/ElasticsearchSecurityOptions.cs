// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Comprehensive security configuration for Elasticsearch integration including authentication, encryption, network security, and
/// compliance features.
/// </summary>
public sealed class ElasticsearchSecurityOptions
{
	/// <summary>
	/// Gets a value indicating whether comprehensive security features are enabled.
	/// </summary>
	/// <value> True to enable enterprise security features, false for basic security only. Defaults to true. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the security mode determining the level of security enforcement.
	/// </summary>
	/// <value> The security enforcement level. Defaults to <see cref="SecurityMode.Strict" />. </value>
	public SecurityMode Mode { get; init; } = SecurityMode.Strict;

	/// <summary>
	/// Gets the authentication configuration.
	/// </summary>
	/// <value> Settings for managing authentication methods and credential security. </value>
	public AuthenticationOptions Authentication { get; init; } = new();

	/// <summary>
	/// Gets the encryption and data protection configuration.
	/// </summary>
	/// <value> Settings for encryption, data protection, and key management. </value>
	public EncryptionOptions Encryption { get; init; } = new();

	/// <summary>
	/// Gets the network security configuration.
	/// </summary>
	/// <value> Settings for network-level security controls and access restrictions. </value>
	public NetworkSecurityOptions NetworkSecurity { get; init; } = new();

	/// <summary>
	/// Gets the audit and compliance configuration.
	/// </summary>
	/// <value> Settings for security auditing, logging, and compliance reporting. </value>
	public AuditOptions Audit { get; init; } = new();

	/// <summary>
	/// Gets the security monitoring and alerting configuration.
	/// </summary>
	/// <value> Settings for security event monitoring, threat detection, and alerting. </value>
	public SecurityMonitoringOptions Monitoring { get; init; } = new();

	/// <summary>
	/// Gets the transport security configuration.
	/// </summary>
	/// <value> Settings for SSL/TLS transport encryption and certificate management. </value>
	public TransportSecurityOptions Transport { get; init; } = new();
}
