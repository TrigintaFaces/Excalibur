// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Options;

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

        services.AddWorkflows();
        services.Configure<WorkflowOptions>(o => o.PayloadTypeInfoResolver = payloadContext);
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

/// <summary>
/// Regression lock for the AOT scoped-claim honesty gap (7xd0zj, S878 REVIEW_ARCH F1). The assembly declares
/// <c>IsAotCompatible=true</c> and the reflection-default payload path carries an unconditional IL2026/IL3050
/// suppression; that is only honest if the reflection path is unreachable under native AOT. This lock proves
/// the reflection default fails LOUD and actionably when dynamic code is unsupported (instead of a silent /
/// opaque deep-STJ failure), while remaining usable under JIT. The <c>dynamicCodeSupported</c> capability is
/// injected because <see cref="System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported"/> cannot
/// be flipped in-process.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Workflows")]
public sealed class WorkflowPayloadAotHonestyGuardShould
{
    [Fact]
    public void ThrowActionable_WhenNoResolverAndDynamicCodeUnsupported()
    {
        // RED on the pre-fix code: it returned bare options (silent) instead of failing loud.
        var ex = Should.Throw<NotSupportedException>(() =>
            WorkflowPayloadSerializer.CreateOptions(consumerPayloadResolver: null, dynamicCodeSupported: false));

        ex.Message.ShouldContain("PayloadTypeInfoResolver");
        ex.Message.ShouldContain("AOT");
    }

    [Fact]
    public void ReturnReflectionDefault_WhenNoResolverAndDynamicCodeSupported()
    {
        // JIT path preserved: reflection default still available when dynamic code is supported.
        var options = WorkflowPayloadSerializer.CreateOptions(
            consumerPayloadResolver: null, dynamicCodeSupported: true);

        options.ShouldNotBeNull();
        WorkflowPayloadSerializer.Serialize(new EchoPayload("hi", 1), options).ShouldContain("hi");
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
