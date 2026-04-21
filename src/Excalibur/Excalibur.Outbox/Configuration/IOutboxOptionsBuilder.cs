// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Fluent builder interface for configuring <see cref="OutboxOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// Options are validated at <see cref="Build"/> time, ensuring fail-fast behavior.
/// </para>
/// <para>
/// Core configuration methods are defined on <see cref="IOutboxOptionsCoreBuilder"/>.
/// Reliability and maintenance methods are defined on <see cref="IOutboxOptionsReliabilityBuilder"/>.
/// </para>
/// <para>
/// Start with a preset using <see cref="OutboxOptions.HighThroughput"/>,
/// <see cref="OutboxOptions.Balanced"/>, <see cref="OutboxOptions.HighReliability"/>,
/// or <see cref="OutboxOptions.Custom"/>, then apply overrides as needed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Using a preset with overrides
/// var options = OutboxOptions.HighThroughput()
///     .WithBatchSize(2000)
///     .WithProcessorId("worker-1")
///     .Build();
///
/// // Full custom configuration
/// var customOptions = OutboxOptions.Custom()
///     .WithBatchSize(500)
///     .WithPollingInterval(TimeSpan.FromMilliseconds(500))
///     .WithParallelism(4)
///     .Build();
/// </code>
/// </example>
public interface IOutboxOptionsBuilder : IOutboxOptionsCoreBuilder, IOutboxOptionsReliabilityBuilder
{
    /// <summary>
    /// Builds the immutable <see cref="OutboxOptions"/> instance.
    /// </summary>
    /// <returns>The configured options.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration is invalid (e.g., RetentionPeriod is less than CleanupInterval).
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method validates all configured settings and returns an immutable
    /// <see cref="OutboxOptions"/> instance. After calling <see cref="Build"/>,
    /// the options cannot be modified.
    /// </para>
    /// </remarks>
    OutboxOptions Build();
}
