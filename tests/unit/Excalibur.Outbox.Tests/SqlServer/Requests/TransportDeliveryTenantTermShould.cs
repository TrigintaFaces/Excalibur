// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Binds the tenant term on the transport-delivery write path: the caller's tenant must reach the emitted
/// SQL, and the untenanted case must be the named sentinel rather than an absent column.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists separately from <c>InsertTransportDeliveryRequestShould</c>.</b> That suite was
/// updated alongside the fix to pass a tenant argument so its calls compile against the widened constructor
/// — ten call sites, all inside argument-guard tests, and <b>zero assertions naming a tenant</b>. A
/// signature-following change is not coverage: it makes the suite compile, it does not make the suite
/// notice. The defect could therefore be reintroduced and every existing arm would stay green. These arms
/// are authored independently of the implementer for that reason.
/// </para>
/// <para>
/// <b>The defect being locked.</b> The shipped schema declares
/// <c>TenantId … NOT NULL DEFAULT '__untenanted__'</c>. A write that never mentions the column therefore
/// produces a row that is <i>indistinguishable</i> from a write that deliberately chose the untenanted
/// partition — the engine supplies the same value either way. That is exactly why the split landed
/// silently: nothing downstream can tell "no tenant was supplied" from "the untenanted partition was
/// selected". An arm that merely asserts a row exists, or that the statement executes, passes over it.
/// The only place the difference is observable is the <b>parameter set the request emits</b>, which is
/// what these arms read.
/// </para>
/// <para>
/// <b>Both arms are required.</b> Safety alone ("no other tenant's term appears") is satisfied by a
/// request that emits no tenant parameter at all — the original defect. Liveness alone ("something is
/// emitted") is satisfied by a request that hardcodes the sentinel for every caller, which would silently
/// collapse every tenant into one partition. Neither arm detects its own inverse.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportDeliveryTenantTermShould : UnitTestBase
{
	private const string TestTableName = "[dbo].[OutboxMessageTransports]";

	private static OutboundMessageTransport CreateTestDelivery() => new()
	{
		Id = Guid.NewGuid().ToString(),
		MessageId = "msg-123",
		TransportName = "kafka",
		Destination = "orders-topic",
		Status = TransportDeliveryStatus.Pending,
		CreatedAt = DateTimeOffset.UtcNow,
		RetryCount = 0
	};

	/// <summary>Reads the tenant term the request will actually send to the database.</summary>
	private static string? EmittedTenantTerm(InsertTransportDeliveryRequest request) =>
		request.Command.Parameters is DynamicParameters parameters
			? parameters.Get<string>("@TenantId")
			: throw new InvalidOperationException(
				"Expected the request to emit DynamicParameters. If the parameter mechanism has changed, this "
				+ "arm must be updated to read the new one — it must never be relaxed to stop reading the "
				+ "tenant term, because reading that term is the entire point of this file.");

	/// <summary>
	/// SAFETY: the caller's tenant must reach the emitted parameters, not the schema default.
	/// </summary>
	/// <remarks>
	/// RED against the pre-fix request, which bound no tenant parameter at all: every transport row then took
	/// <c>DEFAULT '__untenanted__'</c> from the schema while the caller believed it had written a tenant.
	/// </remarks>
	[Fact]
	public void CarryTheCallersTenantIntoTheEmittedParameters()
	{
		var request = new InsertTransportDeliveryRequest(
			TestTableName, CreateTestDelivery(), "tenant-a", null, 30, CancellationToken.None);

		var emitted = EmittedTenantTerm(request);

		emitted.ShouldBe(
			"tenant-a",
			"the tenant the caller named must be the tenant that is persisted. If this is the untenanted "
			+ "sentinel, the write silently reassigned the row to the shared partition; if it is null, the "
			+ "column falls to its schema DEFAULT and the row is untenanted with no trace that a tenant was "
			+ "ever supplied.");

		request.Command.CommandText.ShouldContain(
			"TenantId",
			Case.Sensitive,
			"the INSERT must name the tenant column — a parameter that no statement references is inert.");
		request.Command.CommandText.ShouldContain(
			"@TenantId",
			Case.Sensitive,
			"the INSERT must bind the tenant parameter, not merely list the column.");
	}

	// LIVENESS — the unscoped case (an absent tenant must bind the reserved sentinel, never NULL) is
	// covered by InsertTransportDeliveryRequestShould.BindTheReservedSentinelWhenTheCallerSuppliesNoTenant,
	// authored by the implementer alongside the fix. It is NOT duplicated here.
	//
	// That arm is necessary and it is not sufficient, which is why this file exists: it is satisfied by a
	// request that IGNORES its tenant argument entirely and binds the sentinel unconditionally. Such a
	// request would pass every existing arm while collapsing every tenant's transport rows into the shared
	// partition — a regression that is arguably worse than the original defect, because the original at
	// least failed uniformly rather than silently merging tenants. The two arms below are what notice it.

	/// <summary>
	/// The two cases must be DISTINGUISHABLE. A request that collapses them is the defect wearing a fix.
	/// </summary>
	/// <remarks>
	/// This is the arm that survives a well-intentioned "simplification". If someone later routes every
	/// caller through <c>KeyedTenantPartition.Untenanted</c> — which would satisfy both arms above
	/// individually — every tenant's transport rows land in one partition and the two arms above cannot
	/// tell. Only comparing the two emissions catches it.
	/// </remarks>
	[Fact]
	public void DistinguishATenantedWriteFromAnUntenantedOne()
	{
		var tenanted = new InsertTransportDeliveryRequest(
			TestTableName, CreateTestDelivery(), "tenant-a", null, 30, CancellationToken.None);
		var untenanted = new InsertTransportDeliveryRequest(
			TestTableName, CreateTestDelivery(), null, null, 30, CancellationToken.None);

		EmittedTenantTerm(tenanted).ShouldNotBe(
			EmittedTenantTerm(untenanted),
			"a tenanted write and an unscoped write must not persist the same tenant term. If they match, "
			+ "every tenant has been collapsed into the shared partition and both the safety and liveness "
			+ "arms above would still pass.");
	}
}
