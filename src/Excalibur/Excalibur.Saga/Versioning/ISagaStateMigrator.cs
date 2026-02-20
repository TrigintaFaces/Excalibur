// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Versioning;

/// <summary>
/// Migrates saga state from one version to another.
/// </summary>
/// <typeparam name="TFrom">The source state type being migrated from.</typeparam>
/// <typeparam name="TTo">The target state type being migrated to.</typeparam>
/// <remarks>
/// <para>
/// Implement this interface to define how saga state is transformed when the
/// saga definition evolves. Each migrator handles one version transition
/// (e.g., V1 to V2, V2 to V3). The infrastructure chains migrators to handle
/// multi-version upgrades.
/// </para>
/// <para>
/// This follows the same pattern as event upcasting in event sourcing
/// (see <c>Excalibur.EventSourcing</c> snapshot upgrading via BFS).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [SagaVersion(1, 2)]
/// public class OrderSagaV1ToV2Migrator : ISagaStateMigrator&lt;OrderSagaStateV1, OrderSagaStateV2&gt;
/// {
///     public Task&lt;OrderSagaStateV2&gt; MigrateAsync(OrderSagaStateV1 source, CancellationToken ct)
///     {
///         return Task.FromResult(new OrderSagaStateV2
///         {
///             OrderId = source.OrderId,
///             CustomerName = source.Name,
///             TotalAmount = source.Amount,
///         });
///     }
/// }
/// </code>
/// </example>
public interface ISagaStateMigrator<in TFrom, TTo>
	where TFrom : class
	where TTo : class
{
	/// <summary>
	/// Migrates the saga state from the source type to the target type.
	/// </summary>
	/// <param name="source">The source state to migrate.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The migrated state in the target format.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="source"/> is null.
	/// </exception>
	Task<TTo> MigrateAsync(TFrom source, CancellationToken cancellationToken);
}
