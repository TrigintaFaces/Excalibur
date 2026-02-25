// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Specifies the monitoring verbosity level for Elasticsearch operations.
/// </summary>
public enum MonitoringLevel
{
	/// <summary>
	/// Minimal monitoring with only essential metrics.
	/// </summary>
	Minimal = 0,

	/// <summary>
	/// Standard monitoring with balanced metrics and logging.
	/// </summary>
	Standard = 1,

	/// <summary>
	/// Verbose monitoring with detailed logging and comprehensive metrics.
	/// </summary>
	Verbose = 2,
}
