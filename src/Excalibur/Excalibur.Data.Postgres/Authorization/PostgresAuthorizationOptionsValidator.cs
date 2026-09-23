// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Validation;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.Authorization;

/// <summary>
/// Refuses a schema name the authorization stores could not safely place in a statement.
/// </summary>
internal sealed class PostgresAuthorizationOptionsValidator : IValidateOptions<PostgresAuthorizationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, PostgresAuthorizationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return !string.IsNullOrEmpty(options.SchemaName) && SqlIdentifierValidator.IsValid(options.SchemaName)
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(
				$"{nameof(options.SchemaName)} '{options.SchemaName}' is not a valid identifier. It is written into "
				+ "every authorization statement, so it may contain only ASCII letters, digits and underscores.");
	}
}
