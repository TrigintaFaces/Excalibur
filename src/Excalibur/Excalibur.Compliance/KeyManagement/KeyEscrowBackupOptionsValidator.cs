// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.KeyManagement;

/// <summary>Validates <see cref="KeyEscrowBackupOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class KeyEscrowBackupOptionsValidator : IValidateOptions<KeyEscrowBackupOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, KeyEscrowBackupOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.EscrowProvider))
		{
			failures.Add($"{nameof(KeyEscrowBackupOptions.EscrowProvider)} must be a non-empty provider name.");
		}

		if (options.SplitThreshold < 2)
		{
			failures.Add(
				$"{nameof(KeyEscrowBackupOptions.SplitThreshold)} must be at least 2. " +
				"A 1-of-N quorum is security-nonsense (any single custodian reconstructs the secret), " +
				"and the underlying secret-sharing scheme requires a threshold of at least 2.");
		}

		if (options.TotalShares < options.SplitThreshold)
		{
			failures.Add(
				$"{nameof(KeyEscrowBackupOptions.TotalShares)} ({options.TotalShares}) must be greater than or equal to " +
				$"{nameof(KeyEscrowBackupOptions.SplitThreshold)} ({options.SplitThreshold}) for a valid M-of-N secret-sharing scheme.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
