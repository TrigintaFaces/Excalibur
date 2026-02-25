// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.Observability.Aws;

/// <summary>
/// Event IDs for AWS observability integration diagnostics (93550-93569).
/// </summary>
public static class AwsObservabilityEventId
{
	/// <summary>AWS X-Ray integration configured.</summary>
	public const int XRayConfigured = 93550;

	/// <summary>AWS CloudWatch metrics integration configured.</summary>
	public const int CloudWatchMetricsConfigured = 93551;

	/// <summary>AWS X-Ray configuration failed.</summary>
	public const int XRayConfigurationFailed = 93555;

	/// <summary>AWS CloudWatch metrics configuration failed.</summary>
	public const int CloudWatchMetricsConfigurationFailed = 93556;
}
