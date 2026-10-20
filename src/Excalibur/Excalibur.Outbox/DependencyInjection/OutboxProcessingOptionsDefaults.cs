// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox.Outbox;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.DependencyInjection;

/// <summary>Translates builder defaults before explicit background processing configuration.</summary>
internal sealed class OutboxProcessingOptionsDefaults(OutboxOptions outbox) : IConfigureOptions<OutboxProcessingOptions>
{
    public void Configure(OutboxProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PollingInterval = outbox.PollingInterval;
        options.MaxRetries = outbox.Retry.MaxRetryCount;
    }
}
