// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Orchestration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.Orchestration;

[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Pattern", "Regression")]
public sealed class SagaGeneratedPreparationMetadataShould
{
    [Fact]
    public void RoundTripActualGeneratedContextThroughPublicRegistrationWithoutReplacingItsOptionsOwner()
    {
        var context = SagaPolicyStateContext.Default;
        var options = context.Options;
        var metadata = context.GetTypeInfo(typeof(SagaPolicyState)).ShouldNotBeNull();
        metadata.Type.ShouldBe(typeof(SagaPolicyState));
        metadata.Options.PropertyNamingPolicy.ShouldBeSameAs(JsonNamingPolicy.CamelCase);
        metadata.Options.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
        using var services = Provider(context);
        var policy = services.GetRequiredService<SagaAtomicPreparationPolicy>();
        var original = new SagaPolicyState { SagaId = Guid.NewGuid(), DisplayName = "literal-value", Version = 7 };

        var bytes = policy.EncodeState(original, typeof(SagaPolicyState));
        using var wire = JsonDocument.Parse(bytes);
        wire.RootElement.GetProperty("displayName").GetString().ShouldBe("literal-value");
        wire.RootElement.TryGetProperty("DisplayName", out _).ShouldBeFalse();
        wire.RootElement.TryGetProperty("optionalValue", out _).ShouldBeFalse();
        var recovered = policy.DecodeState(bytes, typeof(SagaPolicyState)).ShouldBeOfType<SagaPolicyState>();
        recovered.ShouldNotBeSameAs(original);
        recovered.SagaId.ShouldBe(original.SagaId);
        recovered.DisplayName.ShouldBe(original.DisplayName);
        recovered.Version.ShouldBe(7);
        context.Options.ShouldBeSameAs(options);
        context.GetTypeInfo(typeof(SagaPolicyState)).ShouldNotBeNull().Type.ShouldBe(typeof(SagaPolicyState));
        options.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void KeepForeignOptionsMetadataRefusalInsteadOfRelaxingTheProvenanceGuard()
    {
        using var services = Provider(new ForeignOptionsResolver());
        var policy = services.GetRequiredService<SagaAtomicPreparationPolicy>();
        Should.Throw<InvalidOperationException>(() => policy.EncodeState(new SagaPolicyState(), typeof(SagaPolicyState)));
    }

    [Fact]
    public void RefuseMissingGeneratedStateWithoutReflectionFallback()
    {
        using var services = Provider(SagaPolicyStateContext.Default);
        var policy = services.GetRequiredService<SagaAtomicPreparationPolicy>();
        Should.Throw<NotSupportedException>(() => policy.EncodeState(new UnregisteredState(), typeof(UnregisteredState)));
    }

    private static ServiceProvider Provider(IJsonTypeInfoResolver metadata)
    {
        var services = new ServiceCollection();
        services.AddSingleton(A.Fake<IPayloadSerializer>());
        services.AddDurableSagaTransitions(metadata);
        return services.BuildServiceProvider();
    }

    private sealed class ForeignOptionsResolver : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
            => SagaPolicyStateContext.Default.GetTypeInfo(type);
    }

    private sealed class UnregisteredState : SagaState;
}

internal sealed class SagaPolicyState : SagaState
{
    public string? DisplayName { get; set; }
    public string? OptionalValue { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SagaPolicyState))]
internal partial class SagaPolicyStateContext : JsonSerializerContext;
