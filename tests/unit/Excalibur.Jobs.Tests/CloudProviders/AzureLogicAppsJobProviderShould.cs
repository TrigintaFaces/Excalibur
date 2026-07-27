// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json;

using Excalibur.Jobs;
using Excalibur.Jobs.Azure;
using Excalibur.Jobs.Azure.Internal;

using FakeItEasy;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Tests.CloudProviders;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureLogicAppsJobProviderShould
{
	[Fact]
	public void ThrowWhenArmClientIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AzureLogicAppsJobProvider((IArmClientSeam)null!, CreateOptions(), A.Fake<ILogger<AzureLogicAppsJobProvider>>()));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AzureLogicAppsJobProvider(A.Fake<IArmClientSeam>(), null!, A.Fake<ILogger<AzureLogicAppsJobProvider>>()));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AzureLogicAppsJobProvider(A.Fake<IArmClientSeam>(), CreateOptions(), null!));
	}

	[Fact]
	public void BuildWorkflowDefinitionUsingConfiguredEndpoint()
	{
		var provider = new AzureLogicAppsJobProvider(
			A.Fake<IArmClientSeam>(),
			CreateOptions(),
			A.Fake<ILogger<AzureLogicAppsJobProvider>>());
		var method = typeof(AzureLogicAppsJobProvider)
			.GetMethod("CreateWorkflowDefinition", BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull();

		var recurrence = CronRecurrenceMapper.Map("30 8 * * *");
		var result = method.MakeGenericMethod(typeof(TestBackgroundJob))
			.Invoke(provider, ["sync-orders", recurrence]);
		result.ShouldBeOfType<BinaryData>();

		using var json = JsonDocument.Parse(result.ToString()!);
		json.RootElement.GetProperty("triggers")
			.GetProperty("recurrence")
			.GetProperty("type")
			.GetString()
			.ShouldBe("recurrence");
		json.RootElement.GetProperty("actions")
			.GetProperty("http_request")
			.GetProperty("inputs")
			.GetProperty("uri")
			.GetString()
			.ShouldBe("https://jobs.example.com/execute");
		json.RootElement.GetProperty("actions")
			.GetProperty("http_request")
			.GetProperty("inputs")
			.GetProperty("body")
			.GetProperty("jobName")
			.GetString()
			.ShouldBe("sync-orders");
		json.RootElement.GetProperty("actions")
			.GetProperty("http_request")
			.GetProperty("inputs")
			.GetProperty("body")
			.GetProperty("jobType")
			.GetString()
			.ShouldContain(nameof(TestBackgroundJob));
	}

	/// <summary>
	/// AOT ship-gate (bcdl33): <c>CreateWorkflowDefinition</c> was rewritten from reflection-based
	/// <c>BinaryData.FromObjectAsJson</c> to a hand-built <see cref="System.Text.Json.Nodes.JsonObject"/>
	/// tree so the scheduler surface can drop <c>[RequiresUnreferencedCode]</c>/<c>[RequiresDynamicCode]</c>.
	/// This test proves the new build is byte-for-byte identical to the old reflection-based serialization
	/// for a full, representative workflow definition (a weekly recurrence trigger + the HTTP action), so
	/// the AOT-safety rewrite could not have silently changed the emitted Logic Apps definition.
	/// </summary>
	[Fact]
	public void BuildWorkflowDefinitionByteIdenticalToReflectionBasedSerialization()
	{
		var options = CreateOptions();
		var provider = new AzureLogicAppsJobProvider(A.Fake<IArmClientSeam>(), options, A.Fake<ILogger<AzureLogicAppsJobProvider>>());
		var method = typeof(AzureLogicAppsJobProvider)
			.GetMethod("CreateWorkflowDefinition", BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull();

		// Weekly recurrence at 09:15 on Mon/Tue/Wed/Fri — exercises the full nested "schedule" shape
		// (hours + minutes + weekDays), not just the bare frequency/interval branch.
		var recurrence = CronRecurrenceMapper.Map("15 9 * * 1-3,5");

		var actual = (BinaryData)method.MakeGenericMethod(typeof(TestBackgroundJob))
			.Invoke(provider, ["golden-job", recurrence])!;

		var expected = BuildLegacyReflectionBasedDefinition<TestBackgroundJob>("golden-job", recurrence, options);

		actual.ToString().ShouldBe(expected.ToString());
	}

	/// <summary>
	/// Reproduces the pre-fix (reflection-based) <c>CreateWorkflowDefinition</c> exactly as it existed
	/// before the AOT-safety rewrite, so the golden test above can assert byte-identical output.
	/// </summary>
	private static BinaryData BuildLegacyReflectionBasedDefinition<TJob>(
		string jobName,
		AzureRecurrence recurrence,
		AzureLogicAppsOptions options)
		where TJob : class, IBackgroundJob
	{
		object recurrenceValue = recurrence.Hours is null && recurrence.Minutes is null && recurrence.WeekDays is null
			? new { frequency = recurrence.Frequency, interval = recurrence.Interval }
			: new
			{
				frequency = recurrence.Frequency,
				interval = recurrence.Interval,
				schedule = new
				{
					hours = recurrence.Hours,
					minutes = recurrence.Minutes,
					weekDays = recurrence.WeekDays,
				},
			};

		var definition = new
		{
			contentVersion = "1.0.0.0",
			parameters = new { },
			triggers = new
			{
				recurrence = new
				{
					type = "recurrence",
					recurrence = recurrenceValue,
				},
			},
			actions = new
			{
				http_request = new
				{
					type = "Http",
					inputs = new
					{
						method = "POST",
						uri = options.JobExecutionEndpoint,
						headers = new { ContentType = "application/json" },
						body = new { jobType = typeof(TJob).AssemblyQualifiedName, jobName },
					},
				},
			},
		};

		return BinaryData.FromObjectAsJson(definition);
	}

	[Fact]
	public async Task RethrowWhenScheduleCannotResolveSubscription()
	{
		var armClient = A.Fake<IArmClientSeam>();
		_ = A.CallTo(() => armClient.GetDefaultSubscriptionAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("subscription unavailable"));
		var provider = new AzureLogicAppsJobProvider(
			armClient,
			CreateOptions(),
			A.Fake<ILogger<AzureLogicAppsJobProvider>>());

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			provider.ScheduleJobAsync<TestBackgroundJob>("sync-orders", "*/5 * * * *", CancellationToken.None));

		ex.Message.ShouldBe("subscription unavailable");
	}

	[Fact]
	public async Task RethrowWhenDeleteCannotResolveSubscription()
	{
		var armClient = A.Fake<IArmClientSeam>();
		_ = A.CallTo(() => armClient.GetDefaultSubscriptionAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("subscription unavailable"));
		var provider = new AzureLogicAppsJobProvider(
			armClient,
			CreateOptions(),
			A.Fake<ILogger<AzureLogicAppsJobProvider>>());

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			provider.DeleteJobAsync("sync-orders", CancellationToken.None));

		ex.Message.ShouldBe("subscription unavailable");
	}

	private static AzureLogicAppsOptions CreateOptions() =>
		new()
		{
			ResourceGroupName = "jobs-rg",
			SubscriptionId = "sub-id",
			JobExecutionEndpoint = "https://jobs.example.com/execute",
		};

	private sealed class TestBackgroundJob : IBackgroundJob
	{
		public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
