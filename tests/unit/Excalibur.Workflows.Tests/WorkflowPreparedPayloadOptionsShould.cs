// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Workflows.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Workflows")]
public sealed class WorkflowPreparedPayloadOptionsShould
{
    [Fact]
    public void RefuseNullMetadataAtTheSafeFactoryBoundary()
    {
        var error = Should.Throw<ArgumentNullException>(() => WorkflowPayloadSerializer.CreateOptions(null!));
        error.ParamName.ShouldBe("consumerPayloadResolver");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddWorkflows(null!));
    }

    [Fact]
    public void PreserveLegacyJitReflectionAndNullPayloadSemantics()
    {
        RuntimeFeature.IsDynamicCodeSupported.ShouldBeTrue("this is the ordinary JIT legacy-functionality control, not a simulated Native test");
        var services = new ServiceCollection();
        services.AddWorkflows();
        using var provider = services.BuildServiceProvider();
        var options = Prepared(provider);
        var payload = new EchoPayload("legacy-\u03bb", 9001);
        var json = WorkflowPayloadSerializer.Serialize(payload, options).ShouldNotBeNull();
        WorkflowPayloadSerializer.Deserialize<EchoPayload>(json, options).ShouldBe(payload);
        WorkflowPayloadSerializer.Serialize(null, options).ShouldBeNull();
        WorkflowPayloadSerializer.Deserialize<EchoPayload>(null, options).ShouldBeNull();
        WorkflowPayloadSerializer.Deserialize<int>(null, options).ShouldBe(0);
    }

    [Fact]
    public void ShareFrozenMetadataOptionsAcrossExecutorScopes()
    {
        var services = new ServiceCollection();
        services.AddSingleton(A.Fake<IEventStore>());
        services.AddSingleton(A.Fake<IEventSerializer>());
        services.AddWorkflows(PayloadJsonContext.Default);
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        first.ServiceProvider.GetRequiredService<IWorkflowExecutor>().ShouldNotBeNull();
        second.ServiceProvider.GetRequiredService<IWorkflowExecutor>().ShouldNotBeNull();
        var options = Prepared(first.ServiceProvider);
        Prepared(second.ServiceProvider).ShouldBeSameAs(options);
        options.IsReadOnly.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        var payload = new EchoPayload("generated-\ud83d\ude80", 23);
        WorkflowPayloadSerializer.Deserialize<EchoPayload>(WorkflowPayloadSerializer.Serialize(payload, options), options).ShouldBe(payload);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitMetadataOverridesUnspecifiedLegacyCallsWithoutReflectionFallback(bool legacyFirst)
    {
        var services = new ServiceCollection();
        if (legacyFirst) services.AddWorkflows();
        services.AddWorkflows(UnrelatedJsonContext.Default);
        if (!legacyFirst) services.AddWorkflows();
        using var provider = services.BuildServiceProvider();
        var error = Should.Throw<NotSupportedException>(() => WorkflowPayloadSerializer.Serialize(new EchoPayload("not-registered", 1), Prepared(provider)));
        error.Message.ShouldContain(nameof(EchoPayload));
    }

    [Fact]
    public void RepeatTheSameExplicitResolverIdempotently()
    {
        var services = new ServiceCollection();
        services.AddWorkflows(PayloadJsonContext.Default);
        services.AddWorkflows(PayloadJsonContext.Default);
        services.Count(descriptor => descriptor.ServiceType == typeof(WorkflowPreparedPayloadOptions)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(WorkflowPayloadSelection)).ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        WorkflowPayloadSerializer.Serialize(new EchoPayload("same", 1), Prepared(provider)).ShouldContain("same");
    }

    [Fact]
    public void RefuseContradictoryExplicitResolversWithoutReplacingTheFirst()
    {
        var services = new ServiceCollection();
        services.AddWorkflows(UnrelatedJsonContext.Default);
        Should.Throw<InvalidOperationException>(() => services.AddWorkflows(PayloadJsonContext.Default));
        using var provider = services.BuildServiceProvider();
        Should.Throw<NotSupportedException>(() => WorkflowPayloadSerializer.Serialize(new EchoPayload("missing", 1), Prepared(provider)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowTheSameExplicitAndOptionsResolverInEitherConfigurationOrder(bool configureFirst)
    {
        var services = new ServiceCollection();
        if (configureFirst) services.Configure<WorkflowOptions>(options => options.PayloadTypeInfoResolver = PayloadJsonContext.Default);
        services.AddWorkflows(PayloadJsonContext.Default);
        if (!configureFirst) services.Configure<WorkflowOptions>(options => options.PayloadTypeInfoResolver = PayloadJsonContext.Default);
        using var provider = services.BuildServiceProvider();
        WorkflowPayloadSerializer.Serialize(new EchoPayload("same-options", 2), Prepared(provider)).ShouldContain("same-options");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RefuseConflictingOptionsResolverAtResolutionInEitherConfigurationOrder(bool configureFirst)
    {
        var services = new ServiceCollection();
        if (configureFirst) services.Configure<WorkflowOptions>(options => options.PayloadTypeInfoResolver = PayloadJsonContext.Default);
        services.AddWorkflows(UnrelatedJsonContext.Default);
        if (!configureFirst) services.Configure<WorkflowOptions>(options => options.PayloadTypeInfoResolver = PayloadJsonContext.Default);
        using var provider = services.BuildServiceProvider();
        var error = Should.Throw<InvalidOperationException>(() => Prepared(provider));
        error.Message.ShouldContain("conflicts");
    }

    [Fact]
    public void PreserveLastOptionsConfigurationOnTheLegacyOnlyRoute()
    {
        var services = new ServiceCollection();
        services.AddWorkflows();
        services.Configure<WorkflowOptions>(options => options.PayloadTypeInfoResolver = PayloadJsonContext.Default);
        services.Configure<WorkflowOptions>(options => options.PayloadTypeInfoResolver = UnrelatedJsonContext.Default);
        using var provider = services.BuildServiceProvider();
        Should.Throw<NotSupportedException>(() => WorkflowPayloadSerializer.Serialize(new EchoPayload("last-config", 3), Prepared(provider)));
    }

    [Fact]
    public void KeepAnAlreadyBuiltProvidersDeferredChoiceIndependentOfLaterRegistration()
    {
        var services = new ServiceCollection();
        services.AddWorkflows();
        using var first = services.BuildServiceProvider(); // Deliberately do not resolve its payload options yet.
        services.AddWorkflows(UnrelatedJsonContext.Default);
        using var second = services.BuildServiceProvider();
        WorkflowPayloadSerializer.Serialize(new EchoPayload("first-provider", 4), Prepared(first)).ShouldContain("first-provider");
        Should.Throw<NotSupportedException>(() => WorkflowPayloadSerializer.Serialize(new EchoPayload("second-provider", 4), Prepared(second)));
        Prepared(first).ShouldNotBeSameAs(Prepared(second));
    }

    [Fact]
    public void PreserveNonPayloadWorkflowOptions()
    {
        var services = new ServiceCollection();
        services.AddWorkflows(PayloadJsonContext.Default);
        services.Configure<WorkflowOptions>(options =>
        {
            options.MaxReplayEvents = 31;
            options.CaptureActivityFailureDetails = false;
        });
        using var provider = services.BuildServiceProvider();
        _ = Prepared(provider);
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkflowOptions>>().Value;
        options.MaxReplayEvents.ShouldBe(31);
        options.CaptureActivityFailureDetails.ShouldBeFalse();
    }

    [Fact]
    public async Task SerializeExplicitMetadataSignalPayloadIntoTheRealInbox()
    {
        var services = new ServiceCollection();
        services.AddSingleton(A.Fake<IEventStore>());
        services.AddSingleton(A.Fake<IEventSerializer>());
        services.AddWorkflows(PayloadJsonContext.Default);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
        var payload = new EchoPayload("signal-\u03bb", 131);
        await executor.SignalAsync("instance", "signal", "signal-id", payload, CancellationToken.None);
        var entries = await provider.GetRequiredService<IWorkflowSignalInbox>().DrainAsync("instance", CancellationToken.None);
        var accepted = entries.ShouldHaveSingleItem();
        WorkflowPayloadSerializer.Deserialize<EchoPayload>(accepted.PayloadJson, Prepared(provider)).ShouldBe(payload);
    }

    [Fact]
    public async Task RefuseAnUncoveredSignalPayloadBeforeTheRealInboxAcceptsIt()
    {
        var services = new ServiceCollection();
        services.AddSingleton(A.Fake<IEventStore>());
        services.AddSingleton(A.Fake<IEventSerializer>());
        services.AddWorkflows(UnrelatedJsonContext.Default);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
        var error = await Should.ThrowAsync<NotSupportedException>(async () =>
            await executor.SignalAsync("instance", "signal", "signal-id", new EchoPayload("uncovered", 4), CancellationToken.None));
        error.Message.ShouldContain(nameof(EchoPayload));
        (await provider.GetRequiredService<IWorkflowSignalInbox>().DrainAsync("instance", CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public void DeclareOnlyTheLegacyPublicEntryUnsafe()
    {
        var methods = typeof(WorkflowsServiceCollectionExtensions).GetMethods()
            .Where(method => method.Name == nameof(WorkflowsServiceCollectionExtensions.AddWorkflows)).ToArray();
        var legacy = methods.Single(method => method.GetParameters().Length == 1);
        var safe = methods.Single(method => method.GetParameters().Length == 2);
        legacy.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldNotBeNull();
        legacy.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldNotBeNull();
        safe.GetCustomAttribute<RequiresUnreferencedCodeAttribute>().ShouldBeNull();
        safe.GetCustomAttribute<RequiresDynamicCodeAttribute>().ShouldBeNull();
    }

    private static JsonSerializerOptions Prepared(IServiceProvider provider) =>
        provider.GetRequiredService<WorkflowPreparedPayloadOptions>().Options;
}
