// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricsWarningLogger"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class MetricsWarningLoggerShould : UnitTestBase
{
	#region EmitWarning Tests

	[Fact]
	public void EmitWarning_ThrowOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitWarning(null!));
	}

	[Fact]
	public void EmitWarning_ThrowOnEmptyMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitWarning(""));
	}

	[Fact]
	public void EmitWarning_ThrowOnWhitespaceMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitWarning("   "));
	}

	[Fact]
	public void EmitWarning_NotThrowWithValidMessage()
	{
		// Capture console output
		var originalOut = Console.Out;
		try
		{
			using var writer = new StringWriter();
			Console.SetOut(writer);

			// Act & Assert - Should not throw
			Should.NotThrow(() =>
				MetricsWarningLogger.EmitWarning("Test warning message"));
		}
		finally
		{
			Console.SetOut(originalOut);
		}
	}

	#endregion

	#region EmitDebug Tests

	[Fact]
	public void EmitDebug_ThrowOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitDebug(null!));
	}

	[Fact]
	public void EmitDebug_ThrowOnEmptyMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitDebug(""));
	}

	[Fact]
	public void EmitDebug_ThrowOnWhitespaceMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			MetricsWarningLogger.EmitDebug("   "));
	}

	[Fact]
	public void EmitDebug_NotThrowWithValidMessage()
	{
		// Capture console output
		var originalOut = Console.Out;
		try
		{
			using var writer = new StringWriter();
			Console.SetOut(writer);

			// Act & Assert - Should not throw
			Should.NotThrow(() =>
				MetricsWarningLogger.EmitDebug("Test debug message"));
		}
		finally
		{
			Console.SetOut(originalOut);
		}
	}

	#endregion

	#region EmitOnce Tests

	[Fact]
	public void EmitOnce_NotThrow()
	{
		// Capture console output
		var originalOut = Console.Out;
		try
		{
			using var writer = new StringWriter();
			Console.SetOut(writer);

			// Act & Assert - Should not throw
			Should.NotThrow(() => MetricsWarningLogger.EmitOnce());
		}
		finally
		{
			Console.SetOut(originalOut);
		}
	}

	[Fact]
	public void EmitOnce_AllowMultipleCalls()
	{
		// Capture console output
		var originalOut = Console.Out;
		try
		{
			using var writer = new StringWriter();
			Console.SetOut(writer);

			// Act & Assert - Multiple calls should not throw
			Should.NotThrow(() =>
			{
				MetricsWarningLogger.EmitOnce();
				MetricsWarningLogger.EmitOnce();
				MetricsWarningLogger.EmitOnce();
			});
		}
		finally
		{
			Console.SetOut(originalOut);
		}
	}

	#endregion
}
