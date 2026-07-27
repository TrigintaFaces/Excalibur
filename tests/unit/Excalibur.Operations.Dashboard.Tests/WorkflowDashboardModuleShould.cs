// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Excalibur.Workflows;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests;

/// <summary>
/// Endpoint lock for the durable-workflow dashboard subsystem (ut3nd6, W4-L15): the read module is wired
/// into <c>MapDashboardApi</c>, resolves <see cref="IWorkflowStoreAdmin"/> optionally, fails open when the
/// admin surface is absent, and surfaces a paged instance list with the owning tenant redacted by default.
/// </summary>
/// <remarks>
/// Drives the real committed module end-to-end through an in-process ASP.NET Core <see cref="TestServer"/>.
/// Proves the seam is actually resolved (not merely registered): with a fake admin registered the list
/// endpoint returns the instance; with none registered it fails open (200 / empty), never a 500. Non-vacuous
/// PII redaction: tenant omitted by default, present only when <c>ExposeSensitiveData</c> is opted in. Never
/// skipped, no Docker.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Platform")]
public sealed class WorkflowDashboardModuleShould
{
	private const string SecretTenant = "tenant-contoso";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static async Task<WebApplication> BuildHostAsync(
		bool withAdmin, bool exposeSensitive = false, bool emptyStore = false)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddDashboard(o => o.ExposeSensitiveData = exposeSensitive);
		if (withAdmin)
		{
			builder.Services.AddSingleton<IWorkflowStoreAdmin>(new FakeWorkflowStoreAdmin(emptyStore));
		}

		var app = builder.Build();
		app.MapDashboardApi();
		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	[Fact]
	public async Task FailOpenWithConfiguredFalseWhenNoWorkflowAdminIsRegistered()
	{
		await using var app = await BuildHostAsync(withAdmin: false).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client
			.GetAsync(new Uri("/dashboard/api/workflows", UriKind.Relative)).ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var view = await response.Content
			.ReadFromJsonAsync<WorkflowStatsProbe>(JsonOptions).ConfigureAwait(false);
		view.ShouldNotBeNull();
		view.Configured.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnEmptyInstanceListWhenNoWorkflowAdminIsRegistered()
	{
		await using var app = await BuildHostAsync(withAdmin: false).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var response = await client
			.GetAsync(new Uri("/dashboard/api/workflows/instances", UriKind.Relative)).ConfigureAwait(false);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var entries = await response.Content
			.ReadFromJsonAsync<JsonElement[]>(JsonOptions).ConfigureAwait(false);
		entries.ShouldNotBeNull();
		entries.ShouldBeEmpty();
	}

	[Fact]
	public async Task ListInstancesAndReportStatsWhenTheAdminIsResolved()
	{
		await using var app = await BuildHostAsync(withAdmin: true).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var stats = await client.GetFromJsonAsync<WorkflowStatsProbe>(
			new Uri("/dashboard/api/workflows", UriKind.Relative), JsonOptions).ConfigureAwait(false);
		stats.ShouldNotBeNull();
		stats.Configured.ShouldBeTrue();
		stats.Total.ShouldBe(1);
		stats.Running.ShouldBe(1);

		var instances = await client.GetFromJsonAsync<JsonElement[]>(
			new Uri("/dashboard/api/workflows/instances", UriKind.Relative), JsonOptions).ConfigureAwait(false);
		instances.ShouldNotBeNull();
		instances.Length.ShouldBe(1);
		instances[0].GetProperty("workflowName").GetString().ShouldBe("OrderWorkflow");
		instances[0].GetProperty("status").GetString().ShouldBe("Running");
	}

	[Fact]
	public async Task DistinguishAbsentStoreFromAnEmptyStoreViaTheConfiguredFlag()
	{
		// SA condition #2: fail-open must be VISIBLE — "no admin store registered" (feature absent) must be
		// distinguishable from "store present, zero workflows", not a silent empty that reads as "zero".
		await using var absent = await BuildHostAsync(withAdmin: false).ConfigureAwait(false);
		using var absentClient = absent.GetTestClient();
		var absentView = await absentClient.GetFromJsonAsync<WorkflowStatsProbe>(
			new Uri("/dashboard/api/workflows", UriKind.Relative), JsonOptions).ConfigureAwait(false);
		absentView.ShouldNotBeNull();
		absentView.Configured.ShouldBeFalse("absent store must report configured:false (feature unavailable)");

		await using var empty = await BuildHostAsync(withAdmin: true, emptyStore: true).ConfigureAwait(false);
		using var emptyClient = empty.GetTestClient();
		var emptyView = await emptyClient.GetFromJsonAsync<WorkflowStatsProbe>(
			new Uri("/dashboard/api/workflows", UriKind.Relative), JsonOptions).ConfigureAwait(false);
		emptyView.ShouldNotBeNull();
		emptyView.Configured.ShouldBeTrue("present-but-empty store must report configured:true (available, zero)");
		emptyView.Total.ShouldBe(0);
	}

	[Fact]
	public async Task RedactInstanceTenantIdByDefault()
	{
		await using var app = await BuildHostAsync(withAdmin: true, exposeSensitive: false).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var body = await client.GetStringAsync(
			new Uri("/dashboard/api/workflows/instances", UriKind.Relative)).ConfigureAwait(false);

		body.ShouldNotContain(SecretTenant);
		using var doc = JsonDocument.Parse(body);
		doc.RootElement[0].TryGetProperty("tenantId", out _).ShouldBeFalse("tenantId must be omitted by default");
	}

	[Fact]
	public async Task ExposeInstanceTenantIdWhenOptedIn()
	{
		await using var app = await BuildHostAsync(withAdmin: true, exposeSensitive: true).ConfigureAwait(false);
		using var client = app.GetTestClient();

		var instances = await client.GetFromJsonAsync<JsonElement[]>(
			new Uri("/dashboard/api/workflows/instances", UriKind.Relative), JsonOptions).ConfigureAwait(false);

		instances.ShouldNotBeNull();
		instances[0].GetProperty("tenantId").GetString().ShouldBe(SecretTenant);
	}

	private sealed record WorkflowStatsProbe(bool Configured, long Running, long Completed, long Faulted, long Total);

	private sealed class FakeWorkflowStoreAdmin(bool empty = false) : IWorkflowStoreAdmin
	{
		public ValueTask<IReadOnlyList<WorkflowInstanceSummary>> QueryWorkflowsAsync(
			WorkflowQueryFilter filter,
			CancellationToken cancellationToken)
		{
			IReadOnlyList<WorkflowInstanceSummary> summaries = empty
				? []
				:
				[
					new WorkflowInstanceSummary
					{
						InstanceId = "wf-1",
						WorkflowName = "OrderWorkflow",
						Status = WorkflowStatus.Running,
						StartedAt = DateTimeOffset.UtcNow,
						TenantId = SecretTenant,
					},
				];
			return ValueTask.FromResult(summaries);
		}

		public ValueTask<WorkflowInstanceSummary?> GetSummaryAsync(string instanceId, CancellationToken cancellationToken) =>
			ValueTask.FromResult<WorkflowInstanceSummary?>(null);

		public ValueTask<WorkflowStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(empty
				? new WorkflowStoreStatistics()
				: new WorkflowStoreStatistics { Running = 1, Total = 1 });
	}
}
