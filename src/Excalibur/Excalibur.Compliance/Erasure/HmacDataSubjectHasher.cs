// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Keyed (HMAC-SHA-256) implementation of <see cref="IDataSubjectHasher"/>.
/// </summary>
/// <remarks>
/// Uses HMAC-SHA-256 with a secret pepper (<see cref="DataSubjectHashingOptions.Pepper"/>) so the
/// pseudonymization token cannot be reversed by a rainbow-table / dictionary attack against low-entropy
/// identifiers. The pepper is validated at startup (see the options validator); this type never falls back
/// to an unkeyed hash.
/// </remarks>
internal sealed class HmacDataSubjectHasher : IDataSubjectHasher
{
	private readonly byte[] _key;

	/// <summary>
	/// Initializes a new instance of the <see cref="HmacDataSubjectHasher"/> class.
	/// </summary>
	/// <param name="options">The hashing options carrying the secret pepper.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the configured pepper is missing or shorter than
	/// <see cref="DataSubjectHashingOptions.MinimumPepperLength"/> — fail-closed, so a misconfigured
	/// deployment cannot silently pseudonymize with an unkeyed hash.
	/// </exception>
	public HmacDataSubjectHasher(IOptions<DataSubjectHashingOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var pepper = options.Value.Pepper;
		if (string.IsNullOrEmpty(pepper) || pepper.Length < DataSubjectHashingOptions.MinimumPepperLength)
		{
			throw new InvalidOperationException(
				"DataSubjectHashingOptions.Pepper is required and must be at least " +
				$"{DataSubjectHashingOptions.MinimumPepperLength} characters. Configure a high-entropy secret " +
				"from your secret manager / KMS; the data-subject hasher will not fall back to an unkeyed hash.");
		}

		_key = Encoding.UTF8.GetBytes(pepper);
	}

	/// <inheritdoc />
	public string HashDataSubjectId(string dataSubjectId)
	{
		ArgumentNullException.ThrowIfNull(dataSubjectId);

		var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(dataSubjectId));
		return Convert.ToHexString(hash);
	}
}
