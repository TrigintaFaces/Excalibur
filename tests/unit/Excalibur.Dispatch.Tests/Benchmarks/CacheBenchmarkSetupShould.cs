// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics;
using Excalibur.Dispatch.Benchmarks.Optimization;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Behavior")]
public sealed class CacheBenchmarkSetupShould
{
    [Fact]
    public void RejectAmbientActivityBeforeItCanRetainUnclassifiedMessageEvents()
    {
        using var activity = new Activity("cache-fixture-rejection").Start();
        var fixture = new CacheOptimizationBenchmarks();
        Should.Throw<InvalidOperationException>(() => fixture.Setup()).Message.ShouldContain("Activity");
        activity.Events.ShouldBeEmpty();
    }

    [Fact]
    public void PreserveOriginalArmResultsWithoutAmbientActivity()
    {
        var previous = Activity.Current;
        try
        {
            Activity.Current = null;
            var fixture = new CacheOptimizationBenchmarks();
            fixture.Setup();
            fixture.ProfileSelect_Warm()!.Name.ShouldBe("strict");
            fixture.TypeName_FromField().ShouldBe("TestActionMessage");
            fixture.TypeName_FromTypeof().ShouldBe(fixture.TypeName_FromField());
            fixture.TypeName_Cached().ShouldBe(fixture.TypeName_FromField());
            fixture.ActivityName_Interpolated().ShouldBe("middleware.TestActionMessage");
            fixture.ActivityName_Cached().ShouldBe(fixture.ActivityName_Interpolated());
            fixture.MessageKind_StringContains().ShouldBe("Action");
            fixture.MessageKind_Cached().ShouldBe(fixture.MessageKind_StringContains());
            fixture.Control_ReturnConstant().ShouldBe("TestActionMessage");
            Activity.Current.ShouldBeNull();
        }
        finally
        {
            Activity.Current = previous;
        }
    }
}
