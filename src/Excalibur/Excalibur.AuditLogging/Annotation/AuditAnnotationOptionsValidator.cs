// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Annotation;

/// <summary>
/// Validates <see cref="AuditAnnotationOptions"/> at startup. Reflection-free (AOT-safe).
/// </summary>
internal sealed class AuditAnnotationOptionsValidator : IValidateOptions<AuditAnnotationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AuditAnnotationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxTagsPerEvent is < 1 or > 1000)
		{
			failures.Add($"{nameof(AuditAnnotationOptions.MaxTagsPerEvent)} must be between 1 and 1000.");
		}

		if (options.MaxTagLength is < 1 or > 512)
		{
			failures.Add($"{nameof(AuditAnnotationOptions.MaxTagLength)} must be between 1 and 512.");
		}

		if (options.MaxNoteLength is < 1 or > 32_000)
		{
			failures.Add($"{nameof(AuditAnnotationOptions.MaxNoteLength)} must be between 1 and 32000.");
		}

		if (options.MaxNotesPerEvent is < 1 or > 10_000)
		{
			failures.Add($"{nameof(AuditAnnotationOptions.MaxNotesPerEvent)} must be between 1 and 10000.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
