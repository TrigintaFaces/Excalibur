// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.DynamoDb.Snapshots;

internal sealed class DynamoDbSnapshotStoreOptionsValidator : IValidateOptions<DynamoDbSnapshotStoreOptions>
{
    public ValidateOptionsResult Validate(string? name, DynamoDbSnapshotStoreOptions options)
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
