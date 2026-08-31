// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Source-generated data-annotation validation for <see cref="PostgresPersistenceOptions"/> and its
/// nested option objects. Generated at compile time, so validation costs no reflection.
/// </summary>
[OptionsValidator]
internal sealed partial class PostgresPersistenceOptionsAnnotationValidator : IValidateOptions<PostgresPersistenceOptions>
{
}

/// <summary>
/// Options validator for Postgres persistence.
/// </summary>
internal sealed class PostgresPersistenceOptionsValidator : IValidateOptions<PostgresPersistenceOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, PostgresPersistenceOptions options)
	{
		try
		{
			options.Validate();
			return ValidateOptionsResult.Success;
		}
		catch (Exception ex)
		{
			return ValidateOptionsResult.Fail(ex.Message);
		}
	}
}
