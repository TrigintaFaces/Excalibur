// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Delivery;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.DependencyInjection;

/// <summary>Translates builder defaults before explicit delivery configuration.</summary>
internal sealed class OutboxDeliveryOptionsDefaults(OutboxOptions outbox) : IConfigureOptions<OutboxDeliveryOptions>
{
    public void Configure(OutboxDeliveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.ProducerBatchSize = outbox.BatchSize;
        options.ConsumerBatchSize = outbox.BatchSize;
        options.QueueCapacity = Math.Max(options.QueueCapacity, outbox.BatchSize);
        // Retry count excludes the initial attempt. An unrepresentable default remains invalid
        // until the consumer supplies a representable total; final options validation owns refusal.
        options.MaxAttempts = outbox.Retry.MaxRetryCount == int.MaxValue ? 0 : outbox.Retry.MaxRetryCount + 1;
        if (outbox.EnableParallelProcessing)
        {
            options.BatchProcessing.ParallelProcessingDegree = outbox.MaxDegreeOfParallelism;
        }
    }
}
