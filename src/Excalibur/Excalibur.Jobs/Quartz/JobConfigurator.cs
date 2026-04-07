// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
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
public class JobConfigurator(IServiceCollection services) : IJobConfigurator
{
	private readonly IServiceCollection _services = services ?? throw new ArgumentNullException(nameof(services));

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Job types are registered at startup via AddQuartz and resolved by Quartz at runtime.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Job types are registered at startup via AddQuartz and resolved by Quartz at runtime.")]
	public IJobConfigurator AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(
		string cronExpression, string? jobKey = null)
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
	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Job types are registered at startup via AddQuartz and resolved by Quartz at runtime. JsonSerializer.Serialize is used for context serialization.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Job types are registered at startup via AddQuartz and resolved by Quartz at runtime. JsonSerializer.Serialize is used for context serialization.")]
	public IJobConfigurator AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob, TContext>(
		string cronExpression, TContext context, string? jobKey = null)
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
	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Job types are registered at startup via AddQuartz and resolved by Quartz at runtime.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Job types are registered at startup via AddQuartz and resolved by Quartz at runtime.")]
	public IJobConfigurator AddOneTimeJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(
		string? jobKey = null)
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
	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Job types are registered at startup via AddQuartz and resolved by Quartz at runtime.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Job types are registered at startup via AddQuartz and resolved by Quartz at runtime.")]
	public IJobConfigurator AddDelayedJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(
		TimeSpan delay, string? jobKey = null)
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

}
