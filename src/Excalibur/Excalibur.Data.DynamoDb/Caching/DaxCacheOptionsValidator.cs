// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.DynamoDb.Caching;

/// <summary>
/// Validates <see cref="DaxCacheOptions"/> at startup.
/// </summary>
internal sealed class DaxCacheOptionsValidator : IValidateOptions<DaxCacheOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DaxCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ClusterEndpoint))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.ClusterEndpoint)} is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
