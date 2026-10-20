// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Scheduling.Aot.Sample;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args.Contains("--require-native", StringComparer.Ordinal) && RuntimeFeature.IsDynamicCodeSupported)
{
	throw new InvalidOperationException("This acceptance run requires the published NativeAOT executable.");
}
if (JsonSerializer.IsReflectionEnabledByDefault)
{
	throw new InvalidOperationException("Reflection-based JSON must remain disabled in this sample.");
}

using var host = SchedulingSampleHost.Create(args);
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var token = deadline.Token;
var scheduler = host.Services.GetRequiredService<IDispatchScheduler>();
var store = host.Services.GetRequiredService<IScheduleStore>();
var state = host.Services.GetRequiredService<VerificationState>();
var correlation = Guid.NewGuid();
var executeAt = DateTimeOffset.UtcNow.AddMilliseconds(100);
await scheduler.ScheduleOnceAsync(executeAt, new ScheduledAction("action-payload", "tenant-action", correlation), token);
await scheduler.ScheduleOnceAsync(executeAt, new ScheduledDocument("document-payload", "tenant-document", correlation), token);
var accepted = (await store.GetAllAsync(token)).ToArray();
if (accepted.Length != 2 || accepted.Any(row => !row.Enabled))
{
	throw new InvalidOperationException("Both public producer calls must persist enabled schedules.");
}

try
{
	await scheduler.ScheduleOnceAsync(executeAt, new UndeclaredAction("must-not-store"), token);
	throw new InvalidOperationException("Undeclared non-null message metadata must be rejected.");
}
catch (NotSupportedException)
{
	if ((await store.GetAllAsync(token)).Count() != accepted.Length)
	{
		throw new InvalidOperationException("Missing metadata changed the schedule population.");
	}
}

await host.StartAsync(token);
try
{
	var deliveries = await Task.WhenAll(state.Action.Task, state.Document.Task).WaitAsync(token);
	VerificationState.AssertDelivery(deliveries[0], "action-payload", "tenant-action", correlation);
	VerificationState.AssertDelivery(deliveries[1], "document-payload", "tenant-document", correlation);
	while (true)
	{
		var inventory = (await store.GetAllAsync(token)).ToArray();
		if (accepted.All(original => inventory.All(current => current.Id != original.Id || (!current.Enabled && current.LastExecutionUtc is not null))))
		{
			break;
		}
		await Task.Delay(TimeSpan.FromMilliseconds(20), token);
	}
}
finally
{
	using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
	await host.StopAsync(shutdown.Token);
}

if (host.Services.GetServices<IHostedService>().OfType<BackgroundService>().Any(service => service.ExecuteTask is { IsCompleted: false }))
{
	throw new InvalidOperationException("The hosted workers have not drained; invocation counts cannot be certified.");
}
state.AssertFinalCounts();
Console.WriteLine($"Scheduling acceptance passed: action and document delivered once; payload, tenant, correlation preserved; 2 schedules retired; missing metadata stored 0; dynamicCode={RuntimeFeature.IsDynamicCodeSupported}; jsonReflection={JsonSerializer.IsReflectionEnabledByDefault}.");

internal static class SchedulingSampleHost
{
	internal static IHost Create(string[] args)
	{
		var builder = Host.CreateApplicationBuilder(args);
		builder.Services.AddSingleton<VerificationState>();
		builder.Services.AddSingleton<IHandlerActivator, AotHandlerActivator>();
		builder.Services.AddAotJsonSerialization(SchedulingJsonContext.Default);
		builder.Services.AddAotEventSerializer(SchedulingJsonContext.Default);
		builder.Services.AddDispatch(dispatch => dispatch.AddDiscoveredHandlers());
		builder.Services.AddTimeAwareScheduling(options => options.PollInterval = TimeSpan.FromMilliseconds(50));
		builder.Services.Configure<SchedulerOptions>(options => options.PollInterval = TimeSpan.FromMilliseconds(50));
		// This bounded local sample accepts loss on exit. Production hosts must use a durable store.
		builder.Services.Configure<ScheduleDurabilityOptions>(options => options.AllowVolatileScheduleStore = true);
		MessageTypeRegistry.RegisterType<ScheduledAction>();
		MessageTypeRegistry.RegisterType<ScheduledDocument>();
		return builder.Build();
	}
}
