// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Options for pseudonymizing data-subject identifiers (see <see cref="IDataSubjectHasher"/>).
/// </summary>
public sealed class DataSubjectHashingOptions
{
	/// <summary>
	/// The minimum accepted <see cref="Pepper"/> length, in characters.
	/// </summary>
	public const int MinimumPepperLength = 32;

	/// <summary>
	/// Gets or sets the secret pepper (HMAC key) used to key the data-subject hash.
	/// </summary>
	/// <remarks>
	/// The pepper MUST be a high-entropy secret sourced from a secret manager / KMS and MUST NOT be
	/// co-located with the erasure / legal-hold / data-inventory store it protects. It must be stable
	/// across the whole system so identifier lookups remain consistent. A missing or too-short pepper is
	/// a fail-closed startup error — the hasher never falls back to an unkeyed hash.
	/// </remarks>
	public string? Pepper { get; set; }
}
