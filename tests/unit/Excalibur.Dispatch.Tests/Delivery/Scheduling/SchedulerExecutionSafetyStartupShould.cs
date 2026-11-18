// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class SchedulerExecutionSafetyStartupShould
{
    [Fact]
    public void RefuseAnAttestedButExecutionIncapableSelectedStoreAtActualStartupValidation()
    {
        var services = Runtime();
        services.AddDurableScheduleStore<AdministrativeOnlyStore>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<AdministrativeOnlyStore>();
        var error = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        error.Message.ShouldContain(nameof(IScheduleExecutionFailureStore));
    }

    [Fact]
    public void StartWithTheActualCapableInMemoryAuthorityAndExplicitVolatilePersistenceIntent()
    {
        var services = Runtime();
        services.Configure<ScheduleDurabilityOptions>(o => o.AllowVolatileScheduleStore = true);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IScheduleStore>().ShouldBeAssignableTo<IScheduleExecutionFailureStore>();
        Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void RegisterTheSafetyValidatorAndNamedHealthCheckOnceAcrossRepeatedComposition()
    {
        var services = Runtime();
        services.AddTimeAwareScheduling();
        services.Configure<ScheduleDurabilityOptions>(o => o.AllowVolatileScheduleStore = true);
        using var provider = services.BuildServiceProvider();
        provider.GetServices<IValidateOptions<SchedulerOptions>>().Count(x => x is SchedulerExecutionSafetyValidator).ShouldBe(1);
        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Count(x => x.Name == "schedule-disposition").ShouldBe(1);
        Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void LeaveAdministrativeCompositionWithoutARuntimeCapabilityRequirement()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatchScheduling();
        services.AddDurableScheduleStore<AdministrativeOnlyStore>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<AdministrativeOnlyStore>();
        provider.GetServices<IValidateOptions<SchedulerOptions>>().ShouldNotContain(x => x is SchedulerExecutionSafetyValidator);
    }

    private static ServiceCollection Runtime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTimeAwareScheduling();
        services.AddAotEventSerializer(SchedulerSafetyJsonContext.Default);
        return services;
    }

    private sealed class AdministrativeOnlyStore : IScheduleStore
    {
        public AdministrativeOnlyStore() { }
        public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<IScheduledMessage>>([]);
        public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(string))]
internal sealed partial class SchedulerSafetyJsonContext : JsonSerializerContext;
