// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricsWarningLogger"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class MetricsWarningLoggerShould
{
	[Fact]
	public void EmitWarning_WithValidMessage()
	{
		// Act & Assert — should not throw
		MetricsWarningLogger.EmitWarning("Test warning message");
	}

	[Fact]
	public void EmitWarning_ThrowsOnNullMessage()
	{
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitWarning(null!));
	}

	[Fact]
	public void EmitWarning_ThrowsOnWhitespaceMessage()
	{
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitWarning("   "));
	}

	[Fact]
	public void EmitDebug_WithValidMessage()
	{
		// Act & Assert — should not throw
		MetricsWarningLogger.EmitDebug("Test debug message");
	}

	[Fact]
	public void EmitDebug_ThrowsOnNullMessage()
	{
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitDebug(null!));
	}

	[Fact]
	public void EmitDebug_ThrowsOnWhitespaceMessage()
	{
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitDebug("   "));
	}
}
