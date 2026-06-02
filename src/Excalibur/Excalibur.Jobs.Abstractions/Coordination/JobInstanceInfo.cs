// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Contains information about a job processing instance in the distributed system.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="JobInstanceInfo" /> class. </remarks>
/// <param name="instanceId"> The unique identifier for this instance. </param>
/// <param name="hostName"> The hostname where this instance is running. </param>
/// <param name="capabilities"> The job processing capabilities of this instance. </param>
public sealed class JobInstanceInfo(string instanceId, string hostName, JobInstanceCapabilities capabilities)
{
	/// <summary>
	/// Gets the unique identifier for this instance.
	/// </summary>
	/// <value>
	/// The unique identifier for this instance.
	/// </value>
	public string InstanceId { get; } = instanceId ?? throw new ArgumentNullException(nameof(instanceId));

	/// <summary>
	/// Gets the hostname where this instance is running.
	/// </summary>
	/// <value>
	/// The hostname where this instance is running.
	/// </value>
	public string HostName { get; } = hostName ?? throw new ArgumentNullException(nameof(hostName));

	/// <summary>
	/// Gets the job processing capabilities of this instance.
	/// </summary>
	/// <value>
	/// The job processing capabilities of this instance.
	/// </value>
	public JobInstanceCapabilities Capabilities { get; } = capabilities ?? throw new ArgumentNullException(nameof(capabilities));

	/// <summary>
	/// Gets the time when this instance was registered.
	/// </summary>
	/// <value>
	/// The time when this instance was registered.
	/// </value>
	public DateTimeOffset RegisteredAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the time of the last heartbeat from this instance.
	/// </summary>
	/// <value>
	/// The time of the last heartbeat from this instance.
	/// </value>
	public DateTimeOffset LastHeartbeat { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the current status of this instance.
	/// </summary>
	/// <value>
	/// The current status of this instance.
	/// </value>
	public JobInstanceStatus Status { get; set; } = JobInstanceStatus.Active;

	/// <summary>
	/// Gets or sets the number of jobs currently being processed by this instance.
	/// </summary>
	/// <value>
	/// The number of jobs currently being processed by this instance.
	/// </value>
	public int ActiveJobCount { get; set; }

	/// <summary>
	/// Gets or sets additional metadata about this instance in JSON format.
	/// </summary>
	/// <value>
	/// Additional metadata about this instance in JSON format.
	/// </value>
	public string? Metadata { get; set; }

	/// <summary>
	/// Updates the heartbeat timestamp to indicate the instance is still alive.
	/// </summary>
	public void UpdateHeartbeat() => LastHeartbeat = DateTimeOffset.UtcNow;

	/// <summary>
	/// Checks if this instance is considered healthy based on the heartbeat timeout.
	/// </summary>
	/// <param name="heartbeatTimeout"> The maximum time allowed since the last heartbeat. </param>
	/// <returns> True if the instance is healthy, false otherwise. </returns>
	public bool IsHealthy(TimeSpan heartbeatTimeout) =>
		Status == JobInstanceStatus.Active &&
		DateTimeOffset.UtcNow - LastHeartbeat <= heartbeatTimeout;
}
