// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Refuses a pipeline in which tenant context would be read before it is established.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is one type rather than a line in each consumer.</b> It is a single correctness rule
/// over a list that two consumers read: the pipeline, and the invoker the dispatcher actually executes.
/// Written once in each, the two copies drift, and the copy that stops matching is the one nobody
/// notices — because a pipeline that fails to refuse looks exactly like a pipeline with nothing to
/// refuse.
/// </para>
/// <para>
/// <b>CAPABILITY IS READ AT TYPE LEVEL, THROUGH THE IDENTITY HELPER. This is the whole correctness of
/// the rule and it is easy to undo by accident.</b> A pipeline entry is frequently not the middleware:
/// a middleware registered <c>Scoped</c> is replaced by a per-dispatch stand-in that holds no instance
/// at all, <c>UseAt&lt;T&gt;(stage)</c> wraps it, and <c>ForMessageKinds(...).Use&lt;T&gt;()</c> wraps it
/// again. None of those wrappers implements the capability markers. So an instance test —
/// <c>entry is IEstablishesTenantContext</c> — is <b>false for a middleware that does establish tenant
/// context</b>, and the rule silently concludes there is no ordering relation to enforce.
/// </para>
/// <para>
/// That is not hypothetical: the framework's own tenant establisher is registered <c>TryAddScoped</c>,
/// so in any host with a service-scope factory it always arrives wrapped. An instance-level test makes
/// this rule unreachable in essentially every real application while leaving it green under tests that
/// construct the pipeline from bare, undecorated doubles.
/// </para>
/// <para>
/// <see cref="MiddlewareIdentity.TypeOf"/> resolves an entry to the type it stands for, which is what
/// the capability question is actually about. <c>IsAssignableFrom</c> over a <see cref="Type"/> already
/// in hand performs no member reflection and is safe under trimming and ahead-of-time compilation.
/// </para>
/// <para>
/// <b>SCOPE, held deliberately narrow.</b> This is the one constraint the stage enum cannot express.
/// Constraints that ARE structural — deduplication before side effects, Processing before
/// PostProcessing — are left to the enum, so there is one source of truth per property.
/// </para>
/// <para>
/// <b>KNOWN GAP, recorded rather than silently accepted.</b> The rule is evaluated over the FULL
/// middleware set, while execution filters by message kind. Filtering preserves relative order, but it
/// can remove the establisher and keep the reader — leaving a reader that runs with nothing to
/// establish tenancy for it, which this rule then reports as having no ordering relation to violate.
/// Whether a reader with no establisher in the applicable subsequence should itself be refused is a
/// contract question, not an oversight, and it is deliberately not decided here.
/// </para>
/// </remarks>
internal static class TenantOrderingRule
{
	/// <summary>
	/// Returns <paramref name="middlewares"/> unchanged once the tenant-ordering rule has been checked
	/// against the order those middleware will execute in.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>It returns the list so the check cannot be separated from the value.</b> The caller assigns
	/// the result, so a pipeline list cannot be published to a consumer without having been verified —
	/// removing the rule means removing the call that produces the value being assigned, which does not
	/// survive review the way a deleted statement does. This is the same shape as composing a floor with
	/// <c>min</c>: the unsafe outcome is not merely discouraged, it has no expression.
	/// </para>
	/// <para>
	/// The input is in registration order; execution order is by effective stage. The check is therefore
	/// run over an ordered copy, and the caller's list is returned untouched for the consumers that sort
	/// it themselves.
	/// </para>
	/// </remarks>
	/// <param name="middlewares"> The resolved middleware, in registration order. </param>
	/// <returns> The same list, unmodified. </returns>
	/// <exception cref="InvalidOperationException">
	/// A middleware that reads tenant context would execute before the last middleware that establishes
	/// it.
	/// </exception>
	public static IReadOnlyList<IDispatchMiddleware> Verified(IReadOnlyList<IDispatchMiddleware> middlewares)
	{
		ArgumentNullException.ThrowIfNull(middlewares);

		Verify(InExecutionOrder(middlewares));
		return middlewares;
	}

	/// <summary>
	/// Sorts into execution order, checks the rule, and returns the sorted array.
	/// </summary>
	/// <param name="middlewares"> The middleware, in registration order. </param>
	/// <returns> The middleware in execution order. </returns>
	/// <exception cref="InvalidOperationException">
	/// A middleware that reads tenant context would execute before the last middleware that establishes
	/// it.
	/// </exception>
	public static IDispatchMiddleware[] OrderAndVerify(IEnumerable<IDispatchMiddleware> middlewares)
	{
		ArgumentNullException.ThrowIfNull(middlewares);

		var ordered = InExecutionOrder([.. middlewares]);
		Verify(ordered);
		return ordered;
	}

	/// <summary>
	/// Orders by effective stage, stably, so equal stages keep registration order.
	/// </summary>
	/// <remarks>
	/// Keyed identically to the sort the invoker runs, so the order verified here is the order that
	/// executes. Both are stable and both key on <c>Stage ?? End</c>, so they produce the same
	/// permutation for the same input.
	/// </remarks>
	private static IDispatchMiddleware[] InExecutionOrder(IReadOnlyList<IDispatchMiddleware> middlewares) =>
		[.. middlewares.OrderBy(static m => (int?)m.Stage ?? (int)DispatchMiddlewareStage.End)];

	private static void Verify(IDispatchMiddleware[] ordered)
	{
		var lastEstablisher = Array.FindLastIndex(ordered, static m => Declares<IEstablishesTenantContext>(m));
		if (lastEstablisher < 0)
		{
			// Nothing establishes tenant context, so there is no ordering relation to violate. Whether a
			// reader registered with NO establisher should itself be refused is a separate question and
			// is deliberately not decided here.
			return;
		}

		var firstReader = Array.FindIndex(ordered, static m => Declares<IRequiresTenantContext>(m));
		if (firstReader < 0 || firstReader >= lastEstablisher)
		{
			return;
		}

		throw new InvalidOperationException(
			$"Middleware '{MiddlewareIdentity.TypeOf(ordered[firstReader]).Name}' declares "
			+ "IRequiresTenantContext but is ordered before "
			+ $"'{MiddlewareIdentity.TypeOf(ordered[lastEstablisher]).Name}', which declares "
			+ "IEstablishesTenantContext. It would run outside the ambient tenant scope and observe no "
			+ "tenant. Give the READER a LATER DispatchMiddlewareStage than the establisher, or give the "
			+ "ESTABLISHER an earlier one. Middleware execute in ascending stage order, so a reader that "
			+ "runs too early needs a HIGHER stage, not a lower one. Within one stage, the order is the "
			+ "order you added them, so an establisher and a reader sharing a stage are fixed by adding "
			+ "the establisher first.");
	}

	/// <summary>
	/// Whether the middleware an entry stands for declares <typeparamref name="TCapability"/>.
	/// </summary>
	/// <remarks>
	/// Asks the question of the TYPE the entry represents, never of the entry, which may be a decorator
	/// or a per-dispatch stand-in carrying no instance.
	/// </remarks>
	/// <typeparam name="TCapability"> The capability marker interface. </typeparam>
	/// <param name="middleware"> The pipeline entry. </param>
	/// <returns> <see langword="true"/> if the represented type declares the capability. </returns>
	private static bool Declares<TCapability>(IDispatchMiddleware middleware) =>
		typeof(TCapability).IsAssignableFrom(MiddlewareIdentity.TypeOf(middleware));
}
