// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;


namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for the AOT payload-serialization seam (fm5g0y/no8rzw/dszteu). Proves the consumer-supplied
/// source-generated resolver (<see cref="WorkflowOptions.PayloadTypeInfoResolver"/>) is actually used for
/// activity/workflow payloads — a reflection-free path when the consumer registers a context — and is
/// non-vacuous: a resolver that does not cover the payload type fails rather than silently falling back to
/// reflection, proving the seam replaces the reflection default.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowPayloadAotSerializationShould
{
    private const string WorkflowName = "aot";
    private const string ActivityName = "echo";

    [Fact]
    public async Task RoundTripPayload_ViaConsumerSourceGenResolver()
    {
        await using var provider = BuildProvider(PayloadJsonContext.Default);
        const string instanceId = "inst-aot-ok";

        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();

        await executor.StartAsync(WorkflowName, instanceId, new EchoPayload("hi", 7), CancellationToken.None);

        (await executor.GetStatusAsync(instanceId, CancellationToken.None)).ShouldBe(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task FailClosed_WhenConsumerResolverOmitsThePayloadType()
    {
        // A resolver that covers only an unrelated type: with a resolver set, System.Text.Json does NOT fall
        // back to reflection, so serializing the (uncovered) payload throws — proving the consumer resolver is
        // genuinely on the serialization path (a reflection default would have succeeded here).
        await using var provider = BuildProvider(UnrelatedJsonContext.Default);

        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();

        // The uncovered-type serialization throws NotSupportedException inside the activity step, surfaced as
        // a WorkflowActivityException. Its presence proves the consumer resolver is on the path (reflection
        // would have serialized the type successfully).
        var ex = await Should.ThrowAsync<WorkflowActivityException>(async () =>
            await executor.StartAsync(WorkflowName, "inst-aot-fail", new EchoPayload("x", 1), CancellationToken.None));
        ex.Message.ShouldContain("EchoPayload");
    }

    private static ServiceProvider BuildProvider(System.Text.Json.Serialization.JsonSerializerContext payloadContext)
    {
        var services = new ServiceCollection();

        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());

        services.AddWorkflows(payloadContext);
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
            await context.CallActivityAsync<EchoPayload>(ActivityName, input, cancellationToken));
        services.AddActivity<EchoActivity, EchoPayload, EchoPayload>(ActivityName);

        return services.BuildServiceProvider();
    }

    private sealed class EchoActivity : IActivity<EchoPayload, EchoPayload>
    {
        public ValueTask<EchoPayload> ExecuteAsync(EchoPayload input, CancellationToken cancellationToken) =>
            ValueTask.FromResult(input);
    }
}

/// <summary>A consumer payload type.</summary>
public sealed record EchoPayload(string Text, int Number);

/// <summary>An unrelated type, for the fail-closed proof that the resolver (not reflection) is used.</summary>
public sealed record UnrelatedType(string Value);

[JsonSerializable(typeof(EchoPayload))]
internal sealed partial class PayloadJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(UnrelatedType))]
internal sealed partial class UnrelatedJsonContext : JsonSerializerContext;
