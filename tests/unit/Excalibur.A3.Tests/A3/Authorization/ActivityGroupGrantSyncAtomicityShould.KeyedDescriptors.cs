// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MsServiceCollection = Microsoft.Extensions.DependencyInjection.ServiceCollection;

namespace Excalibur.Tests.A3.Authorization;

[Trait("Pattern", "Regression")]
public sealed partial class ActivityGroupGrantSyncAtomicityShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefuseANonAtomicKeyedDescriptorRatherThanMisreadItAsAbsent(bool useInstance)
    {
        var services = new MsServiceCollection();
        if (useInstance)
        {
            services.AddKeyedSingleton<IActivityGroupGrantStore>("grants", new NonAtomicGrantStore());
        }
        else
        {
            services.AddKeyedSingleton<IActivityGroupGrantStore, NonAtomicGrantStore>("grants");
        }
        services.ShouldHaveSingleItem().IsKeyedService.ShouldBeTrue();

        var failure = Should.Throw<InvalidOperationException>(() =>
            Validator(services, GrantSyncAtomicity.Required, NullLogger<ActivityGroupGrantSyncPrerequisiteValidator>.Instance).Validate());

        failure.Message.ShouldContain(nameof(NonAtomicGrantStore));
        failure.Message.ShouldContain(nameof(ActivityGroupSyncOptions.GrantSyncAtomicity));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveAnAtomicKeyedDescriptorAsAHealthyComposition(bool useInstance)
    {
        var services = new MsServiceCollection();
        if (useInstance)
        {
            services.AddKeyedSingleton<IActivityGroupGrantStore>("grants", new AtomicGrantStore());
        }
        else
        {
            services.AddKeyedSingleton<IActivityGroupGrantStore, AtomicGrantStore>("grants");
        }
        services.ShouldHaveSingleItem().IsKeyedService.ShouldBeTrue();
        var logger = new RecordingLogger();

        Validator(services, GrantSyncAtomicity.Required, logger).Validate();

        logger.Warnings.ShouldBeEmpty();
    }
}
