// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Core.Authorization.Roles;

/// <summary>Validates <see cref="RoleOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class RoleOptionsValidator : IValidateOptions<RoleOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, RoleOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxHierarchyDepth is < 1 or > 10)
		{
			failures.Add($"{nameof(RoleOptions.MaxHierarchyDepth)} must be between 1 and 10.");
		}

		if (options.PermissionCacheDurationSeconds is < 0 or > 86400)
		{
			failures.Add($"{nameof(RoleOptions.PermissionCacheDurationSeconds)} must be between 0 and 86400.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
