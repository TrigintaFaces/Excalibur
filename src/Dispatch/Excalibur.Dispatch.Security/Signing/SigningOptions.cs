// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Configuration options for message signing.
/// </summary>
public sealed partial class SigningOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether signing is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if signing is enabled; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the default signing algorithm.
	/// </summary>
	/// <value>
	/// The default <see cref="SigningAlgorithm"/> to use for signing. The default is <see cref="SigningAlgorithm.HMACSHA256"/>.
	/// </value>
	public SigningAlgorithm DefaultAlgorithm { get; set; } = SigningAlgorithm.HMACSHA256;

	/// <summary>
	/// Gets or sets the default key identifier.
	/// </summary>
	/// <value>
	/// The default key identifier, or <see langword="null"/> if no default key is specified.
	/// </value>
	public string? DefaultKeyId { get; set; }

	/// <summary>
	/// Gets or sets the maximum age of signatures in minutes (for replay protection).
	/// </summary>
	/// <value>
	/// The maximum age of signatures in minutes. The default is 5 minutes.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxSignatureAgeMinutes { get; set; } = 5;

	/// <summary>
	/// Gets or sets a value indicating whether to include timestamps by default.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to include timestamps by default; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool IncludeTimestampByDefault { get; set; } = true;

	/// <summary>
	/// Gets or sets the key rotation interval in days.
	/// </summary>
	/// <value>
	/// The key rotation interval in days. The default is 30 days.
	/// </value>
	[Range(1, int.MaxValue)]
	public int KeyRotationIntervalDays { get; set; } = 30;

	/// <summary>
	/// Gets the per-tenant signing algorithm overrides.
	/// When a tenant ID is present in the message context and has an entry in this dictionary,
	/// the tenant-specific algorithm is used instead of <see cref="DefaultAlgorithm"/>.
	/// </summary>
	/// <value>
	/// A dictionary mapping tenant IDs to their signing algorithms. Empty by default.
	/// </value>
	public Dictionary<string, SigningAlgorithm> TenantAlgorithms { get; } = new(StringComparer.Ordinal);
}
