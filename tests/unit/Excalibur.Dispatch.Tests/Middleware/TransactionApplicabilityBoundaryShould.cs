// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Middleware.Transaction;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

using DispatchTransactionOptions = Excalibur.Dispatch.Options.Middleware.TransactionOptions;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Binds the whole transactional boundary, not half of it: a transaction wraps Actions only, and the atomicity an
/// Event needs with the producer's state change comes from outbox staging instead. Widening TransactionMiddleware to
/// Events would enrol a subscriber's handler in the producer's transaction; a deliberate widening must be a visible
/// edit to this file rather than a silent property change.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransactionApplicabilityBoundaryShould
{
	private static readonly DefaultMiddlewareApplicabilityStrategy Strategy = new();

	private static TransactionMiddleware NewTransactionMiddleware() =>
		new(
			Microsoft.Extensions.Options.Options.Create(new DispatchTransactionOptions()),
			A.Fake<ITransactionService>(),
			NullLogger<TransactionMiddleware>.Instance);

	private static OutboxStagingMiddleware NewOutboxStagingMiddleware() =>
		new(
			Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions()),
			A.Fake<IOutboxStore>(),
			new DispatchJsonSerializer(),
			NullLogger<OutboxStagingMiddleware>.Instance);

	// SAFETY: resolved the way the pipeline resolves it -- through the applicability strategy reading the real
	// ApplicableMessageKinds property, never by reflecting over [AppliesTo]. An Event does not join the producer's
	// transaction; an Action does.
	[Fact]
	public void ApplyATransactionToActionsAndNotToEvents()
	{
		var middleware = NewTransactionMiddleware();

		Strategy.ShouldApplyMiddleware(middleware.ApplicableMessageKinds, MessageKinds.Event)
			.ShouldBeFalse("an event handler must not be enrolled in the producer's transaction");
		Strategy.ShouldApplyMiddleware(middleware.ApplicableMessageKinds, MessageKinds.Action)
			.ShouldBeTrue("a command that modifies state is exactly what the transaction wraps");
	}

	// LIVENESS: events are not left without an atomicity story. Outbox staging applies to events and stages them
	// inside the producer's transaction. Without this arm the safety arm above reads as "events get no atomicity".
	[Fact]
	public void StageAnEventThroughTheOutboxSoItStillCommitsWithTheProducer()
	{
		var middleware = NewOutboxStagingMiddleware();

		Strategy.ShouldApplyMiddleware(middleware.ApplicableMessageKinds, MessageKinds.Event)
			.ShouldBeTrue("an event's atomicity with the producer's state change is provided by outbox staging");
	}
}
