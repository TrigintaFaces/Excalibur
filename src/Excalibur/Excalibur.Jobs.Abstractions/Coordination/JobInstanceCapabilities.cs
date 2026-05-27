// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Describes the job processing capabilities of an instance.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="JobInstanceCapabilities" /> class. </remarks>
/// <param name="maxConcurrentJobs"> The maximum number of jobs this instance can process concurrently. </param>
/// <param name="supportedJobTypes"> The types of jobs this instance can process. </param>
public sealed class JobInstanceCapabilities(int maxConcurrentJobs, IEnumerable<string> supportedJobTypes)
{
	/// <summary>
	/// Gets the maximum number of jobs this instance can process concurrently.
	/// </summary>
	/// <value>
	/// The maximum number of jobs this instance can process concurrently.
	/// </value>
	public int MaxConcurrentJobs { get; } =
		maxConcurrentJobs > 0 ? maxConcurrentJobs : throw new ArgumentOutOfRangeException(nameof(maxConcurrentJobs));

	/// <summary>
	/// Gets the types of jobs this instance can process.
	/// </summary>
	/// <value>
	/// The types of jobs this instance can process.
	/// </value>
	public IReadOnlySet<string> SupportedJobTypes { get; } =
		new HashSet<string>(supportedJobTypes ?? throw new ArgumentNullException(nameof(supportedJobTypes)), StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the priority of this instance for job assignment (higher values = higher priority).
	/// </summary>
	/// <value>
	/// The priority of this instance for job assignment (higher values = higher priority).
	/// </value>
	public int Priority { get; set; } = 1;

	/// <summary>
	/// Gets tags that describe this instance's characteristics.
	/// </summary>
	/// <value>
	/// Tags that describe this instance's characteristics.
	/// </value>
	public ISet<string> Tags { get; } = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>
	/// Checks if this instance can process the specified job type.
	/// </summary>
	/// <param name="jobType"> The job type to check. </param>
	/// <returns> True if the instance can process this job type, false otherwise. </returns>
	public bool CanProcess(string jobType) => SupportedJobTypes.Contains(jobType) || SupportedJobTypes.Contains("*");
}
