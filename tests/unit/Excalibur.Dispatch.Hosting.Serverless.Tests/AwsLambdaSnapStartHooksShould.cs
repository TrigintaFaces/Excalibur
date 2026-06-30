// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Independent engage-test (author≠impl) for the AWS Lambda SnapStart warmup hook registration point.
/// </summary>
/// <remarks>
/// SnapStart itself is enabled on the Lambda function configuration; what the framework provides is a
/// <see cref="AwsLambdaSnapStartHooks.RegisterWarmup"/> entry point that registers the cold-start optimizer's
/// warmup as a before-snapshot hook, so DI/JIT/SDK initialization is captured in the snapshot rather than paid
/// after restore. Registration is FAIL-OPEN: a runtime without snapshot-hook support never throws or breaks
/// host startup. These locks bind that contract — registration never throws and never logs an error, the
/// null-argument guards hold, and an observable hook outcome (registered, or fail-open warning) is logged.
/// Non-vacuous: the type and behavior did not exist before the fix (RED — compile failure pre-fix).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "ColdStart")]
public sealed class AwsLambdaSnapStartHooksShould : UnitTestBase
{
    private const int RegisteredEventId = 50168;        // AwsLambdaEventId.SnapStartHookRegistered
    private const int RegistrationFailedEventId = 50170; // AwsLambdaEventId.SnapStartHookRegistrationFailed

    [Fact]
    public void RegisterWarmup_DoesNotThrow_AndLogsHookOutcome_FailOpen()
    {
        // Arrange — mock optimizer; the warmup delegate is only invoked by a real snapshot, never here.
        var optimizer = A.Fake<IColdStartOptimizer>();
        var logger = new CapturingLogger<ColdStartOptimizerBase>();

        // Act — registering the before-snapshot hook must NEVER throw (fail-open).
        Should.NotThrow(() => AwsLambdaSnapStartHooks.RegisterWarmup(optimizer, logger));

        // Assert — an observable hook outcome is logged: either the success registration (50168) or the
        // fail-open skip warning (50170) when the runtime lacks snapshot-hook support. Never an error.
        logger.Entries.ShouldContain(e =>
            e.EventId.Id == RegisteredEventId || e.EventId.Id == RegistrationFailedEventId);
        logger.HasLoggedError().ShouldBeFalse();
    }

    [Fact]
    public void RegisterWarmup_Throws_WhenOptimizerIsNull()
    {
        var logger = new CapturingLogger<ColdStartOptimizerBase>();

        _ = Should.Throw<ArgumentNullException>(
            () => AwsLambdaSnapStartHooks.RegisterWarmup(null!, logger));
    }

    [Fact]
    public void RegisterWarmup_Throws_WhenLoggerIsNull()
    {
        var optimizer = A.Fake<IColdStartOptimizer>();

        _ = Should.Throw<ArgumentNullException>(
            () => AwsLambdaSnapStartHooks.RegisterWarmup(optimizer, null!));
    }
}
