// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Compliance;

namespace Excalibur.Security;

/// <summary>
/// Configuration options for message encryption.
/// </summary>
public sealed class EncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether encryption is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if encryption is enabled; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the current key identifier.
	/// </summary>
	/// <value>
	/// The current key identifier, or <see langword="null"/> if no key is configured.
	/// </value>
	public string? CurrentKeyId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include metadata headers in encrypted content.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to include metadata headers in encrypted content; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool IncludeMetadataHeader { get; set; } = true;

	/// <summary>
	/// Gets or sets the default encryption algorithm.
	/// </summary>
	/// <value>
	/// The default encryption algorithm. The default is <see cref="EncryptionAlgorithm.Aes256Gcm"/>.
	/// </value>
	public EncryptionAlgorithm DefaultAlgorithm { get; set; } = EncryptionAlgorithm.Aes256Gcm;

	/// <summary>
	/// Gets or sets a value indicating whether to enable compression by default.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable compression by default; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
	/// </value>
	public bool EnableCompressionByDefault { get; set; }

	/// <summary>
	/// Gets or sets the key rotation interval in days.
	/// </summary>
	/// <value>
	/// The key rotation interval in days. The default is 90 days.
	/// </value>
	[Range(1, int.MaxValue)]
	public int KeyRotationIntervalDays { get; set; } = 90;

	/// <summary>
	/// Gets or sets the Azure Key Vault URL naming where encryption keys are held.
	/// </summary>
	/// <value>
	/// <see langword="null"/> - the only accepted value. Any other value is refused at startup.
	/// </value>
	/// <remarks>
	/// Message encryption protects its keys with the host's Data Protection key ring, and this package
	/// does not attach an external key provider to it. Naming a vault here would therefore leave keys
	/// protected locally while the configuration read as though a managed vault held them, so a non-null
	/// value fails validation rather than downgrading silently. Configure the key ring on the host
	/// instead, before adding message encryption.
	/// </remarks>
	public Uri? AzureKeyVaultUrl { get; set; }

	/// <summary>
	/// Gets or sets the AWS KMS key ARN naming where encryption keys are held.
	/// </summary>
	/// <value>
	/// <see langword="null"/> - the only accepted value. Any other value is refused at startup.
	/// </value>
	/// <remarks>
	/// Message encryption protects its keys with the host's Data Protection key ring, and this package
	/// does not attach an external key provider to it. Naming a KMS key here would therefore leave keys
	/// protected locally while the configuration read as though KMS held them, so a non-empty value
	/// fails validation rather than downgrading silently. Configure the key ring on the host instead,
	/// before adding message encryption.
	/// </remarks>
	public string? AwsKmsKeyArn { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to encrypt messages by default.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to encrypt messages by default; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool EncryptByDefault { get; set; } = true;

	/// <summary>
	/// Gets or initializes message types that should not be encrypted.
	/// </summary>
	/// <value>
	/// A set of message type names that should not be encrypted, or <see langword="null"/> if all messages should be encrypted.
	/// </value>
	public ISet<string>? ExcludedMessageTypes { get; init; }
}
