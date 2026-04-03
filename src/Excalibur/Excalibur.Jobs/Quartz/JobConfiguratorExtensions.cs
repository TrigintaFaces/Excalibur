// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;

namespace Excalibur.Jobs.Quartz;

/// <summary>
/// Extension methods for <see cref="IJobConfigurator"/> providing convenience scheduling methods.
/// </summary>
public static class JobConfiguratorExtensions
{
	/// <summary>
	/// Adds a recurring job that executes at a specified interval.
	/// Converts the interval to a cron expression and delegates to <see cref="IJobConfigurator.AddJob{TJob}"/>.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="configurator"> The job configurator. </param>
	/// <param name="interval"> The interval between job executions. </param>
	/// <param name="jobKey"> Optional unique key for the job. If not provided, the job type name will be used. </param>
	/// <returns> The job configurator for chaining. </returns>
	public static IJobConfigurator AddRecurringJob<TJob>(this IJobConfigurator configurator, TimeSpan interval, string? jobKey = null)
		where TJob : class, IBackgroundJob
	{
		ArgumentNullException.ThrowIfNull(configurator);

		var cronExpression = IntervalToCron(interval);
		return configurator.AddJob<TJob>(cronExpression, jobKey);
	}

	/// <summary>
	/// Conditionally adds a job based on a predicate.
	/// </summary>
	/// <param name="configurator"> The job configurator. </param>
	/// <param name="condition"> The condition to evaluate. </param>
	/// <param name="configureJob"> Action to configure the job if the condition is true. </param>
	/// <returns> The job configurator for chaining. </returns>
	public static IJobConfigurator AddJobIf(this IJobConfigurator configurator, bool condition, Action<IJobConfigurator> configureJob)
	{
		ArgumentNullException.ThrowIfNull(configurator);
		ArgumentNullException.ThrowIfNull(configureJob);

		if (condition)
		{
			configureJob(configurator);
		}

		return configurator;
	}

	/// <summary>
	/// Adds multiple instances of the same job type with different configurations.
	/// </summary>
	/// <typeparam name="TJob"> The job implementation type. </typeparam>
	/// <param name="configurator"> The job configurator. </param>
	/// <param name="configurations"> Array of job configurations. </param>
	/// <returns> The job configurator for chaining. </returns>
	public static IJobConfigurator AddJobInstances<TJob>(this IJobConfigurator configurator, params QuartzJobOptions[] configurations)
		where TJob : class, IBackgroundJob
	{
		ArgumentNullException.ThrowIfNull(configurator);
		ArgumentNullException.ThrowIfNull(configurations);

		foreach (var config in configurations.Where(static c => c.Enabled))
		{
			_ = configurator.AddJob<TJob>(config.CronExpression, config.JobKey);
		}

		return configurator;
	}

	/// <summary>
	/// Converts a <see cref="TimeSpan"/> interval to a cron expression.
	/// </summary>
	private static string IntervalToCron(TimeSpan interval)
	{
		if (interval.TotalSeconds < 60)
		{
			return $"*/{(int)interval.TotalSeconds} * * * * ?";
		}

		if (interval.TotalMinutes < 60)
		{
			return $"0 */{(int)interval.TotalMinutes} * * * ?";
		}

		if (interval.TotalHours < 24)
		{
			return $"0 0 */{(int)interval.TotalHours} * * ?";
		}

		return $"0 0 0 */{(int)interval.TotalDays} * ?";
	}
}
