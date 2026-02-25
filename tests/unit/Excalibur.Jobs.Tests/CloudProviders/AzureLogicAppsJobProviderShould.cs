// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json;

using Azure.ResourceManager;

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.CloudProviders.Azure;

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
			new AzureLogicAppsJobProvider(null!, CreateOptions(), A.Fake<ILogger<AzureLogicAppsJobProvider>>()));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AzureLogicAppsJobProvider(A.Fake<ArmClient>(), null!, A.Fake<ILogger<AzureLogicAppsJobProvider>>()));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AzureLogicAppsJobProvider(A.Fake<ArmClient>(), CreateOptions(), null!));
	}

	[Fact]
	public void BuildWorkflowDefinitionUsingConfiguredEndpoint()
	{
		var provider = new AzureLogicAppsJobProvider(
			A.Fake<ArmClient>(),
			CreateOptions(),
			A.Fake<ILogger<AzureLogicAppsJobProvider>>());
		var method = typeof(AzureLogicAppsJobProvider)
			.GetMethod("CreateWorkflowDefinition", BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull();

		var result = method.MakeGenericMethod(typeof(TestBackgroundJob))
			.Invoke(provider, ["sync-orders"]);
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

	[Fact]
	public async Task RethrowWhenScheduleCannotResolveSubscription()
	{
		var armClient = A.Fake<ArmClient>();
		_ = A.CallTo(() => armClient.GetDefaultSubscriptionAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("subscription unavailable"));
		var provider = new AzureLogicAppsJobProvider(
			armClient,
			CreateOptions(),
			A.Fake<ILogger<AzureLogicAppsJobProvider>>());

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			provider.ScheduleJobAsync<TestBackgroundJob>("sync-orders", "unused", CancellationToken.None));

		ex.Message.ShouldBe("subscription unavailable");
	}

	[Fact]
	public async Task RethrowWhenDeleteCannotResolveSubscription()
	{
		var armClient = A.Fake<ArmClient>();
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
