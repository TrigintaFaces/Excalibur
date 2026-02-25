// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

using Excalibur.Jobs.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using Quartz;

namespace Excalibur.Jobs.Quartz;

/// <summary>
/// Implementation of <see cref="IJobConfigurator" /> that provides a fluent API for configuring individual jobs.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="JobConfigurator" /> class. </remarks>
/// <param name="services"> The service collection to configure jobs in. </param>
public sealed class JobConfigurator(IServiceCollection services) : IJobConfigurator
{
	private readonly IServiceCollection _services = services ?? throw new ArgumentNullException(nameof(services));

	/// <inheritdoc />
	public IJobConfigurator AddJob<TJob>(string cronExpression, string? jobKey = null)
		where TJob : class, IBackgroundJob
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

		var key = jobKey ?? typeof(TJob).Name;

		// Register the job type
		_ = _services.AddTransient<TJob>();

		// Configure the job in Quartz
		_ = _services.AddQuartz(q =>
		{
			var quartzJobKey = new JobKey(key);

			_ = q.AddJob<QuartzJobAdapter>(opts =>
			{
				_ = opts.WithIdentity(quartzJobKey);
				_ = opts.UsingJobData("JobType", typeof(TJob).AssemblyQualifiedName ?? typeof(TJob).FullName ?? typeof(TJob).Name);
			});

			_ = q.AddTrigger(opts => _ = opts.ForJob(quartzJobKey)
				.WithIdentity($"{key}-trigger")
				.WithCronSchedule(cronExpression));
		});

		return this;
	}

	/// <inheritdoc />
	public IJobConfigurator AddJob<TJob, TContext>(string cronExpression, TContext context, string? jobKey = null)
		where TJob : class, IBackgroundJob<TContext>
		where TContext : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);
		ArgumentNullException.ThrowIfNull(context);

		var key = jobKey ?? typeof(TJob).Name;

		// Register the job type
		_ = _services.AddTransient<TJob>();

		// Configure the job in Quartz
		_ = _services.AddQuartz(q =>
		{
			var quartzJobKey = new JobKey(key);

			_ = q.AddJob<QuartzGenericJobAdapter<TJob, TContext>>(opts =>
			{
				_ = opts.WithIdentity(quartzJobKey);
				_ = opts.UsingJobData(
					"ContextType",
					typeof(TContext).AssemblyQualifiedName ?? typeof(TContext).FullName ?? typeof(TContext).Name);
				_ = opts.UsingJobData("ContextData", JsonSerializer.Serialize(context));
			});

			_ = q.AddTrigger(opts => _ = opts.ForJob(quartzJobKey)
				.WithIdentity($"{key}-trigger")
				.WithCronSchedule(cronExpression));
		});

		return this;
	}

	/// <inheritdoc />
	public IJobConfigurator AddRecurringJob<TJob>(TimeSpan interval, string? jobKey = null)
		where TJob : class, IBackgroundJob
	{
		var key = jobKey ?? typeof(TJob).Name;

		// Register the job type
		_ = _services.AddTransient<TJob>();

		// Configure the job in Quartz
		_ = _services.AddQuartz(q =>
		{
			var quartzJobKey = new JobKey(key);

			_ = q.AddJob<QuartzJobAdapter>(opts =>
			{
				_ = opts.WithIdentity(quartzJobKey);
				_ = opts.UsingJobData("JobType", typeof(TJob).AssemblyQualifiedName ?? typeof(TJob).FullName ?? typeof(TJob).Name);
			});

			_ = q.AddTrigger(opts => _ = opts.ForJob(quartzJobKey)
				.WithIdentity($"{key}-trigger")
				.WithSimpleSchedule(x => x
					.WithInterval(interval)
					.RepeatForever()));
		});

		return this;
	}

	/// <inheritdoc />
	public IJobConfigurator AddOneTimeJob<TJob>(string? jobKey = null)
		where TJob : class, IBackgroundJob
	{
		var key = jobKey ?? typeof(TJob).Name;

		// Register the job type
		_ = _services.AddTransient<TJob>();

		// Configure the job in Quartz
		_ = _services.AddQuartz(q =>
		{
			var quartzJobKey = new JobKey(key);

			_ = q.AddJob<QuartzJobAdapter>(opts =>
			{
				_ = opts.WithIdentity(quartzJobKey);
				_ = opts.UsingJobData("JobType", typeof(TJob).AssemblyQualifiedName ?? typeof(TJob).FullName ?? typeof(TJob).Name);
			});

			_ = q.AddTrigger(opts => _ = opts.ForJob(quartzJobKey)
				.WithIdentity($"{key}-trigger")
				.StartNow());
		});

		return this;
	}

	/// <inheritdoc />
	public IJobConfigurator AddDelayedJob<TJob>(TimeSpan delay, string? jobKey = null)
		where TJob : class, IBackgroundJob
	{
		var key = jobKey ?? typeof(TJob).Name;

		// Register the job type
		_ = _services.AddTransient<TJob>();

		// Configure the job in Quartz
		_ = _services.AddQuartz(q =>
		{
			var quartzJobKey = new JobKey(key);

			_ = q.AddJob<QuartzJobAdapter>(opts =>
			{
				_ = opts.WithIdentity(quartzJobKey);
				_ = opts.UsingJobData("JobType", typeof(TJob).AssemblyQualifiedName ?? typeof(TJob).FullName ?? typeof(TJob).Name);
			});

			_ = q.AddTrigger(opts => _ = opts.ForJob(quartzJobKey)
				.WithIdentity($"{key}-trigger")
				.StartAt(DateTimeOffset.UtcNow.Add(delay)));
		});

		return this;
	}

	/// <inheritdoc />
	public IJobConfigurator AddJobIf(bool condition, Action<IJobConfigurator> configureJob)
	{
		ArgumentNullException.ThrowIfNull(configureJob);

		if (condition)
		{
			configureJob(this);
		}

		return this;
	}

	/// <inheritdoc />
	public IJobConfigurator AddJobInstances<TJob>(params JobConfiguration[] configurations)
		where TJob : class, IBackgroundJob
	{
		ArgumentNullException.ThrowIfNull(configurations);

		foreach (var config in configurations.Where(static c => c.Enabled))
		{
			_ = AddJob<TJob>(config.CronExpression, config.JobKey);
		}

		return this;
	}
}
