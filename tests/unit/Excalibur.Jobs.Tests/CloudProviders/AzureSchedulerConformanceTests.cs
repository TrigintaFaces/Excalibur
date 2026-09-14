// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Azure.ResourceManager.Logic;

using Excalibur.Jobs;
using Excalibur.Jobs.Azure;
using Excalibur.Jobs.Azure.Internal;
using Excalibur.Testing.Conformance;

using FakeItEasy;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Tests.CloudProviders;

/// <summary>
/// Conformance tests wiring <see cref="AzureLogicAppsJobProvider"/> to
/// <see cref="SchedulerConformanceTestKit"/>. Proves the Azure provider carries the caller's cron
/// expression through to the workflow it writes rather than substituting a default recurrence.
/// </summary>
/// <remarks>
/// Logic Apps recurrence triggers do not accept cron syntax, so unlike its AWS and Google siblings this
/// provider cannot be checked by comparing a schedule string it passed straight through. The schedule
/// crosses the wire as a structural <c>frequency</c>/<c>interval</c> pair inside the workflow definition.
/// The captured value is therefore reconstructed back into cron from that pair, which keeps the kit's own
/// comparison — an exact match against the cron expression the kit supplied — intact for this provider
/// too. A provider that dropped the caller's expression would emit a different interval and the
/// reconstruction would not match.
/// </remarks>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Conformance scenario method naming convention (matches sibling conformance kits).")]
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Pattern", "SCHEDULER")]
public sealed class AzureSchedulerConformanceTests : SchedulerConformanceTestKit
{
	private RecordingArmClientSeam? _seam;

	/// <inheritdoc/>
	protected override IJobSchedulerProvider CreateProvider()
	{
		_seam = new RecordingArmClientSeam();
		return new AzureLogicAppsJobProvider(
			_seam,
			Microsoft.Extensions.Options.Options.Create(new AzureLogicAppsOptions
			{
				ResourceGroupName = "jobs-rg",
				SubscriptionId = "sub-id",
				JobExecutionEndpoint = "https://jobs.example.com/execute",
			}),
			A.Fake<ILogger<AzureLogicAppsJobProvider>>());
	}

	/// <inheritdoc/>
	protected override Task<string?> GetCapturedCronExpressionAsync()
	{
		var definition = _seam?.LastWorkflow?.Definition;
		if (definition is null)
		{
			return Task.FromResult<string?>(null);
		}

		using var json = JsonDocument.Parse(definition.ToString());
		var recurrence = json.RootElement
			.GetProperty("triggers")
			.GetProperty("recurrence")
			.GetProperty("recurrence");

		return Task.FromResult<string?>(ToCronExpression(recurrence));
	}

	/// <summary>
	/// Reconstructs the cron expression a Logic Apps recurrence was mapped from, for the interval-based
	/// shapes. Anything else is returned as its raw recurrence JSON: the kit compares the captured value
	/// against the cron it supplied, so an unrecognised shape fails loudly and shows what was actually
	/// emitted, rather than being quietly reported as no capture at all.
	/// </summary>
	private static string ToCronExpression(JsonElement recurrence)
	{
		var frequency = recurrence.GetProperty("frequency").GetString();
		var interval = recurrence.GetProperty("interval").GetInt32();
		var hasSchedule = recurrence.TryGetProperty("schedule", out _);

		return (frequency, hasSchedule) switch
		{
			("Minute", false) => string.Create(CultureInfo.InvariantCulture, $"*/{interval} * * * *"),
			("Hour", false) => string.Create(CultureInfo.InvariantCulture, $"0 */{interval} * * *"),
			_ => recurrence.GetRawText(),
		};
	}

	[Fact]
	[RequiresUnreferencedCode("Conformance scenario drives ScheduleJobAsync, which may serialize the job payload via reflection.")]
	[RequiresDynamicCode("Conformance scenario drives ScheduleJobAsync, which may serialize the job payload via reflection.")]
	public Task ScheduleJobAsync_PassesCronExpressionToDownstreamCall_Test() =>
		ScheduleJobAsync_PassesCronExpressionToDownstreamCall();

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	/// <summary>
	/// Records what the provider sent across the seam. This is the substitution the seam exists to make
	/// possible: the workflow collection the SDK would require is reached through a static extension over
	/// a concrete non-virtual type, which no test double can stand in for.
	/// </summary>
	private sealed class RecordingArmClientSeam : IArmClientSeam
	{
		public LogicWorkflowData? LastWorkflow { get; private set; }

		public Task CreateOrUpdateWorkflowAsync(
			string resourceGroupName,
			string workflowName,
			LogicWorkflowData workflow,
			CancellationToken cancellationToken)
		{
			LastWorkflow = workflow;
			return Task.CompletedTask;
		}

		public Task<bool> DeleteWorkflowAsync(
			string resourceGroupName,
			string workflowName,
			CancellationToken cancellationToken) => Task.FromResult(true);
	}
}
