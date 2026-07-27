// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Binds the tenant term on the transport-delivery <b>read</b> path: the caller's partition must reach the
/// emitted predicate, and the predicate must be evaluated by the engine rather than applied to returned rows.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect being locked.</b> The read selected by <c>MessageId</c> alone. A caller scoped to one tenant
/// who supplied another tenant's message id received that tenant's <c>Destination</c> and
/// <c>TransportMetadata</c> — where their data was sent, and with what routing metadata. Message ids are the
/// store's own identifiers, so this needed no privilege beyond knowing one.
/// </para>
/// <para>
/// <b>Why the assertions bind the SQL and the bound value, not the returned rows.</b> A test that filtered
/// results in memory would pass against a store that fetched every tenant's rows and discarded the others —
/// the disclosure would already have crossed the process boundary. The requirement is that the engine never
/// returns them, so the arms assert the term is <i>in the statement</i> and that the caller's partition is
/// the value bound to it.
/// </para>
/// <para>
/// <b>Scope, stated rather than implied.</b> These are unit arms over the emitted command. They prove the
/// predicate is present and carries the caller's partition; they do <b>not</b> execute SQL, so they cannot
/// prove the engine's evaluation. A real-infrastructure arm — two tenants, one message id, against a live
/// SQL Server — is the stronger lock and is not this file.
/// </para>
/// <para>
/// <b>Authorship.</b> Written by the implementer of the fix, which is weaker than an independent author and
/// is disclosed for that reason: the arms can only catch defects their author imagined. The sibling write-path
/// suite was authored independently precisely because the implementer's own arms missed a tenant-ignoring
/// implementation. These deserve the same adversarial read.
/// </para>
/// </remarks>
public sealed class TransportDeliveryReadTenantTermShould
{
	private const string TableName = "[dbo].[OutboxMessageTransports]";
	private const string MessageId = "msg-1";

	/// <summary>
	/// SAFETY — the emitted statement carries a tenant term, so the engine excludes other tenants' rows.
	/// </summary>
	[Fact]
	public void ConstrainTheReadWithATenantTermInTheStatement()
	{
		// Act
		var request = new GetTransportDeliveriesRequest(
			TableName, MessageId, KeyedTenantPartition.Scoped("tenant-a"), 30, CancellationToken.None);

		// Assert — the term is in the SQL, so the database applies it.
		request.Command.CommandText.ShouldContain(
			"TenantId",
			Case.Sensitive,
			"the read must constrain on the tenant column; selecting by MessageId alone returns another tenant's rows.");
		request.Command.CommandText.ShouldContain(
			"@TenantId",
			Case.Sensitive,
			"the tenant must be a bound parameter evaluated by the engine, not a value filtered after the rows are returned.");
	}

	/// <summary>
	/// SAFETY — the bound value is the caller's partition, not a constant.
	/// </summary>
	/// <remarks>
	/// The arm above is satisfied by a request that names the column and binds a hard-coded value. This one
	/// distinguishes "a tenant term is present" from "the CALLER'S tenant term is present" — the difference
	/// between a predicate and a decoration.
	/// </remarks>
	[Fact]
	public void BindTheCallersOwnPartitionRatherThanAConstant()
	{
		// Act
		var request = new GetTransportDeliveriesRequest(
			TableName, MessageId, KeyedTenantPartition.Scoped("tenant-b"), 30, CancellationToken.None);

		// Assert
		((DynamicParameters)request.Command.Parameters).Get<string>("TenantId")
			.ShouldBe("tenant-b", "the read must bind the partition it was given; binding anything else scopes the query to the wrong tenant.");
	}

	/// <summary>
	/// LIVENESS — a caller with no ambient tenant still reads the untenanted partition.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The safety arms above are both satisfied by a predicate that matches nothing at all. A single-tenant
	/// host supplies no ambient tenant and must still receive its own rows: untenanted is a named partition,
	/// not an absent one, and a read that returned nothing to it would be "safe" and useless.
	/// </para>
	/// <para>
	/// "No ambient tenant" is stated rather than "unscoped" deliberately: that word names two different
	/// things in this codebase. A <c>TenantScope</c> can be genuinely unscoped and emit no tenant term at
	/// all; a <see cref="KeyedTenantPartition"/> has no such inhabitant — its untenanted case still binds the
	/// reserved sentinel. This arm exercises the second. The caller is unscoped; the partition it resolves to
	/// is not.
	/// </para>
	/// </remarks>
	[Fact]
	public void StillReadTheUntenantedPartitionForACallerWithNoAmbientTenant()
	{
		// Act
		var request = new GetTransportDeliveriesRequest(
			TableName, MessageId, KeyedTenantPartition.Untenanted, 30, CancellationToken.None);

		// Assert
		((DynamicParameters)request.Command.Parameters).Get<string>("TenantId")
			.ShouldBe("__untenanted__", "a caller with no ambient tenant must read the reserved partition, not be excluded from every row.");
	}

	/// <summary>
	/// LIVENESS — rows predating the tenant column remain reachable by their owner.
	/// </summary>
	/// <remarks>
	/// Transport rows written before the table carried a tenant column hold NULL. A bare
	/// <c>TenantId = @TenantId</c> is never true for them, so they would become invisible to every tenant —
	/// safe, and a silent data-availability loss. The COALESCE keeps them attributable to the untenanted
	/// partition instead of to nobody.
	/// </remarks>
	[Fact]
	public void KeepPreColumnRowsReachableRatherThanHidingThemFromEveryone()
	{
		// Act
		var request = new GetTransportDeliveriesRequest(
			TableName, MessageId, KeyedTenantPartition.Untenanted, 30, CancellationToken.None);

		// Assert
		request.Command.CommandText.ShouldContain(
			"COALESCE",
			Case.Sensitive,
			"legacy NULL tenant rows must resolve to the untenanted partition; a bare equality hides them from every tenant including their owner.");
		((DynamicParameters)request.Command.Parameters).Get<string>("UntenantedSentinel")
			.ShouldBe("__untenanted__", "the COALESCE fallback must be the reserved sentinel so legacy rows land in the partition the store writes today.");
	}
}
