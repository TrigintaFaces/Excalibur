// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Refuses a grant-sync atomicity setting that names neither behaviour.
/// </summary>
/// <remarks>
/// Configuration binding writes any integer into an enum property without complaint, and an undefined value
/// here would not match <see cref="GrantSyncAtomicity.Required"/>, so the start-up refusal this option exists
/// to raise would silently not happen.
/// </remarks>
internal sealed class ActivityGroupSyncOptionsValidator : IValidateOptions<ActivityGroupSyncOptions>
{
	public ValidateOptionsResult Validate(string? name, ActivityGroupSyncOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return Enum.IsDefined(options.GrantSyncAtomicity)
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(
				$"{nameof(ActivityGroupSyncOptions.GrantSyncAtomicity)} is {(int)options.GrantSyncAtomicity}, which "
				+ $"is neither {nameof(GrantSyncAtomicity.Required)} nor {nameof(GrantSyncAtomicity.BestEffort)}.");
	}
}
