// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides FIPS 140-2 compliance validation for cryptographic operations.
/// </summary>
/// <remarks>
/// <para>
/// FIPS 140-2 compliance is required for certain regulated environments.
/// This service validates that:
/// <list type="bullet">
/// <item>The operating system is configured for FIPS mode</item>
/// <item>Cryptographic operations use FIPS-validated algorithms</item>
/// <item>Key lengths meet FIPS requirements (AES-256 for encryption)</item>
/// </list>
/// </para>
/// <para>
/// This service delegates FIPS detection to <see cref="IFipsDetector"/>, enabling
/// unit tests to mock the FIPS status without requiring actual OS configuration changes.
/// </para>
/// </remarks>
public sealed class FipsValidationService
{
	private readonly IFipsDetector _fipsDetector;

	/// <summary>
	/// Initializes a new instance of the <see cref="FipsValidationService"/> class.
	/// </summary>
	/// <param name="fipsDetector">The FIPS detector for platform-specific detection.</param>
	public FipsValidationService(IFipsDetector fipsDetector)
	{
		_fipsDetector = fipsDetector ?? throw new ArgumentNullException(nameof(fipsDetector));
	}

	/// <summary>
	/// Gets a value indicating whether the system is running in FIPS 140-2 mode.
	/// </summary>
	public bool IsFipsEnabled => _fipsDetector.IsFipsEnabled;

	/// <summary>
	/// Gets the current FIPS detection result.
	/// </summary>
	public FipsDetectionResult DetectionResult => _fipsDetector.GetStatus();

	/// <summary>
	/// Validates that FIPS compliance is enabled, throwing if not.
	/// </summary>
	/// <exception cref="EncryptionException">Thrown when FIPS compliance is not enabled.</exception>
	public void RequireFipsCompliance()
	{
		if (!IsFipsEnabled)
		{
			var status = DetectionResult;
			throw new EncryptionException(
				$"FIPS 140-2 compliance required but not enabled. {status.ValidationDetails}")
			{
				ErrorCode = EncryptionErrorCode.FipsComplianceViolation
			};
		}
	}

	/// <summary>
	/// Validates that a specific encryption algorithm is FIPS 140-2 compliant.
	/// </summary>
	/// <param name="algorithm">The algorithm to validate.</param>
	/// <returns>True if the algorithm is FIPS compliant; otherwise, false.</returns>
	public bool IsAlgorithmFipsCompliant(EncryptionAlgorithm algorithm)
	{
		return algorithm switch
		{
			// AES-256-GCM is FIPS 140-2 approved (SP 800-38D)
			EncryptionAlgorithm.Aes256Gcm => true,

			// AES-256-CBC with HMAC-SHA256 is FIPS 140-2 approved
			EncryptionAlgorithm.Aes256CbcHmac => true,

			_ => false
		};
	}

	/// <summary>
	/// Validates that a key length is FIPS 140-2 compliant for AES.
	/// </summary>
	/// <param name="keySizeInBits">The key size in bits.</param>
	/// <returns>True if the key size is FIPS compliant for AES; otherwise, false.</returns>
	public bool IsKeySizeFipsCompliant(int keySizeInBits)
	{
		// FIPS 197 specifies AES key sizes: 128, 192, or 256 bits
		// For highest security, 256 bits is recommended
		return keySizeInBits is 128 or 192 or 256;
	}

	/// <summary>
	/// Gets a detailed compliance report for the current system.
	/// </summary>
	/// <returns>A detailed FIPS compliance report.</returns>
	public FipsComplianceReport GetComplianceReport()
	{
		var status = DetectionResult;

		return new FipsComplianceReport
		{
			IsCompliant = status.IsFipsEnabled,
			Platform = status.Platform,
			ValidationDetails = status.ValidationDetails,
			CheckedAt = DateTimeOffset.UtcNow,
			ApprovedAlgorithms =
			[
				new AlgorithmComplianceInfo
				{
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					IsApproved = true,
					Standard = "NIST SP 800-38D",
					Notes = "Recommended for field-level encryption"
				},
				new AlgorithmComplianceInfo
				{
					Algorithm = EncryptionAlgorithm.Aes256CbcHmac,
					IsApproved = true,
					Standard = "FIPS 197, FIPS 198-1",
					Notes = "Legacy support; prefer AES-GCM"
				}
			]
		};
	}
}

/// <summary>
/// Represents a detailed FIPS compliance report.
/// </summary>
public sealed record FipsComplianceReport
{
	/// <summary>
	/// Gets a value indicating whether the system is FIPS 140-2 compliant.
	/// </summary>
	public bool IsCompliant { get; init; }

	/// <summary>
	/// Gets the platform identifier.
	/// </summary>
	public required string Platform { get; init; }

	/// <summary>
	/// Gets validation details.
	/// </summary>
	public required string ValidationDetails { get; init; }

	/// <summary>
	/// Gets the timestamp when compliance was checked.
	/// </summary>
	public DateTimeOffset CheckedAt { get; init; }

	/// <summary>
	/// Gets information about approved algorithms.
	/// </summary>
	public required IReadOnlyList<AlgorithmComplianceInfo> ApprovedAlgorithms { get; init; }
}

/// <summary>
/// Represents FIPS compliance information for a specific algorithm.
/// </summary>
public sealed record AlgorithmComplianceInfo
{
	/// <summary>
	/// Gets the encryption algorithm.
	/// </summary>
	public EncryptionAlgorithm Algorithm { get; init; }

	/// <summary>
	/// Gets a value indicating whether this algorithm is FIPS approved.
	/// </summary>
	public bool IsApproved { get; init; }

	/// <summary>
	/// Gets the NIST or RFC standard reference.
	/// </summary>
	public required string Standard { get; init; }

	/// <summary>
	/// Gets additional notes about the algorithm.
	/// </summary>
	public string? Notes { get; init; }
}
