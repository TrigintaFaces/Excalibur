// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for <see cref="IJobSchedulerProvider"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this kit, implement <see cref="CreateProvider"/> and
/// <see cref="GetCapturedCronExpressionAsync"/>, and expose each scenario as a test in the derived class.
/// The kit pins the contract every <see cref="IJobSchedulerProvider"/> implementation must honor: the
/// cron expression passed to <see cref="IJobSchedulerProvider.ScheduleJobAsync{TJob}"/> MUST reach
/// whatever downstream scheduling call the provider makes — it must never be silently dropped or
/// substituted with a different schedule (the class of bug this kit exists to catch).
/// </para>
/// <para>
/// <strong>Wiring per provider (author≠impl seam):</strong> <see cref="CreateProvider"/> must return a
/// provider instance wired against a test double for its cloud SDK client that RECORDS the schedule
/// value the provider actually sends downstream, rather than a live cloud client. Concretely:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>AWS</strong> (<c>AwsSchedulerJobProvider</c>): the constructor takes a concrete
/// <c>AmazonSchedulerClient</c>. Point that client at a fake HTTP handler
/// (<c>AmazonSchedulerClient(credentials, new AmazonSchedulerConfig { HttpClientFactory = ... })</c> or an
/// injected <see cref="System.Net.Http.HttpMessageHandler"/>) that captures the request body's
/// <c>ScheduleExpression</c> field (the provider sends <c>$"cron({cronExpression})"</c> — strip the
/// wrapper when comparing) and returns a minimal valid <c>CreateScheduleResponse</c>.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Google Cloud</strong> (<c>GoogleCloudSchedulerJobProvider</c>): <c>CloudSchedulerClient</c> is
/// constructible from a <c>CloudSchedulerClientBuilder</c> with a fake gRPC channel/call invoker, or the
/// gRPC testing helpers for the Google client libraries; capture the <c>Job.Schedule</c> field of the
/// <c>CreateJobRequest</c> the provider issues.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Azure</strong> (<c>AzureLogicAppsJobProvider</c>): use the internal
/// <c>IArmClientSeam</c>/<c>ArmClientAdapter</c> test seam (the constructor overload accepting
/// <c>IArmClientSeam</c>) to capture the workflow's <c>LogicWorkflowData.Definition</c> BinaryData, parse
/// the JSON, and read back the mapped <c>triggers.recurrence.recurrence</c> shape (frequency/interval/
/// schedule) built from the cron expression — the captured value for this kit's purposes is the
/// reconstructed cron-equivalent (e.g. re-derive <c>"*/N * * * *"</c> from <c>frequency="Minute"</c>,
/// <c>interval=N</c>) or, more directly, assert the recurrence shape fields the mapper is known to
/// produce for <see cref="SampleCronExpression"/> rather than round-tripping back to cron text.
/// </description>
/// </item>
/// </list>
/// <para>
/// The kit exposes plain <c>public virtual</c> methods with no test-framework attributes. Add the
/// attributes required by your own test framework (for example <c>[Fact]</c>) on thin overrides in your
/// derived class.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Conformance scenario method naming convention (matches sibling conformance kits).")]
public abstract class SchedulerConformanceTestKit
{
	/// <summary>
	/// Gets the job name used by the conformance scenarios.
	/// </summary>
	/// <value> A stable, provider-agnostic job name. </value>
	protected virtual string SampleJobName => "excalibur-conformance-job";

	/// <summary>
	/// Gets the cron expression used to verify the provider honors the configured schedule.
	/// </summary>
	/// <value> A distinctive 5-field cron expression unlikely to collide with a provider's hardcoded default. </value>
	protected virtual string SampleCronExpression => "*/7 * * * *";

	/// <summary>
	/// Creates a new instance of the scheduler provider under test, wired against a test double for its
	/// cloud SDK client so the schedule it sends downstream can be observed via
	/// <see cref="GetCapturedCronExpressionAsync"/>.
	/// </summary>
	/// <returns> The provider instance under test. </returns>
	protected abstract IJobSchedulerProvider CreateProvider();

	/// <summary>
	/// Returns the schedule value that reached the provider's downstream SDK call during the most recent
	/// <see cref="IJobSchedulerProvider.ScheduleJobAsync{TJob}"/> invocation, or <see langword="null"/> if
	/// no call has been captured yet.
	/// </summary>
	/// <returns>
	/// The captured schedule value, expressed however is natural for the provider under test (for example
	/// the raw cron string for AWS/Google, or a structural description of the mapped recurrence for
	/// Azure) — the derived class defines the comparison in <see cref="AssertCronExpressionHonored"/>.
	/// </returns>
	protected abstract Task<string?> GetCapturedCronExpressionAsync();

	/// <summary>
	/// Asserts that the value captured by <see cref="GetCapturedCronExpressionAsync"/> reflects
	/// <see cref="SampleCronExpression"/> having actually reached the provider's downstream call, rather
	/// than a hardcoded or default schedule. The default implementation requires an exact string match;
	/// override when the provider under test represents the captured schedule structurally (for example
	/// Azure's mapped recurrence shape) instead of as a raw cron string.
	/// </summary>
	/// <param name="capturedValue"> The value returned by <see cref="GetCapturedCronExpressionAsync"/>. </param>
	protected virtual void AssertCronExpressionHonored(string? capturedValue)
	{
		if (!string.Equals(capturedValue, SampleCronExpression, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Expected the provider to pass the configured cron expression '{SampleCronExpression}' to its " +
				$"downstream scheduling call, but the captured value was '{capturedValue ?? "<null>"}'. " +
				"A provider that ignores the caller's cron expression silently schedules the wrong recurrence.");
		}
	}

	/// <summary>
	/// Verifies <see cref="IJobSchedulerProvider.ScheduleJobAsync{TJob}"/> passes the caller's cron
	/// expression through to the provider's downstream scheduling call, rather than silently dropping it
	/// in favor of a hardcoded or default recurrence.
	/// </summary>
	/// <returns> A task representing the assertion. </returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Calls IJobSchedulerProvider.ScheduleJobAsync, which at least one provider implementation fulfils via reflection-based JSON.")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"Calls IJobSchedulerProvider.ScheduleJobAsync, which at least one provider implementation fulfils via reflection-based JSON.")]
	public virtual async Task ScheduleJobAsync_PassesCronExpressionToDownstreamCall()
	{
		var provider = CreateProvider();

		await provider.ScheduleJobAsync<ConformanceJob>(SampleJobName, SampleCronExpression, CancellationToken.None)
			.ConfigureAwait(false);

		var captured = await GetCapturedCronExpressionAsync().ConfigureAwait(false);

		AssertCronExpressionHonored(captured);
	}

	/// <summary>
	/// A minimal <see cref="IBackgroundJob"/> used as the <c>TJob</c> type argument for conformance scenarios.
	/// </summary>
	protected sealed class ConformanceJob : IBackgroundJob
	{
		/// <inheritdoc/>
		public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
