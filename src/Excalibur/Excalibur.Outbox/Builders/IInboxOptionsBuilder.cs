// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Fluent builder interface for constructing immutable <see cref="InboxOptions"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern. Use one of
/// the factory methods on <see cref="InboxOptions"/> to create a builder initialized with
/// a preset, then chain override methods as needed.
/// </para>
/// <para>
/// Core configuration methods are defined on <see cref="IInboxOptionsCoreBuilder"/>.
/// Advanced tuning methods are defined on <see cref="IInboxOptionsAdvancedBuilder"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Preset-based (recommended)
/// var options = InboxOptions.HighThroughput().Build();
///
/// // Preset with overrides
/// var options = InboxOptions.Balanced()
///     .WithQueueCapacity(1000)
///     .WithParallelism(8)
///     .Build();
///
/// // Full custom
/// var options = InboxOptions.Custom()
///     .WithQueueCapacity(500)
///     .WithProducerBatchSize(100)
///     .WithConsumerBatchSize(50)
///     .WithMaxAttempts(7)
///     .Build();
/// </code>
/// </example>
public interface IInboxOptionsBuilder : IInboxOptionsCoreBuilder, IInboxOptionsAdvancedBuilder
{
    /// <summary>
    /// Builds the immutable <see cref="InboxOptions"/> instance.
    /// </summary>
    /// <returns>The configured inbox options.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the configuration is invalid.</exception>
    InboxOptions Build();
}
