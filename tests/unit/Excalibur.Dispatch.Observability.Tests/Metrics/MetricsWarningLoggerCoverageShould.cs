// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class MetricsWarningLoggerCoverageShould
{
    [Fact]
    public void EmitWarningWithMessage()
    {
        // Act & Assert - should not throw
        MetricsWarningLogger.EmitWarning("Test warning message");
    }

    [Fact]
    public void ThrowForNullWarningMessage()
    {
        Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitWarning(null!));
    }

    [Fact]
    public void ThrowForEmptyWarningMessage()
    {
        Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitWarning(""));
    }

    [Fact]
    public void ThrowForWhitespaceWarningMessage()
    {
        Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitWarning("   "));
    }

    [Fact]
    public void EmitDebugWithMessage()
    {
        // Act & Assert - should not throw
        MetricsWarningLogger.EmitDebug("Debug message");
    }

    [Fact]
    public void ThrowForNullDebugMessage()
    {
        Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitDebug(null!));
    }

    [Fact]
    public void ThrowForEmptyDebugMessage()
    {
        Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitDebug(""));
    }

    [Fact]
    public void EmitOnceIsIdempotent()
    {
        // Act & Assert - calling multiple times should not throw
        MetricsWarningLogger.EmitOnce();
        MetricsWarningLogger.EmitOnce();
    }
}
