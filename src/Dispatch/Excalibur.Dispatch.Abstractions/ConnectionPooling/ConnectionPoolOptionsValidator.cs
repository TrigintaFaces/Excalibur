// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Validates <see cref="ConnectionPoolOptions"/> by delegating to <see cref="ConnectionPoolOptions.Validate"/>.
/// </summary>
internal sealed class ConnectionPoolOptionsValidator : IValidateOptions<ConnectionPoolOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ConnectionPoolOptions options)
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
