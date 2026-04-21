// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the complete contract for real-time security monitoring of Elasticsearch operations including threat detection, anomaly analysis,
/// and automated security response capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This interface composes four focused sub-interfaces for consumers that need only a subset of capabilities:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="IElasticsearchSecurityMonitoring"/> — Lifecycle operations (start, stop, status).</description></item>
/// <item><description><see cref="IElasticsearchSecurityAnalysis"/> — Event analysis and threat detection.</description></item>
/// <item><description><see cref="IElasticsearchSecurityAlerting"/> — Alert processing, risk scoring, and automated response.</description></item>
/// <item><description><see cref="IElasticsearchSecurityMonitorEvents"/> — Events and threat intelligence updates.</description></item>
/// </list>
/// <para>
/// Consumers that need the full security monitoring surface can depend on this interface.
/// Consumers that need only a specific capability should depend on the focused sub-interface instead.
/// </para>
/// </remarks>
public interface IElasticsearchSecurityMonitor
	: IElasticsearchSecurityMonitoring,
	  IElasticsearchSecurityAnalysis,
	  IElasticsearchSecurityAlerting,
	  IElasticsearchSecurityMonitorEvents
{
}
