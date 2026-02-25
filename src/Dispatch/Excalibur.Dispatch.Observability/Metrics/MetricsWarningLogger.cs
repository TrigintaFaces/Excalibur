// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Provides logging utilities for metrics configuration warnings.
/// </summary>
public static class MetricsWarningLogger
{
	private static readonly CompositeFormat WarningFormat =
			CompositeFormat.Parse(Resources.MetricsWarningLogger_WarningFormat);
	private static readonly CompositeFormat DebugFormat =
			CompositeFormat.Parse(Resources.MetricsWarningLogger_DebugFormat);

	private static int _emitted;

	/// <summary>
	/// Emits a warning message only once about metrics configuration.
	/// </summary>
	public static void EmitOnce()
	{
		if (Interlocked.CompareExchange(ref _emitted, 1, 0) == 0)
		{
			Trace.WriteLine(Resources.MetricsWarningLogger_NoMetricsProviderConfigured);
		}
	}

	/// <summary>
	/// Emits a warning message about missing metrics configuration.
	/// </summary>
	/// <param name="message"> The custom warning message. </param>
	public static void EmitWarning(string message)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message);

		Trace.WriteLine(string.Format(
						CultureInfo.CurrentCulture,
						WarningFormat,
						message));
	}

	/// <summary>
	/// Emits a debug message about metrics configuration.
	/// </summary>
	/// <param name="message"> The debug message. </param>
	public static void EmitDebug(string message)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message);

#if DEBUG
		Trace.WriteLine(string.Format(
						CultureInfo.CurrentCulture,
						DebugFormat,
						message));
#endif
	}
}
