// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Workflows;

namespace Excalibur.Workflows.Abstractions.Tests;

/// <summary>
/// Smoke coverage for the workflow journal event seam: the concrete entry type loads, exposes its
/// discriminator via the abstract override, and round-trips the replay cursor.
/// </summary>
/// <remarks>
/// The Category trait is load-bearing, not decoration. Every shard selects by
/// <c>Category=...</c>, so a test class without one is selected by NO filter: the assembly is still
/// launched, reports "No test matches the given testcase filter", and exits 0. The shard then goes
/// green having executed nothing here. This class carried no traits at all and had never run.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Workflows")]
public sealed class WorkflowJournalEventShould
{
	[Fact]
	public void ExposeDiscriminatorAndRoundTripVersion()
	{
		var started = new WorkflowStarted
		{
			AggregateId = "wf-1",
			Version = 42,
			WorkflowName = "OrderFulfillment",
		};

		started.EventType.ShouldBe("WorkflowStarted");
		started.Version.ShouldBe(42);
		started.WorkflowName.ShouldBe("OrderFulfillment");
		started.DefinitionVersion.ShouldBe(1);
	}
}
