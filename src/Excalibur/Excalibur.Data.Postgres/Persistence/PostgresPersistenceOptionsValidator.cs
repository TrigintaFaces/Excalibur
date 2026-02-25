// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Options validator for Postgres persistence.
/// </summary>
internal sealed class PostgresPersistenceOptionsValidator : IValidateOptions<PostgresPersistenceOptions>
{
	/// <inheritdoc/>
	[RequiresUnreferencedCode("Calls Excalibur.Data.Postgres.Persistence.PostgresPersistenceOptions.Validate()")]
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
