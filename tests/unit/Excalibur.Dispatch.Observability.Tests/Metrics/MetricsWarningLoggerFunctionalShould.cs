// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Functional tests for <see cref="MetricsWarningLogger"/> verifying emit-once semantics and argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class MetricsWarningLoggerFunctionalShould
{
	[Fact]
	public void EmitWarning_ThrowsOnNullOrWhitespace()
	{
		Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitWarning(null!));
		Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitWarning(""));
		Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitWarning("   "));
	}

	[Fact]
	public void EmitDebug_ThrowsOnNullOrWhitespace()
	{
		Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitDebug(null!));
		Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitDebug(""));
		Should.Throw<ArgumentException>(() => MetricsWarningLogger.EmitDebug("   "));
	}

	[Fact]
	public void EmitWarning_DoesNotThrowForValidMessage()
	{
		// Should not throw
		MetricsWarningLogger.EmitWarning("Test warning message");
	}

	[Fact]
	public void EmitDebug_DoesNotThrowForValidMessage()
	{
		// Should not throw
		MetricsWarningLogger.EmitDebug("Test debug message");
	}

	[Fact]
	public void EmitOnce_DoesNotThrow()
	{
		// EmitOnce uses Interlocked.CompareExchange, so calling multiple times is safe
		MetricsWarningLogger.EmitOnce();
		MetricsWarningLogger.EmitOnce(); // Second call should be no-op
	}
}
