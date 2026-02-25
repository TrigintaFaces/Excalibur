// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Represents the freeze status of Dispatch performance caches.
/// </summary>
/// <remarks>
/// <para>
/// PERF-22: This record provides visibility into which caches have been frozen
/// for optimized production performance. Use this to diagnose performance issues
/// or verify that auto-freeze has completed successfully.
/// </para>
/// </remarks>
/// <param name="HandlerInvokerFrozen">Whether the handler invoker cache is frozen.</param>
/// <param name="HandlerRegistryFrozen">Whether the manual handler registry cache is frozen.</param>
/// <param name="HandlerActivatorFrozen">Whether the handler activator cache is frozen.</param>
/// <param name="ResultFactoryFrozen">Whether the result factory cache is frozen.</param>
/// <param name="MiddlewareEvaluatorFrozen">Whether the middleware applicability evaluator cache is frozen.</param>
/// <param name="FrozenAt">The timestamp when caches were frozen, or null if not frozen.</param>
public sealed record CacheFreezeStatus(
	bool HandlerInvokerFrozen,
	bool HandlerRegistryFrozen,
	bool HandlerActivatorFrozen,
	bool ResultFactoryFrozen,
	bool MiddlewareEvaluatorFrozen,
	DateTimeOffset? FrozenAt)
{
	/// <summary>
	/// Gets a value indicating whether all caches are frozen.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if all caches are frozen; otherwise, <see langword="false"/>.
	/// </value>
	public bool AllFrozen =>
		HandlerInvokerFrozen &&
		HandlerRegistryFrozen &&
		HandlerActivatorFrozen &&
		ResultFactoryFrozen &&
		MiddlewareEvaluatorFrozen;

	/// <summary>
	/// Gets the default unfrozen status.
	/// </summary>
	public static CacheFreezeStatus Unfrozen { get; } = new(
		HandlerInvokerFrozen: false,
		HandlerRegistryFrozen: false,
		HandlerActivatorFrozen: false,
		ResultFactoryFrozen: false,
		MiddlewareEvaluatorFrozen: false,
		FrozenAt: null);
}
