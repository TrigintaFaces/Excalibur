// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Examples.ECommerceSample;

internal sealed class SqlOrderIdentityConflictException() : InvalidOperationException("This order identity has different business inputs.");

// Uses actual framework transaction capabilities; no application-owned replacement store or transaction engine.
internal sealed class SqlOrderSubmissionService
{
	private readonly ITransactionalInboxStore _inbox;
	private readonly ITransactionalOutboxWriter _outbox;
	private readonly Func<SqlConnection> _connections;

	public SqlOrderSubmissionService(IInboxStore inbox, IOutboxStore outbox, Func<SqlConnection> connections)
	{
		ArgumentNullException.ThrowIfNull(inbox);
		ArgumentNullException.ThrowIfNull(outbox);
		ArgumentNullException.ThrowIfNull(connections);
		if (inbox is not ITransactionalInboxStore transactional
			|| inbox is IInboxStoreCapabilities { SupportsTransactional: false })
		{
			throw new InvalidOperationException("SQL orders require an effective transactional inbox store; volatile fallback is refused.");
		}
		_inbox = transactional;
		_outbox = outbox.GetService(typeof(ITransactionalOutboxWriter)) as ITransactionalOutboxWriter
			?? throw new InvalidOperationException("SQL orders require a transactional outbox writer; volatile fallback is refused.");
		_connections = connections;
	}

	public async Task PrepareAsync(CancellationToken token)
	{
		await using var connection = _connections();
		await connection.OpenAsync(token).ConfigureAwait(false);
		_ = await new CreateCommerceOrders(token).ResolveAsync(connection).ConfigureAwait(false);
	}

	public async Task<OrderProcessingOutcome> ProcessAsync(OrderCreated input, CancellationToken token)
	{
		var plan = new OrderSubmissionPlan(input);
		if (plan.Id.Length > 128 || plan.IntentId.Length > 256)
		{
			throw new ArgumentException("SQL sample order identity exceeds the provisioned schema limits.", nameof(input));
		}
		var existingBusiness = false;
		try
		{
			var committed = await _inbox.TryProcessTransactionallyAsync(plan.Id, OrderProcessingService.HandlerType,
				async (transaction, cancellation) =>
				{
					var connection = transaction.Connection
						?? throw new InvalidOperationException("The inbox transaction has no active connection.");
					var prior = await new ReadCommerceOrder(plan.Id, transaction, cancellation).ResolveAsync(connection).ConfigureAwait(false);
					if (prior is not null)
					{
						if (!Matches(prior, plan)) { throw new SqlOrderIdentityConflictException(); }
						existingBusiness = true;
						return; // Retained immutable business evidence survives normal inbox retention.
					}
					var count = await new InsertCommerceOrder(plan, transaction, cancellation).ResolveAsync(connection).ConfigureAwait(false);
					if (count != 1) { throw new InvalidOperationException("The order insert did not apply exactly one row."); }
					await _outbox.StageMessageAsync(plan.CreateMessage(), transaction, cancellation).ConfigureAwait(false);
				}, token).ConfigureAwait(false);
			if (committed) { return existingBusiness ? OrderProcessingOutcome.Duplicate : OrderProcessingOutcome.Processed; }
			// Framework false skips the callback: it does not prove the submitted payload is the same.
			await using var readConnection = _connections();
			await readConnection.OpenAsync(token).ConfigureAwait(false);
			var evidence = await new ReadCommerceOrder(plan.Id, null, token).ResolveAsync(readConnection).ConfigureAwait(false);
			if (evidence is null) { return OrderProcessingOutcome.NeedsReconciliation; }
			return Matches(evidence, plan) ? OrderProcessingOutcome.Duplicate : OrderProcessingOutcome.Conflict;
		}
		catch (SqlOrderIdentityConflictException) { return OrderProcessingOutcome.Conflict; }
		// SQL/commit-ack/read failures propagate. A caller must not infer rollback or duplicate success.
	}

	internal async Task<SqlOrderEvidence?> ReadAsync(string id, CancellationToken token)
	{
		await using var connection = _connections();
		await connection.OpenAsync(token).ConfigureAwait(false);
		return await new ReadCommerceOrder(id, null, token).ResolveAsync(connection).ConfigureAwait(false);
	}

	private static bool Matches(SqlOrderEvidence evidence, OrderSubmissionPlan plan) =>
		evidence.IntentCommitted && string.Equals(evidence.Fingerprint, plan.Fingerprint, StringComparison.Ordinal)
		&& string.Equals(evidence.IntentId, plan.IntentId, StringComparison.Ordinal);
}
