// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;

public sealed partial class InMemoryOrderRepository
{
	private readonly ConcurrentDictionary<string, OrderSubmission> _submissions = new(StringComparer.Ordinal);

	internal OrderSubmission Capture(OrderSubmissionPlan plan) => _submissions.GetOrAdd(plan.Id,
		new OrderSubmission(plan, PublicationState.Ready, 0, Guid.Empty, 0, null));

	internal OrderSubmission? Submission(string id) => _submissions.GetValueOrDefault(id);

	internal bool BeginPublication(OrderSubmission expected, out OrderSubmission permit)
	{
		permit = expected;
		if (expected.Publication is not (PublicationState.Ready or PublicationState.RetryableUncertain)
			|| expected.StageInvocations >= 2) { return false; }
		permit = expected with { Publication = PublicationState.InFlight, Version = checked(expected.Version + 1),
			Operation = Guid.NewGuid(), StageInvocations = expected.StageInvocations + 1 };
		return _submissions.TryUpdate(expected.Plan.Id, permit, expected);
	}

	internal bool RecordPublication(OrderSubmission permit, bool accepted, string? error)
	{
		if (permit.Publication != PublicationState.InFlight) { return false; }
		var state = accepted ? PublicationState.Accepted : permit.StageInvocations >= 2
			? PublicationState.NeedsReconciliation : PublicationState.RetryableUncertain;
		var next = permit with { Publication = state, Version = checked(permit.Version + 1), LastError = error };
		return _submissions.TryUpdate(permit.Plan.Id, next, permit);
	}

	private OrderRecord[] SubmissionOrders() => _submissions.Values.Select(static state => state.Plan.Order with
	{
		Status = state.Publication == PublicationState.Accepted ? OrderProcessingService.ConfirmedStatus : "PendingConfirmation",
	}).ToArray();
}
