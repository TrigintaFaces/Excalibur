// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.OpenSearch.Projections;

/// <summary>
/// Validates <see cref="OpenSearchProjectionStoreOptions"/> at startup.
/// </summary>
internal sealed class OpenSearchProjectionStoreOptionsValidator
	: IValidateOptions<OpenSearchProjectionStoreOptions>
{
	/// <inheritdoc />
	/// <remarks>
	/// The node rules are DELEGATED rather than restated. They used to be a second copy here, and the two
	/// copies diverged: the one in <see cref="OpenSearchProjectionStoreOptions.Validate"/> knows a
	/// registration that supplies its own client reads no node address, and this one did not -- so a
	/// client-factory registration was refused by whichever copy ran. Delegating leaves one statement of
	/// the rule, and this validator keeps only the checks that are genuinely its own.
	/// </remarks>
	public ValidateOptionsResult Validate(string? name, OpenSearchProjectionStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		try
		{
			options.Validate();
		}
		catch (InvalidOperationException ex)
		{
			return ValidateOptionsResult.Fail(ex.Message);
		}

		if (options.RequestTimeoutSeconds <= 0)
		{
			return ValidateOptionsResult.Fail("RequestTimeoutSeconds must be greater than 0.");
		}

		if (options.NumberOfShards < 1)
		{
			return ValidateOptionsResult.Fail("NumberOfShards must be at least 1.");
		}

		if (options.NumberOfReplicas < 0)
		{
			return ValidateOptionsResult.Fail("NumberOfReplicas must be non-negative.");
		}

		return ValidateOptionsResult.Success;
	}
}
