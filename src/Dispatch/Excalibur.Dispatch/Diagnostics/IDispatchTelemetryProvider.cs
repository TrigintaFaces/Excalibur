// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Interface for providing telemetry components across the Excalibur framework.
/// </summary>
/// <remarks>
/// Centralizes access to ActivitySource and Meter instances for consistent telemetry collection across all enhanced stores and pipeline components.
/// </remarks>
public interface IDispatchTelemetryProvider
{
	/// <summary>
	/// Gets the ActivitySource for the specified component.
	/// </summary>
	/// <param name="componentName"> The component name from DispatchTelemetryConstants.ActivitySources. </param>
	/// <returns> The ActivitySource instance for distributed tracing. </returns>
	ActivitySource GetActivitySource(string componentName);

	/// <summary>
	/// Gets the Meter for the specified component.
	/// </summary>
	/// <param name="componentName"> The component name from DispatchTelemetryConstants.Meters. </param>
	/// <returns> The Meter instance for metrics collection. </returns>
	Meter GetMeter(string componentName);

	/// <summary>
	/// Gets the current telemetry options.
	/// </summary>
	/// <returns> The configured telemetry options. </returns>
	DispatchTelemetryOptions GetOptions();
}
