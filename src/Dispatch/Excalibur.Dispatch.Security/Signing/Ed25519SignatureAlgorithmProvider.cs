// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides Ed25519 signing and verification for the composite signing service.
/// </summary>
/// <remarks>
/// <para>
/// Ed25519 requires .NET 10 or later for native BCL support via <c>System.Security.Cryptography</c>.
/// On earlier runtimes, all operations throw <see cref="PlatformNotSupportedException"/>.
/// </para>
/// <para>
/// No third-party libraries are used. If the target framework does not provide native Ed25519 support,
/// consumers should use <see cref="SigningAlgorithm.ECDSASHA256"/> instead.
/// </para>
/// </remarks>
public sealed class Ed25519SignatureAlgorithmProvider : ISignatureAlgorithmProvider
{
	/// <inheritdoc />
	public bool SupportsAlgorithm(SigningAlgorithm algorithm)
		=> algorithm == SigningAlgorithm.Ed25519;

	/// <inheritdoc />
	public Task<byte[]> SignAsync(
		byte[] data,
		byte[] keyMaterial,
		SigningAlgorithm algorithm,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(keyMaterial);

		throw new PlatformNotSupportedException(
			"Ed25519 requires .NET 10 or later with native BCL support. " +
			"Use SigningAlgorithm.ECDSASHA256 for asymmetric signing on earlier runtimes.");
	}

	/// <inheritdoc />
	public Task<bool> VerifyAsync(
		byte[] data,
		byte[] signature,
		byte[] keyMaterial,
		SigningAlgorithm algorithm,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(signature);
		ArgumentNullException.ThrowIfNull(keyMaterial);

		throw new PlatformNotSupportedException(
			"Ed25519 requires .NET 10 or later with native BCL support. " +
			"Use SigningAlgorithm.ECDSASHA256 for asymmetric signing on earlier runtimes.");
	}
}
