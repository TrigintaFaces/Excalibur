// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Integration.Tests.DataElasticSearch.Security;

/// <summary>
/// Base for security-audit integration tests, which share one container and one global index pattern.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this class exists.</b> Every other Elasticsearch test isolates itself by naming its indices with
/// a unique <c>TestIndexPrefix</c>. These tests cannot: the subject under test —
/// <c>SecurityAuditMaintenanceService</c> — hardcodes the index pattern <c>security-audit-*</c>
/// (<c>SecurityAuditMaintenanceService.cs:234,332,550</c>), and <c>SecurityAuditor</c> writes to that same
/// family. Renaming the test's index would move the documents outside the pattern the service queries, so
/// the service would find nothing and the tests would pass without testing anything. The index names are
/// therefore left exactly as they are.
/// </para>
/// <para>
/// <b>How isolation is achieved instead.</b> Each fact used to get a brand-new container, so it began with
/// an empty <c>security-audit-*</c> namespace. This base reproduces that precondition exactly by deleting
/// <c>security-audit-*</c> before each fact runs. Tests execute sequentially
/// (<c>xunit.runner.json</c>: <c>parallelizeTestCollections: false</c>, <c>maxParallelThreads: 1</c>), so
/// an up-front delete is sufficient: no other fact can be writing while this one runs. The precondition is
/// inherited rather than copy-pasted, so a new audit test cannot forget it.
/// </para>
/// <para>
/// The delete is scoped to this collection's dedicated container (see
/// <see cref="Infrastructure.TestBaseClasses.ElasticsearchAuditTests"/>), so it cannot destroy data
/// belonging to any test outside this family.
/// </para>
/// </remarks>
public abstract class ElasticsearchAuditTestBase : ElasticsearchIntegrationTestBase
{
	/// <summary>
	/// The global index pattern the audit subsystem reads and writes, and which therefore has to be reset
	/// between facts.
	/// </summary>
	protected const string AuditIndexPattern = "security-audit-*";

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchAuditTestBase"/> class.
	/// </summary>
	/// <param name="fixture">The shared Elasticsearch container fixture for the audit collection.</param>
	protected ElasticsearchAuditTestBase(ElasticsearchContainerFixture fixture)
		: base(fixture)
	{
	}

	/// <summary>
	/// Clears the shared audit index pattern so each fact starts from the empty state a fresh container
	/// used to provide.
	/// </summary>
	protected override async Task InitializeTestEnvironmentAsync()
	{
		await ResetAuditIndicesAsync().ConfigureAwait(false);
		await base.InitializeTestEnvironmentAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Removes every audit document under <c>security-audit-*</c> on this collection's container.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This deletes DOCUMENTS, not indices, and that is deliberate.</b> Elasticsearch 8 and later
	/// default <c>action.destructive_requires_name</c> to <see langword="true"/>, which rejects a
	/// wildcard index delete. <c>Indices.DeleteAsync("security-audit-*")</c> therefore removes nothing
	/// and reports an invalid response rather than throwing — a reset written that way looks correct,
	/// silently does nothing, and shows up as audit counts that accumulate across facts (5 expected, 9
	/// observed; 7 expected, 16 observed). A delete-by-query is not a destructive index operation, so it
	/// is not subject to that setting.
	/// </para>
	/// <para>
	/// The response is checked rather than assumed: a reset that fails must fail loudly here, not as a
	/// confusing count mismatch inside whichever fact happens to run next.
	/// </para>
	/// </remarks>
	protected async Task ResetAuditIndicesAsync()
	{
		var response = await Client.DeleteByQueryAsync<SecurityAuditEvent>(d => d
			.Indices(AuditIndexPattern)
			.Query(new MatchAllQuery())
			.IgnoreUnavailable(true)
			.Refresh(true))
			.ConfigureAwait(false);

		// An absent index is the expected state for the first fact in the collection and is not a
		// failure; anything else is.
		if (!response.IsValidResponse && response.ElasticsearchServerError?.Status != 404)
		{
			throw new InvalidOperationException(
				$"Failed to reset '{AuditIndexPattern}' before this test: {response.DebugInformation}");
		}
	}
}
