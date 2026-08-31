// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer.Requests;

using Tests.Shared.Helpers;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Holds every SQL Server outbox request to one property: the parameters its SQL names and the parameters it
/// binds are the same set, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is generic rather than per-request.</b> The defect class it catches is not specific to any one
/// statement — it is what happens when a <c>WHERE</c> fragment and a <c>parameters.Add</c> are edited
/// separately. Pre-existing tests over these requests assert command SHAPE (the text, the timeout, the
/// resolver, the tenancy disposition) and reported green against a binary carrying exactly this defect,
/// because constructing a request never executes it. A per-request lock has to be remembered once per
/// request; enumerating the family here means a new request is covered the moment it is added to the list
/// below, and a request that drifts is caught without anyone deciding to look.
/// </para>
/// <para>
/// <b>Both directions, because neither implies the other.</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Referenced but not bound</b> — the statement fails at the server. This is the residue of ADDING a
/// predicate whose binding was forgotten, or of adding one inside a conditional branch so only some paths
/// carry it.
/// </description></item>
/// <item><description>
/// <b>Bound but not referenced</b> — the value is inert. This is the residue of DELETING a predicate and
/// leaving its <c>parameters.Add</c> behind. It never fails, so nothing surfaces it: the parameter sits
/// there implying a constraint the statement no longer applies, which is how a stale scoping claim outlives
/// the scoping itself.
/// </description></item>
/// </list>
/// <para>
/// The arms read the emitted command, not the request's declared disposition and not its source. A statement
/// is what the server receives.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxRequestParameterBindingShould : UnitTestBase
{
	private const string OutboxTable = "[dbo].[OutboxMessages]";
	private const string TransportsTable = "[dbo].[OutboxMessageTransports]";
	private const string FenceTable = "[dbo].[OutboxFence]";
	private const string MessageId = "msg-1";
	private const string TransportName = "kafka";
	private const int Timeout = 30;

	private static OutboundMessage Message() => new()
	{
		Id = MessageId,
		MessageType = "T",
		Payload = [1],
		Destination = "dest",
		TenantId = "tenant-a",
	};

	private static OutboundMessageTransport Delivery() => new()
	{
		Id = "delivery-1",
		MessageId = MessageId,
		TransportName = TransportName,
		Destination = "orders-topic",
		Status = TransportDeliveryStatus.Pending,
		CreatedAt = DateTimeOffset.UtcNow,
		RetryCount = 0,
	};

	/// <summary>
	/// Every request the package emits. A new request type belongs here; a missing one is invisible to this
	/// lock, which is why <see cref="CoverEveryRequestTypeThePackageDeclares"/> asserts the list rather than
	/// trusting it.
	/// </summary>
	private static readonly Dictionary<string, Func<CommandDefinition>> Builders = new(StringComparer.Ordinal)
	{
		["CleanupSentMessages"] = () => new CleanupSentMessagesRequest(OutboxTable, DateTimeOffset.UtcNow, 100, null, Timeout, CancellationToken.None).Command,
		["CleanupTransportDeliveries"] = () => new CleanupTransportDeliveriesRequest(OutboxTable, TransportsTable, DateTimeOffset.UtcNow, 100, null, Timeout, CancellationToken.None).Command,
		["EnforceOutboxFence"] = () => new EnforceOutboxFenceRequest(FenceTable, OutboxTable, 7L, Timeout, null, CancellationToken.None).Command,
		["GetFailedMessages"] = () => new GetFailedMessagesRequest(OutboxTable, 5, DateTimeOffset.UtcNow, 100, Timeout, CancellationToken.None).Command,
		["GetOutboxStatistics"] = () => new GetOutboxStatisticsRequest(OutboxTable, Timeout, CancellationToken.None).Command,
		["GetScheduledMessages"] = () => new GetScheduledMessagesRequest(OutboxTable, DateTimeOffset.UtcNow, 100, Timeout, CancellationToken.None).Command,
		["GetTransportDeliveries"] = () => new GetTransportDeliveriesRequest(TransportsTable, MessageId, Timeout, CancellationToken.None).Command,
		["GetTenantScopedTransportDeliveries"] = () => new GetTenantScopedTransportDeliveriesRequest(TransportsTable, MessageId, KeyedTenantPartition.Scoped("tenant-a"), Timeout, CancellationToken.None).Command,
		["GetUnsentMessages"] = () => new GetUnsentMessagesRequest(OutboxTable, 100, Timeout, 300, "proc-1", null, FenceTable, OutboxTable, CancellationToken.None).Command,
		["InsertOutboxMessage"] = () => new InsertOutboxMessageRequest(OutboxTable, Message(), null, Timeout, CancellationToken.None).Command,
		["InsertTransportDelivery"] = () => new InsertTransportDeliveryRequest(TransportsTable, Delivery(), "tenant-a", null, Timeout, CancellationToken.None).Command,
		["MarkMessageDeadLettered"] = () => new MarkMessageDeadLetteredRequest(OutboxTable, MessageId, "reason", Timeout, CancellationToken.None).Command,
		["MarkBatchFailed"] = () => new MarkBatchFailedRequest(OutboxTable, [MessageId], "boom", 1, "proc-1", 30, Timeout, CancellationToken.None).Command,
		["MarkMessageFailed"] = () => new MarkMessageFailedRequest(OutboxTable, MessageId, "boom", 1, "proc-1", Timeout, CancellationToken.None).Command,
		["MarkMessageSent"] = () => new MarkMessageSentRequest(OutboxTable, MessageId, Timeout, null, FenceTable, OutboxTable, CancellationToken.None).Command,
		["MarkTransportFailed"] = () => new MarkTransportFailedRequest(TransportsTable, MessageId, TransportName, "boom", Timeout, CancellationToken.None).Command,
		["MarkTransportSent"] = () => new MarkTransportSentRequest(TransportsTable, MessageId, TransportName, Timeout, CancellationToken.None).Command,
		["MarkTransportSkipped"] = () => new MarkTransportSkippedRequest(TransportsTable, MessageId, TransportName, "skip", Timeout, CancellationToken.None).Command,
		["UpdateAggregateStatus"] = () => new UpdateAggregateStatusRequest(OutboxTable, TransportsTable, MessageId, Timeout, CancellationToken.None).Command,
	};

	/// <summary>Names the requests under test — a serializable key so each row enumerates on its own.</summary>
	public static TheoryData<string> EveryRequest() => [.. Builders.Keys];

	/// <summary>
	/// LIVENESS. Every parameter the statement names is carried, so the statement can execute.
	/// </summary>
	/// <param name="name">The request under test.</param>
	[Theory]
	[MemberData(nameof(EveryRequest))]
	public void BindEveryParameterItsSqlReferences(string name)
	{
		var (unbound, _) = Compare(name, Builders[name]());

		unbound.ShouldBeEmpty(
			$"{name} emits SQL referencing {SqlParameterTokens.Format(unbound)} which the command does not "
			+ "carry, so the statement fails at the server");
	}

	/// <summary>
	/// SAFETY. Every parameter the command carries is named by the statement, so no binding outlives the
	/// predicate it was added for.
	/// </summary>
	/// <param name="name">The request under test.</param>
	[Theory]
	[MemberData(nameof(EveryRequest))]
	public void ReferenceEveryParameterItBinds(string name)
	{
		var (_, orphaned) = Compare(name, Builders[name]());

		orphaned.ShouldBeEmpty(
			$"{name} binds {SqlParameterTokens.Format(orphaned)} which its SQL never names. A predicate was "
			+ "removed and its binding was not, so the value is inert and implies a constraint that is gone");
	}

	/// <summary>
	/// The comparison both arms above make. Shared so the non-vacuity arms below exercise the SAME code the
	/// real arms do, rather than a re-implementation that could drift into agreeing with everything.
	/// </summary>
	private static (List<string> Unbound, List<string> Orphaned) Compare(string name, CommandDefinition command)
	{
		var parameters = command.Parameters as DynamicParameters;
		_ = parameters.ShouldNotBeNull($"{name} must supply Dapper parameters for this lock to read");

		var referenced = SqlParameterTokens.ReferencedBy(command.CommandText);
		var bound = SqlParameterTokens.BoundBy(parameters);

		return (
			referenced.Except(bound, StringComparer.OrdinalIgnoreCase).ToList(),
			bound.Except(referenced, StringComparer.OrdinalIgnoreCase).ToList());
	}

	private static CommandDefinition Broken(string sql, params string[] boundNames)
	{
		var parameters = new DynamicParameters();
		foreach (var boundName in boundNames)
		{
			parameters.Add(boundName, "x");
		}

		return new CommandDefinition(sql, parameters);
	}

	/// <summary>
	/// NON-VACUITY for the liveness arm. Every request above passes it, so on its own that arm is
	/// indistinguishable from one that cannot fail. This proves it fires on the defect it exists to catch.
	/// </summary>
	[Fact]
	public void DetectAParameterTheSqlNamesButTheCommandDoesNotCarry()
	{
		var (unbound, _) = Compare(
			"deliberately broken",
			Broken("UPDATE t SET a = 1 WHERE Id = @MessageId AND TenantId = @TenantId", "@MessageId"));

		unbound.ShouldBe(["TenantId"], "the liveness arm must fail when a named parameter is not carried");
	}

	/// <summary>
	/// NON-VACUITY for the safety arm, and the shape this bead actually produced: a predicate deleted from
	/// the SQL with its binding left behind. It never fails at the server, so only this arm reports it.
	/// </summary>
	[Fact]
	public void DetectAParameterTheCommandCarriesButTheSqlNeverNames()
	{
		var (_, orphaned) = Compare(
			"deliberately broken",
			Broken("UPDATE t SET a = 1 WHERE Id = @MessageId", "@MessageId", "@TenantId"));

		orphaned.ShouldBe(["TenantId"], "the safety arm must fail when a binding outlives its predicate");
	}

	/// <summary>
	/// The enumeration above is this lock's only blind spot, so it is asserted rather than trusted: every
	/// request type the package declares must appear in it. A request added without a row would be silently
	/// uncovered, and nothing else in the suite would notice.
	/// </summary>
	[Fact]
	public void CoverEveryRequestTypeThePackageDeclares()
	{
		var declared = typeof(MarkMessageSentRequest).Assembly
			.GetTypes()
			.Where(static t => t is { IsClass: true, IsAbstract: false }
				&& t.Namespace == typeof(MarkMessageSentRequest).Namespace
				&& t.Name.EndsWith("Request", StringComparison.Ordinal))
			.Select(static t => t.Name[..^"Request".Length])
			.ToHashSet(StringComparer.Ordinal);

		var covered = Builders.Keys.ToHashSet(StringComparer.Ordinal);

		declared.ShouldNotBeEmpty("the reflection query must find the request family, or this arm is vacuous");

		var uncovered = declared.Except(covered, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
		uncovered.ShouldBeEmpty(
			"these request types are declared but absent from the builder table, so the binding property is not "
			+ $"checked for them: {string.Join(", ", uncovered)}");
	}
}
