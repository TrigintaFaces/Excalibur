// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compat.MediatR;

using MediatRMigration.Behaviors;
using MediatRMigration.Messages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Excalibur.MediatRMigration.Tests;

/// <summary>
/// bd-elzenf — functional lock for FR-18 / AC-13: the ported MediatR sample (swap-only migration)
/// builds and its handlers run correctly on Excalibur.Dispatch via the compat surface. Non-vacuous:
/// RED until the WS1 routing adapters land (facade throws), GREEN once a real migrated app works.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class PortedSampleShould
{
    private static IMediator BuildSampleMediator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Mirror the sample's Program.cs registration: scan the sample assembly + the open LoggingBehavior.
        services.AddMediatRCompat(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Ping).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact] // AC-2 via the ported sample: Send routes to PingHandler and returns its response.
    public async Task RunPingHandler_OnTheMigratedApp()
    {
        var mediator = BuildSampleMediator();

        var result = await mediator.Send(new Ping("hello"));

        result.ShouldBe("pong:hello");
    }

    [Fact] // AC-6 via the ported sample: CreateStream runs CountdownHandler (3,2,1).
    public async Task RunCountdownStream_OnTheMigratedApp()
    {
        var mediator = BuildSampleMediator();

        var results = new List<int>();
        await foreach (var value in mediator.CreateStream(new Countdown(3)))
        {
            results.Add(value);
        }

        results.ShouldBe([3, 2, 1]);
    }

    [Fact] // AC-3 via the ported sample: Publish fans out to BOTH Pinged handlers (log + audit).
    public async Task FanOutToBothNotificationHandlers_OnTheMigratedApp()
    {
        var mediator = BuildSampleMediator();

        var originalOut = Console.Out;
        using var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            await mediator.Publish(new Pinged("evt"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = captured.ToString();
        output.ShouldContain("[log]");
        output.ShouldContain("[audit]");
    }
}
