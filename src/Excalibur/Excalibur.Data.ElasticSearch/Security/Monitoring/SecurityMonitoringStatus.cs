// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the security monitoring status.
/// </summary>
public sealed class SecurityMonitoringStatus
{
	/// <summary>
	/// Gets or sets a value indicating whether security monitoring is currently active.
	/// </summary>
	/// <value> True if monitoring is active, false otherwise. </value>
	public bool IsActive { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether monitoring is currently running.
	/// </summary>
	/// <value>
	/// A value indicating whether monitoring is currently running.
	/// </value>
	public bool IsMonitoring { get; set; }

	/// <summary>
	/// Gets or sets the monitoring configuration.
	/// </summary>
	/// <value>
	/// The monitoring configuration.
	/// </value>
	public MonitoringConfiguration Configuration { get; set; } = new();

	/// <summary>
	/// Gets or sets the status timestamp.
	/// </summary>
	/// <value>
	/// The status timestamp.
	/// </value>
	public DateTimeOffset StatusTimestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets a value indicating whether Elasticsearch is healthy.
	/// </summary>
	/// <value>
	/// A value indicating whether Elasticsearch is healthy.
	/// </value>
	public bool ElasticsearchHealthy { get; set; }

	/// <summary>
	/// Gets or sets the Elasticsearch status message.
	/// </summary>
	/// <value>
	/// The Elasticsearch status message.
	/// </value>
	public string ElasticsearchStatus { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the monitoring statistics.
	/// </summary>
	/// <value>
	/// The monitoring statistics.
	/// </value>
	public MonitoringStatistics Statistics { get; set; } = new();

	/// <summary>
	/// Gets or sets the timestamp when security monitoring was started.
	/// </summary>
	/// <value> The start timestamp, or null if monitoring is not active. </value>
	public DateTimeOffset? StartedAt { get; set; }

	/// <summary>
	/// Gets or sets the collection of health indicators for various monitoring components.
	/// </summary>
	/// <value> A dictionary containing health indicator names and their corresponding values. </value>
	public Dictionary<string, object> HealthIndicators { get; set; } = [];

	/// <summary>
	/// Represents monitoring configuration settings.
	/// </summary>
	public sealed class MonitoringConfiguration
	{
		/// <summary>
		/// Gets or sets a value indicating whether monitoring is enabled.
		/// </summary>
		/// <value>
		/// A value indicating whether monitoring is enabled.
		/// </value>
		public bool Enabled { get; set; }

		/// <summary>
		/// Gets or sets the monitoring interval.
		/// </summary>
		/// <value>
		/// The monitoring interval.
		/// </value>
		public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

		/// <summary>
		/// Gets or sets the monitoring configuration settings.
		/// </summary>
		/// <value>
		/// The monitoring configuration settings.
		/// </value>
		public Dictionary<string, object> Settings { get; set; } = [];
	}

	/// <summary>
	/// Represents monitoring statistics.
	/// </summary>
	public sealed class MonitoringStatistics
	{
		/// <summary>
		/// Gets or sets the number of events processed.
		/// </summary>
		/// <value>
		/// The number of events processed.
		/// </value>
		public long EventsProcessed { get; set; }

		/// <summary>
		/// Gets or sets the number of alerts generated.
		/// </summary>
		/// <value>
		/// The number of alerts generated.
		/// </value>
		public long AlertsGenerated { get; set; }

		/// <summary>
		/// Gets or sets the number of threats detected.
		/// </summary>
		/// <value>
		/// The number of threats detected.
		/// </value>
		public long ThreatsDetected { get; set; }

		/// <summary>
		/// Gets or sets the number of anomalies detected.
		/// </summary>
		/// <value>
		/// The number of anomalies detected.
		/// </value>
		public long AnomaliesDetected { get; set; }

		/// <summary>
		/// Gets or sets the timestamp of the last event processed.
		/// </summary>
		/// <value>
		/// The timestamp of the last event processed.
		/// </value>
		public DateTimeOffset LastEventTime { get; set; }
	}
}
