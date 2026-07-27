// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Workflows;

namespace Excalibur.Workflows.Abstractions.Tests;

/// <summary>
/// Smoke coverage for the workflow journal event seam: the concrete entry type loads, exposes its
/// discriminator via the abstract override, and round-trips the replay cursor.
/// </summary>
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
