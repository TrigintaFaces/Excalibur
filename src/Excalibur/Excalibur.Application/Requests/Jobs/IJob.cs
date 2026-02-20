// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Application.Requests.Jobs;

/// <summary>
/// Represents a job that is correlatable, multi-tenant, and produces a <see cref="JobResult" /> response.
/// </summary>
public interface IJob : IAmCorrelatable, IAmMultiTenant, IDispatchAction<JobResult>
{
	/// <summary>
	/// Gets the type of the activity, which is typically <see cref="ActivityType.Job" /> for jobs.
	/// </summary>
	/// <value> The type of the activity, which is typically <see cref="ActivityType.Job" /> for jobs. </value>
	ActivityType ActivityType { get; }

	/// <summary>
	/// Gets the unique name of the job.
	/// </summary>
	/// <value> The unique name of the job. </value>
	/// <remarks> The name is typically a combination of the namespace and type name, providing a unique identifier for the job. </remarks>
	[JsonIgnore]
	string ActivityName { get; }

	/// <summary>
	/// Gets a human-readable display name of the job.
	/// </summary>
	/// <value> The human-readable display name of the job. </value>
	/// <remarks> The display name is suitable for use in logs, dashboards, or other user-facing contexts. </remarks>
	[JsonIgnore]
	string ActivityDisplayName { get; }

	/// <summary>
	/// Gets a description of the job's purpose or functionality.
	/// </summary>
	/// <value> The description of the job's purpose or functionality. </value>
	/// <remarks> The description provides additional context about the job and can be used for documentation or debugging. </remarks>
	[JsonIgnore]
	string ActivityDescription { get; }
}
