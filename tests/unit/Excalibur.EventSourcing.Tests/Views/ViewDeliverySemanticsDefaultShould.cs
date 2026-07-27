// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Views;

// Independent regression lock (author != implementer) for he7n1n — the STRUCTURAL "default is the safe mode"
// invariant of the materialized-view delivery declaration. SA named this as the first thing he verifies at
// REVIEW_ARCH; the gate itself (ExactlyOnce + non-atomic store => reject) is covered by the impl-authored
// MaterializedViewStartupValidatorShould, but the value-0-is-safe invariant was bound NOWHERE.
//
// WHY IT MATTERS (enforce-invariants-structurally). A projection declares its delivery requirement on its own
// builder; if it declares nothing it must default to the SAFE mode (ExactlyOnce → requires an atomic store),
// never the relaxed one. That safety rests on TWO facts a future edit could silently break:
//   1. ExactlyOnce is enum value 0 — so `default(ViewDeliverySemantics)` and any unset field are ExactlyOnce.
//      A renumber (0 = AtLeastOnceIdempotent) would flip every undeclared projection to at-least-once and the
//      atomic gate would stop firing for them — a silent correctness regression (double-counting on crash).
//   2. The default interface property returns ExactlyOnce — so a builder that overrides nothing is exactly-once.
//
// SAFETY + LIVENESS (testing-patterns §3): "the default is ExactlyOnce" is satisfied by a property HARDCODED to
// ExactlyOnce that ignores overrides — which would break the opt-in. So the liveness arm proves a builder that
// DECLARES AtLeastOnceIdempotent is honored.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ViewDeliverySemanticsDefaultShould
{
	[Fact]
	public void HaveExactlyOnceAsEnumValueZero_SoAnUnsetSemanticsIsTheSafeMode()
	{
		// SAFETY (structural). RED against a renumber that made AtLeastOnceIdempotent the zero value.
		((int)ViewDeliverySemantics.ExactlyOnce).ShouldBe(
			0,
			"ExactlyOnce MUST be enum value 0 so the unspecified default is the SAFE mode. If a renumber makes " +
			"AtLeastOnceIdempotent 0, every projection that never declared its semantics silently becomes " +
			"at-least-once and the atomic-store gate stops firing for it — double-counting on crash.");

		default(ViewDeliverySemantics).ShouldBe(
			ViewDeliverySemantics.ExactlyOnce,
			"the default-of-the-type must be the safe exactly-once mode.");
	}

	[Fact]
	public void DefaultAnUndeclaringProjectionToExactlyOnce()
	{
		// SAFETY. A builder that overrides nothing must report ExactlyOnce via the default interface property —
		// the safe default the atomic gate keys on.
		IMaterializedViewBuilder<TestView> undeclaring = new UndeclaringBuilder();

		undeclaring.DeliverySemantics.ShouldBe(
			ViewDeliverySemantics.ExactlyOnce,
			"a projection that does not declare its delivery semantics must default to ExactlyOnce (the safe, " +
			"atomic-required mode) through the default interface property.");
	}

	[Fact]
	public void HonorAProjectionThatDeclaresAtLeastOnceIdempotent()
	{
		// LIVENESS. Proves the property is NOT hardcoded to ExactlyOnce — an explicit opt-in is respected, or the
		// whole supported at-least-once mode is unreachable and the "default is ExactlyOnce" arm is vacuous.
		IMaterializedViewBuilder<TestView> idempotent = new IdempotentBuilder();

		idempotent.DeliverySemantics.ShouldBe(
			ViewDeliverySemantics.AtLeastOnceIdempotent,
			"a projection that explicitly declares AtLeastOnceIdempotent must report it — otherwise the supported " +
			"at-least-once mode (on a non-atomic store like Elasticsearch/OpenSearch) can never be selected.");
	}

	private sealed class TestView;

	// Overrides nothing — inherits the default DeliverySemantics.
	private sealed class UndeclaringBuilder : IMaterializedViewBuilder<TestView>
	{
		public string ViewName => "undeclaring-view";

		public IReadOnlyList<Type> HandledEventTypes => [];

		// AggregateId moved off IDomainEvent to the persistence envelope; the view id is not exercised
		// by these delivery-semantics tests, so a stable constant suffices.
		public string? GetViewId(IDomainEvent @event) => "view";

		public TestView Apply(TestView view, IDomainEvent @event) => view;
	}

	// Explicitly opts into the at-least-once idempotent mode.
	private sealed class IdempotentBuilder : IMaterializedViewBuilder<TestView>
	{
		public string ViewName => "idempotent-view";

		public IReadOnlyList<Type> HandledEventTypes => [];

		public ViewDeliverySemantics DeliverySemantics => ViewDeliverySemantics.AtLeastOnceIdempotent;

		public string? GetViewId(IDomainEvent @event) => "view";

		public TestView Apply(TestView view, IDomainEvent @event) => view;
	}
}
