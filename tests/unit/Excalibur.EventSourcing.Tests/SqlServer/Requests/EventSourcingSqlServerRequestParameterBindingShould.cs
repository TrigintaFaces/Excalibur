// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.SqlServer.Requests;

using Tests.Shared.Helpers;

namespace Excalibur.EventSourcing.Tests.SqlServer.Requests;

/// <summary>
/// Holds every SQL Server event-sourcing request to one property: the parameters its SQL names and the
/// parameters it binds are the same set, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is generic rather than per-request.</b> The defect class it catches is not specific to any
/// one statement — it is what happens when a <c>WHERE</c> fragment and a <c>parameters.Add</c> are edited
/// separately (bd-4yiblk: <c>MarkMessageFailedRequest</c> bound a tenant parameter inside an
/// <c>else if</c> branch while the SQL referenced it on every path). The pre-existing tests in
/// <c>SqlServerRequestsShould</c> assert command SHAPE (the text, the timeout, the table name) and would
/// report green against a binary carrying exactly that defect, because constructing a request never
/// executes it. A per-request lock has to be remembered once per request; enumerating the family here
/// means a new request is covered the moment it is added to the list below, and a request that drifts is
/// caught without anyone deciding to look. This is the same recipe as
/// <c>Excalibur.Outbox.Tests.SqlServer.Requests.OutboxRequestParameterBindingShould</c>, applied to the
/// event-sourcing request family and using the promoted <see cref="SqlParameterTokens"/> helper.
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
[Trait("Component", "EventSourcing")]
public sealed class EventSourcingSqlServerRequestParameterBindingShould
{
	private static readonly CancellationToken Ct = CancellationToken.None;
	private const string AggregateId = "agg-1";
	private const string AggregateType = "OrderAggregate";

	private static ISnapshot FakeSnapshot()
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns("snap-1");
		A.CallTo(() => snapshot.AggregateId).Returns(AggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns(AggregateType);
		A.CallTo(() => snapshot.Version).Returns(5);
		A.CallTo(() => snapshot.Data).Returns(new byte[] { 1, 2, 3 });
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);
		return snapshot;
	}

	private static EventInsertRow Row() =>
		new("e1", AggregateId, AggregateType, "Created", [1], null, 0, DateTimeOffset.UnixEpoch);

	/// <summary>
	/// Every request the package emits. A new request type belongs here; a missing one is invisible to this
	/// lock, which is why <see cref="CoverEveryRequestTypeThePackageDeclares"/> asserts the list rather than
	/// trusting it.
	/// </summary>
	private static readonly Dictionary<string, Func<CommandDefinition>> Builders = new(StringComparer.Ordinal)
	{
		["DeleteEventsUpToVersion"] = () => new DeleteEventsUpToVersionRequest(KeyedTenantPartition.Scoped("tenant-1"), AggregateId, AggregateType, 5, Ct).Command,
		["DeleteSnapshots"] = () => new DeleteSnapshotsRequest(AggregateId, AggregateType, TenantScope.Untenanted, Ct).Command,
		["DeleteSnapshotsOlderThan"] = () => new DeleteSnapshotsOlderThanRequest(AggregateId, AggregateType, 5, TenantScope.Untenanted, Ct).Command,
		["EraseEvents"] = () => new EraseEventsRequest(AggregateId, AggregateType, Guid.NewGuid(), TenantScope.Untenanted, Ct).Command,
		["GetArchiveCandidates"] = () => new GetArchiveCandidatesRequest(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) }, 100, DateTimeOffset.UtcNow, Ct).Command,
		["GetCurrentVersion"] = () => new GetCurrentVersionRequest(AggregateId, AggregateType, null, TenantScope.Scoped("tenant-1"), Ct).Command,
		["GetLatestSnapshot"] = () => new GetLatestSnapshotRequest(AggregateId, AggregateType, TenantScope.Untenanted, Ct).Command,
		["InsertEventsBatch"] = () => new InsertEventsBatchRequest([Row()], null, TenantScope.Untenanted, Ct).Command,
		["IsErased"] = () => new IsErasedRequest(AggregateId, AggregateType, TenantScope.Untenanted, Ct).Command,
		["LoadEvents"] = () => new LoadEventsRequest(AggregateId, AggregateType, -1, TenantScope.Scoped("tenant-1"), Ct).Command,
		["SaveSnapshot"] = () => new SaveSnapshotRequest(FakeSnapshot(), TenantScope.Untenanted, Ct).Command,
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
			Broken("UPDATE t SET a = 1 WHERE AggregateId = @AggregateId AND TenantId = @TenantId", "@AggregateId"));

		unbound.ShouldBe(["TenantId"], "the liveness arm must fail when a named parameter is not carried");
	}

	/// <summary>
	/// NON-VACUITY for the safety arm: a predicate deleted from the SQL with its binding left behind. It
	/// never fails at the server, so only this arm reports it.
	/// </summary>
	[Fact]
	public void DetectAParameterTheCommandCarriesButTheSqlNeverNames()
	{
		var (_, orphaned) = Compare(
			"deliberately broken",
			Broken("UPDATE t SET a = 1 WHERE AggregateId = @AggregateId", "@AggregateId", "@TenantId"));

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
		var declared = typeof(LoadEventsRequest).Assembly
			.GetTypes()
			.Where(static t => t is { IsClass: true, IsAbstract: false }
				&& t.Namespace == typeof(LoadEventsRequest).Namespace
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
