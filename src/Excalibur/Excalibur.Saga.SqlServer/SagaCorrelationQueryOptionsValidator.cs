// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Queries;

using Microsoft.Extensions.Options;

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// Validates <see cref="SagaCorrelationQueryOptions"/> settings.
/// </summary>
internal sealed class SagaCorrelationQueryOptionsValidator : IValidateOptions<SagaCorrelationQueryOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SagaCorrelationQueryOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Saga correlation query options cannot be null.");
		}

		if (options.MaxResults is < 1 or > 10000)
		{
			return ValidateOptionsResult.Fail("MaxResults must be between 1 and 10000.");
		}

		return ValidateOptionsResult.Success;
	}
}
