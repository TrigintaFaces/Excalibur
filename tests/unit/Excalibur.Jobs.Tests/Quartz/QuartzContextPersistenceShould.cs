// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using Excalibur.Jobs.Quartz;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Excalibur.Jobs.Tests.Quartz;

[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Pattern", "Regression")]
public sealed class QuartzContextPersistenceShould
{
	private static readonly string[] ExpectedNames = ["camel-value", "pascal-value"];
	[Fact]
	public async Task RestorePayloadWithEachJobsMetadataInANewProvider()
	{
		var first = Configure();
		JobDataMap firstData;
		JobDataMap secondData;
		await using (var provider = first.BuildServiceProvider())
		{
			var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
			try
			{
				firstData = (await scheduler.GetJobDetail(new JobKey("camel")))!.JobDataMap;
				secondData = (await scheduler.GetJobDetail(new JobKey("pascal")))!.JobDataMap;
			}
			finally { await scheduler.Shutdown(); }
		}

		// The second provider reconstructs codecs; it receives the first provider's persisted JSON.
		await using var restarted = Configure().BuildServiceProvider();
		await using var scope = restarted.CreateAsyncScope();
		var adapter = ActivatorUtilities.CreateInstance<QuartzGenericJobAdapter<ContextRecordingJob, ContextPayload>>(scope.ServiceProvider);
		await adapter.Execute(Context("camel", firstData), CancellationToken.None);
		await adapter.Execute(Context("pascal", secondData), CancellationToken.None);
		restarted.GetRequiredService<ContextRecorder>().Names.ShouldBe(ExpectedNames);
	}

	[Fact]
	public async Task RefuseMissingPersistedCodecVersion()
	{
		await using var provider = Configure().BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		var adapter = ActivatorUtilities.CreateInstance<QuartzGenericJobAdapter<ContextRecordingJob, ContextPayload>>(scope.ServiceProvider);
		await Should.ThrowAsync<InvalidOperationException>(() => adapter.Execute(Context("camel", new JobDataMap
		{
			["ContextData"] = "{\"name\":\"value\"}",
			["ContextSerialization"] = "metadata-v1:previous-version",
		})));
		provider.GetRequiredService<ContextRecorder>().Names.ShouldBeEmpty();
	}

	[Fact]
	public async Task DecodeLegacyPayloadWithLegacyPolicyEvenWhenNewMetadataIsRegistered()
	{
		await using var provider = Configure().BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		var adapter = ActivatorUtilities.CreateInstance<QuartzGenericJobAdapter<ContextRecordingJob, ContextPayload>>(scope.ServiceProvider);
		await adapter.Execute(Context("camel", new JobDataMap { ["ContextData"] = "{\"Name\":\"legacy-value\"}" }));
		provider.GetRequiredService<ContextRecorder>().Names.Single().ShouldBe("legacy-value");
	}

	[Fact]
	public void MatchNestedGenericIdentityAcrossAssemblyVersionChanges()
	{
		var type = typeof(Dictionary<string, List<ContextPayload>>);
		var oldIdentity = System.Text.RegularExpressions.Regex.Replace(type.AssemblyQualifiedName!,
			"Version=[^,\\]]+", "Version=0.0.0.1");
		new RegisteredJobType(type).Matches(oldIdentity).ShouldBeTrue();
		new RegisteredJobType(typeof(Dictionary<string, List<string>>)).Matches(oldIdentity).ShouldBeFalse();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void RejectMixedDuplicateRegistrations(bool metadataFirst)
	{
		var jobs = new JobConfigurator(new ServiceCollection());
		if (metadataFirst)
		{
			jobs.AddJob<ContextRecordingJob, ContextPayload>("0 0 * * * ?", new("one"), ContextJson.Default.ContextPayload, "same");
			Should.Throw<InvalidOperationException>(() => jobs.AddJob<ContextRecordingJob, ContextPayload>("0 0 * * * ?", new("two"), "same"));
		}
		else
		{
			jobs.AddJob<ContextRecordingJob, ContextPayload>("0 0 * * * ?", new("one"), "same");
			Should.Throw<InvalidOperationException>(() => jobs.AddJob<ContextRecordingJob, ContextPayload>("0 0 * * * ?", new("two"), ContextJson.Default.ContextPayload, "same"));
		}
	}

	[Fact]
	public void RejectDuplicateCodecForSameJobIdentity()
	{
		var services = Configure();
		var configurator = new JobConfigurator(services);
		Should.Throw<InvalidOperationException>(() => configurator.AddJob<ContextRecordingJob, ContextPayload>(
			"0 0 * * * ?", new ContextPayload("replacement"), ContextJson.Default.ContextPayload, "camel"));
	}

	[Fact]
	public async Task RejectMalformedCanonicalPayloadRatherThanUseLegacyFallback()
	{
		await using var provider = Configure().BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		var adapter = ActivatorUtilities.CreateInstance<QuartzGenericJobAdapter<ContextRecordingJob, ContextPayload>>(scope.ServiceProvider);
		await Should.ThrowAsync<InvalidOperationException>(() => adapter.Execute(Context("camel", new JobDataMap
		{
			["ContextData"] = "{invalid",
			["Context"] = new ContextPayload("must-not-run"),
		})));
		provider.GetRequiredService<ContextRecorder>().Names.ShouldBeEmpty();
	}

	[Fact]
	public async Task HonorExplicitCancellationToken()
	{
		await using var provider = Configure().BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		var adapter = ActivatorUtilities.CreateInstance<QuartzGenericJobAdapter<ContextRecordingJob, ContextPayload>>(scope.ServiceProvider);
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		await Should.ThrowAsync<OperationCanceledException>(() => adapter.Execute(Context("camel", new JobDataMap
		{
			["Context"] = new ContextPayload("must-not-run"),
		}), cancellation.Token).AsTask());
		provider.GetRequiredService<ContextRecorder>().Names.ShouldBeEmpty();
	}

	private static ServiceCollection Configure()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<ContextRecorder>();
		var camel = new ContextJson(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
		var configurator = new JobConfigurator(services);
		configurator.AddJob<ContextRecordingJob, ContextPayload>("0 0 * * * ?", new("camel-value"), camel.ContextPayload, "camel");
		configurator.AddJob<ContextRecordingJob, ContextPayload>("0 0 * * * ?", new("pascal-value"), ContextJson.Default.ContextPayload, "pascal");
		services.AddQuartz(q => q.ConfigureScheduler(o => o.InstanceName = Guid.NewGuid().ToString()));
		return services;
	}

	private static IJobExecutionContext Context(string name, JobDataMap data)
	{
		var context = A.Fake<IJobExecutionContext>();
		var detail = JobBuilder.Create<QuartzGenericJobAdapter<ContextRecordingJob, ContextPayload>>()
			.WithIdentity(name).UsingJobData(data).Build();
		A.CallTo(() => context.JobDetail).Returns(detail);
		return context;
	}
}

public sealed record ContextPayload(string Name);
public sealed class ContextRecorder
{
	public List<string> Names { get; } = [];
}
public sealed class ContextRecordingJob(ContextRecorder recorder) : IBackgroundJob<ContextPayload>
{
	public Task ExecuteAsync(ContextPayload context, CancellationToken cancellationToken)
	{
		recorder.Names.Add(context.Name);
		return Task.CompletedTask;
	}
}
[JsonSerializable(typeof(ContextPayload))]
internal sealed partial class ContextJson : JsonSerializerContext;
