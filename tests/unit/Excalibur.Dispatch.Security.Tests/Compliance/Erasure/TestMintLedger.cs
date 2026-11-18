// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using global::Excalibur.Compliance;
using global::Excalibur.Dispatch;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Mint ledgers for arms that are not about the mint ledger.
/// </summary>
/// <remarks>
/// <para>
/// The erasure service takes its mint ledger as a REQUIRED dependency, so an arm about something else
/// still has to say which handles were minted. <see cref="None"/> is how it says "none" — a real ledger
/// holding no rows, which is a VALUE rather than a stub.
/// </para>
/// <para>
/// <b>Why an EMPTY enumeration is legal here and catastrophic in production.</b> The production
/// no-erasure ledger THROWS on enumeration rather than returning empty, because there an empty list is
/// indistinguishable from "this subject has no handles" and would let an erasure destroy only what it can
/// derive and then certify. That reasoning is about a ledger that DISCARDS rows it was given. This one
/// keeps every row it is given and returns exactly those — so for a subject nothing ever minted a handle
/// for, empty is the true answer, and it is the same answer the shipped in-memory store gives (its
/// conformance kit binds that arm by name). An arm that drives the real write path gets the real store
/// instead.
/// </para>
/// <para>
/// <b>It implements the two-state row faithfully, including the part that is inconvenient.</b> A row
/// recorded and not confirmed is PENDING, and an erasure that sees a pending row refuses to certify. A
/// fake that marked rows minted on insert would make every arm pass and would hide exactly the state the
/// contract added the state for.
/// </para>
/// </remarks>
internal sealed class TestMintLedger : IKeyMintLedger
{
	private readonly ConcurrentDictionary<string, Row> _rows = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets a real ledger holding no rows.
	/// </summary>
	/// <value>
	/// An empty ledger: every subject enumerates to no handles, so the erasure destroys only what it
	/// derives — which is the behaviour these arms were written against.
	/// </value>
	public static TestMintLedger None { get; } = new();

	/// <inheritdoc />
	/// <remarks>
	/// TRUE, so that an arm about something else is not refused at startup for a reason it is not about.
	/// Rows live for the lifetime of this instance and every arm builds its own, so within one arm the
	/// rows behave exactly as durable ones do. An arm that is ABOUT volatility uses the real in-memory
	/// store, which answers FALSE.
	/// </remarks>
	public bool IsDurable => true;

	/// <inheritdoc />
	public Task RecordMintIntentAsync(
		string keyHandle,
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyHandle);
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);
		cancellationToken.ThrowIfCancellationRequested();

		// Minted left false: the key does not exist yet and this row must not claim it does.
		_ = _rows.TryAdd(keyHandle, new Row(tenantId.Value, dataSubjectIdHash, Minted: false));

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task MarkMintedAsync(string keyHandle, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyHandle);
		cancellationToken.ThrowIfCancellationRequested();

		// Refused when absent, matching the shipped stores: the row is written before the key, so its
		// absence means the intent step was skipped.
		if (!_rows.TryGetValue(keyHandle, out var existing))
		{
			throw new InvalidOperationException(
				$"No mint-ledger row exists for key handle '{keyHandle}', so its key creation cannot be "
				+ "confirmed.");
		}

		_ = _rows.TryUpdate(keyHandle, existing with { Minted = true }, existing);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<MintLedgerSnapshot> ListHandlesAsync(
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);
		cancellationToken.ThrowIfCancellationRequested();

		// Ordinal on BOTH terms, matching the shipped stores. A case-insensitive comparison here would let
		// an arm pass against a store that cannot ship.
		var rows = _rows
			.Where(row =>
				string.Equals(row.Value.TenantId, tenantId.Value, StringComparison.Ordinal)
				&& string.Equals(row.Value.DataSubjectIdHash, dataSubjectIdHash, StringComparison.Ordinal))
			.Select(row => (row.Key, row.Value.Minted))
			.ToList();

		IReadOnlyList<string> minted = [.. rows.Where(static r => r.Minted).Select(static r => r.Key)];
		IReadOnlyList<string> pending = [.. rows.Where(static r => !r.Minted).Select(static r => r.Key)];

		return Task.FromResult(new MintLedgerSnapshot(minted, pending));
	}

	private sealed record Row(string TenantId, string DataSubjectIdHash, bool Minted);
}
