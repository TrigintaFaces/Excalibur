// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance.Vault;

/// <summary>
/// Configuration options for the HashiCorp Vault key management provider.
/// </summary>
/// <remarks>
/// <para>
/// HashiCorp Vault provides enterprise-grade key management with:
/// <list type="bullet">
/// <item>Transit secrets engine for server-side encryption</item>
/// <item>Auto-unseal for automated recovery</item>
/// <item>Cross-datacenter replication</item>
/// <item>Multiple authentication methods</item>
/// </list>
/// </para>
/// </remarks>
public sealed class VaultOptions
{
	/// <summary>
	/// Gets or sets the URI of the HashiCorp Vault instance.
	/// </summary>
	/// <example>https://vault.example.com:8200</example>
	public Uri? VaultUri { get; set; }

	/// <summary>
	/// Gets or sets the mount path for the Transit secrets engine.
	/// Default is "transit".
	/// </summary>
	public string TransitMountPath { get; set; } = "transit";

	/// <summary>
	/// Gets or sets the key name prefix for keys managed by this provider.
	/// Default is "excalibur-dispatch-".
	/// </summary>
	/// <remarks>
	/// Keys are named using the pattern: {KeyNamePrefix}{keyId}
	/// </remarks>
	public string KeyNamePrefix { get; set; } = "excalibur-dispatch-";

	/// <summary>
	/// Gets or sets the namespace (enterprise feature).
	/// Leave null for open-source Vault.
	/// </summary>
	public string? Namespace { get; set; }

	/// <summary>
	/// Gets or sets the duration to cache key metadata.
	/// Default is 5 minutes.
	/// </summary>
	public TimeSpan MetadataCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the HTTP client timeout.
	/// Default is 30 seconds.
	/// </summary>
	public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets whether detailed telemetry should be enabled.
	/// Default is false.
	/// </summary>
	public bool EnableDetailedTelemetry { get; set; }

	/// <summary>
	/// Gets or sets authentication configuration for Vault.
	/// </summary>
	/// <value> The authentication sub-options. </value>
	public VaultAuthOptions Auth { get; set; } = new();

	/// <summary>
	/// Gets or sets key behavior configuration for Vault Transit engine.
	/// </summary>
	/// <value> The key behavior sub-options. </value>
	public VaultKeyOptions Keys { get; set; } = new();

	/// <summary>
	/// Gets or sets retry/resilience configuration for Vault operations.
	/// </summary>
	/// <value> The retry sub-options. </value>
	public VaultRetryOptions Retry { get; set; } = new();
}

/// <summary>
/// Authentication configuration for HashiCorp Vault.
/// </summary>
public sealed class VaultAuthOptions
{
	/// <summary>
	/// Gets or sets the authentication method to use. Default is Token.
	/// </summary>
	public VaultAuthMethod AuthMethod { get; set; } = VaultAuthMethod.Token;

	/// <summary>
	/// Gets or sets the token for Token-based authentication.
	/// </summary>
	public string? Token { get; set; }

	/// <summary>
	/// Gets or sets the AppRole role ID for AppRole authentication.
	/// </summary>
	public string? AppRoleId { get; set; }

	/// <summary>
	/// Gets or sets the AppRole secret ID for AppRole authentication.
	/// </summary>
	public string? AppRoleSecretId { get; set; }

	/// <summary>
	/// Gets or sets the mount path for AppRole authentication. Default is "approle".
	/// </summary>
	public string AppRoleMountPath { get; set; } = "approle";

	/// <summary>
	/// Gets or sets the Kubernetes service account JWT path.
	/// </summary>
	public string KubernetesJwtPath { get; set; } = "/var/run/secrets/kubernetes.io/serviceaccount/token";

	/// <summary>
	/// Gets or sets the Kubernetes auth role.
	/// </summary>
	public string? KubernetesRole { get; set; }

	/// <summary>
	/// Gets or sets the mount path for Kubernetes authentication. Default is "kubernetes".
	/// </summary>
	public string KubernetesMountPath { get; set; } = "kubernetes";
}

/// <summary>
/// Key behavior configuration for Vault Transit secrets engine.
/// </summary>
public sealed class VaultKeyOptions
{
	/// <summary>
	/// Gets or sets the default key type for new keys. Default is aes256-gcm96.
	/// </summary>
	public string DefaultKeyType { get; set; } = "aes256-gcm96";

	/// <summary>
	/// Gets or sets a value indicating whether keys should be exportable. Default is false.
	/// </summary>
	public bool AllowKeyExport { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to allow plaintext backup of keys. Default is false.
	/// </summary>
	public bool AllowPlaintextBackup { get; set; }

	/// <summary>
	/// Gets or sets the minimum decryption version. Default is 0 (all versions allowed).
	/// </summary>
	public int MinDecryptionVersion { get; set; }

	/// <summary>
	/// Gets or sets the minimum encryption version. Default is 0 (use latest version).
	/// </summary>
	public int MinEncryptionVersion { get; set; }

	/// <summary>
	/// Gets or sets whether convergent encryption should be enabled. Default is false.
	/// </summary>
	public bool EnableConvergentEncryption { get; set; }

	/// <summary>
	/// Gets or sets whether key derivation should be enabled. Default is false.
	/// </summary>
	public bool EnableKeyDerivation { get; set; }
}

/// <summary>
/// Retry/resilience configuration for Vault operations.
/// </summary>
public sealed class VaultRetryOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic retry. Default is true.
	/// </summary>
	public bool EnableRetry { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts. Default is 3.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the initial delay for exponential backoff. Default is 1 second.
	/// </summary>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Supported authentication methods for HashiCorp Vault.
/// </summary>
public enum VaultAuthMethod
{
	/// <summary>
	/// Token-based authentication.
	/// Suitable for development and simple deployments.
	/// </summary>
	Token,

	/// <summary>
	/// AppRole authentication.
	/// Suitable for machine-to-machine authentication.
	/// </summary>
	AppRole,

	/// <summary>
	/// Kubernetes authentication.
	/// Suitable for workloads running in Kubernetes.
	/// </summary>
	Kubernetes
}
