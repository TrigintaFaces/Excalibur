// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Snapshots.Compression;

/// <summary>Validates <see cref="SnapshotCompressionOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class SnapshotCompressionOptionsValidator : IValidateOptions<SnapshotCompressionOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SnapshotCompressionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (!Enum.IsDefined(options.Algorithm))
		{
			failures.Add($"{nameof(SnapshotCompressionOptions.Algorithm)} must be a defined compression algorithm.");
		}

		if (options.MinimumSizeBytes < 0)
		{
			failures.Add($"{nameof(SnapshotCompressionOptions.MinimumSizeBytes)} must not be negative.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
