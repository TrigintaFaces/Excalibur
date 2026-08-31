// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Security;

/// <summary>
/// What the monitor advertises it can detect has to match what it can actually raise. A consumer reads
/// this collection to decide which threats it still has to cover elsewhere, so an entry the monitor
/// never produces leaves a gap the consumer believes is closed.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecurityMonitorAdvertisedCapabilitiesShould
{
	private static SecurityMonitor BuildMonitor() =>
		new(
			Options.Create(new SecurityMonitoringOptions()),
			new ElasticsearchClient(),
			NullLogger<SecurityMonitor>.Instance);

	[Fact]
	public void AdvertiseOnlyTheThreatTypesItCanRaise()
	{
		var monitor = BuildMonitor();

		// The failed-login threshold is the only detection this monitor performs; it raises
		// ThreatType.UnauthorizedAccess and nothing else.
		monitor.SupportedThreatTypes.ShouldBe([ThreatType.UnauthorizedAccess]);
	}

	/// <summary>
	/// A criterion the caller sets must reach the query the monitor actually runs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The threat query was built from the time range alone. Every other criterion — the event types, the
	/// target systems — was settable, validated by nothing, and interpolated into a log line, so a caller
	/// asking for one event type on one system received every threat in the window instead. On a security
	/// surface that is a disclosure, not a missing convenience.
	/// </para>
	/// <para>
	/// The query builder is private, so this reads the constructed query rather than the source: the arm
	/// fails if the criteria stop reaching it, whatever the builder is refactored into.
	/// </para>
	/// </remarks>
	[Fact]
	public void NarrowTheThreatQueryByTheCriteriaTheCallerSet()
	{
		var request = new SecurityAlertRequest
		{
			StartTime = DateTimeOffset.UtcNow.AddHours(-1),
			EndTime = DateTimeOffset.UtcNow,
			Criteria = new SecurityAlertRequest.AlertCriteria
			{
				EventTypes = { "BruteForce" },
				TargetSystems = { "payments-api" },
			},
		};

		MustClauses(BuildThreatQuery(request)).Count.ShouldBe(
			3,
			"the time range plus both criteria the caller set must reach the query; a query carrying only "
			+ "the time range returns every threat in the window regardless of what was asked for");

		// Each criterion is proven to reach the query INDEPENDENTLY. The combined count above would still
		// pass if one criterion contributed two clauses and the other none, so the arms below pin each.
		MustClauses(BuildThreatQuery(WithCriteria(eventTypes: ["BruteForce"]))).Count.ShouldBe(
			2, "an event-type criterion on its own must add exactly one clause to the time range");
		MustClauses(BuildThreatQuery(WithCriteria(targetSystems: ["payments-api"]))).Count.ShouldBe(
			2, "a target-system criterion on its own must add exactly one clause to the time range");
	}

	private static SecurityAlertRequest WithCriteria(
		List<string>? eventTypes = null,
		List<string>? targetSystems = null) =>
		new()
		{
			StartTime = DateTimeOffset.UtcNow.AddHours(-1),
			EndTime = DateTimeOffset.UtcNow,
			Criteria = new SecurityAlertRequest.AlertCriteria
			{
				EventTypes = eventTypes ?? [],
				TargetSystems = targetSystems ?? [],
			},
		};

	/// <summary>
	/// LIVENESS: a request that sets no criteria must still search, not return an unsatisfiable query.
	/// </summary>
	[Fact]
	public void StillSearchTheTimeRangeWhenNoCriteriaAreSet()
	{
		var request = new SecurityAlertRequest
		{
			StartTime = DateTimeOffset.UtcNow.AddHours(-1),
			EndTime = DateTimeOffset.UtcNow,
		};

		var clauses = MustClauses(BuildThreatQuery(request));

		clauses.Count.ShouldBe(
			1,
			"with no criteria the query is the time range and nothing else; an empty criteria list must "
			+ "not become a terms clause that matches nothing");
	}

	/// <summary>Reads the must clauses the builder produced.</summary>
	private static List<Query> MustClauses(BoolQuery query) =>
		[.. query.Must.ShouldNotBeNull()];

	/// <summary>Invokes the private query builder, which is the artifact under test.</summary>
	private static BoolQuery BuildThreatQuery(SecurityAlertRequest request) =>
		(BoolQuery)typeof(SecurityMonitor)
			.GetMethod("BuildThreatQuery", BindingFlags.NonPublic | BindingFlags.Static)!
			.Invoke(null, [request])!;

	/// <summary>
	/// The alerting contract must not offer automated-response configuration: nothing in the package ever
	/// read a stored configuration back, so setting one could not change what happened on an alert.
	/// </summary>
	[Fact]
	public void NotOfferAutomatedResponseConfiguration()
	{
		var members = typeof(IElasticsearchSecurityAlerting)
			.GetMembers()
			.Select(static m => m.Name)
			.Where(static n => n.Contains("ConfigureAutomatedResponse", StringComparison.Ordinal))
			.ToList();

		members.ShouldBeEmpty();
	}
}
