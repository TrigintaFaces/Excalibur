// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Oracle.DependencyInjection;

/// <summary>
/// Validates <see cref="OracleSnapshotStoreOptions"/> at startup via <c>ValidateOnStart</c>.
/// </summary>
internal sealed class OracleSnapshotStoreOptionsValidator : IValidateOptions<OracleSnapshotStoreOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, OracleSnapshotStoreOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail("Oracle snapshot store requires a non-empty ConnectionString.");
		}

		if (string.IsNullOrWhiteSpace(options.Schema))
		{
			return ValidateOptionsResult.Fail("Oracle snapshot store requires a non-empty Schema.");
		}

		if (string.IsNullOrWhiteSpace(options.Table))
		{
			return ValidateOptionsResult.Fail("Oracle snapshot store requires a non-empty Table.");
		}

		return ValidateOptionsResult.Success;
	}
}
