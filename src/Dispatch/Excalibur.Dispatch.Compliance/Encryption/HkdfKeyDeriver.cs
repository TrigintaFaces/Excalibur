// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Encryption;

/// <summary>
/// Derives child keys from a master key using HKDF (RFC 5869).
/// </summary>
/// <remarks>
/// <para>
/// HKDF is used for deriving multiple cryptographically strong keys from a single master key.
/// Unlike PBKDF2 (which derives keys from passwords), HKDF is designed for already-strong
/// key material and is suitable for:
/// </para>
/// <list type="bullet">
/// <item><description>Deriving per-message encryption keys</description></item>
/// <item><description>Deriving per-tenant keys from a master key</description></item>
/// <item><description>Creating key hierarchies (master to purpose-specific subkeys)</description></item>
/// </list>
/// </remarks>
public sealed class HkdfKeyDeriver
{
	private readonly HkdfKeyDerivationOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="HkdfKeyDeriver"/> class.
	/// </summary>
	/// <param name="options">The HKDF key derivation options.</param>
	public HkdfKeyDeriver(IOptions<HkdfKeyDerivationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <summary>
	/// Derives a key for a specific purpose and context.
	/// </summary>
	/// <param name="masterKey">The master key material to derive from.</param>
	/// <param name="purpose">The purpose identifier for key separation (e.g., "field-encryption", "tenant-key").</param>
	/// <param name="context">Additional context bytes for key derivation (e.g., tenant ID, message ID).</param>
	/// <returns>The derived key bytes.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="masterKey"/> or <paramref name="purpose"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="masterKey"/> is empty or <paramref name="purpose"/> is whitespace.</exception>
	public byte[] DeriveKey(byte[] masterKey, string purpose, ReadOnlySpan<byte> context)
	{
		ArgumentNullException.ThrowIfNull(masterKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

		if (masterKey.Length == 0)
		{
			throw new ArgumentException("Master key must not be empty.", nameof(masterKey));
		}

		var info = Encoding.UTF8.GetBytes(purpose);
		return HKDF.DeriveKey(
			_options.HashAlgorithm,
			masterKey,
			_options.DefaultOutputLength,
			salt: context.ToArray(),
			info: info);
	}

	/// <summary>
	/// Derives a key for a specific purpose without additional context.
	/// </summary>
	/// <param name="masterKey">The master key material to derive from.</param>
	/// <param name="purpose">The purpose identifier for key separation (e.g., "field-encryption", "tenant-key").</param>
	/// <returns>The derived key bytes.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="masterKey"/> or <paramref name="purpose"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="masterKey"/> is empty or <paramref name="purpose"/> is whitespace.</exception>
	public byte[] DeriveKey(byte[] masterKey, string purpose)
	{
		ArgumentNullException.ThrowIfNull(masterKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

		if (masterKey.Length == 0)
		{
			throw new ArgumentException("Master key must not be empty.", nameof(masterKey));
		}

		var info = Encoding.UTF8.GetBytes(purpose);
		return HKDF.DeriveKey(
			_options.HashAlgorithm,
			masterKey,
			_options.DefaultOutputLength,
			salt: [],
			info: info);
	}
}
