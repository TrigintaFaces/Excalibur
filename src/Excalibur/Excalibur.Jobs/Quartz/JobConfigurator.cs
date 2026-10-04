// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

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
	public IJobConfigurator AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(
		string cronExpression, string? jobKey = null)
		where TJob : class, IBackgroundJob
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

		var key = jobKey ?? typeof(TJob).Name;

		// Register the job type and its persisted identity without runtime type discovery.
		_ = _services.AddTransient<TJob>();
		_ = _services.AddSingleton(new RegisteredJobType(typeof(TJob)));

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
		Justification = "The job context is serialized as JSON without type metadata. AOT consumers must preserve the context type.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "The job context is serialized as JSON without type metadata. AOT consumers must preserve the context type.")]
	public IJobConfigurator AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob, TContext>(
		string cronExpression, TContext context, string? jobKey = null)
		where TJob : class, IBackgroundJob<TContext>
		where TContext : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);
		ArgumentNullException.ThrowIfNull(context);

		if (!JsonSerializer.IsReflectionEnabledByDefault)
		{
			throw new InvalidOperationException("Supply JsonTypeInfo<TContext> when registering a job in a trimmed or Native AOT application.");
		}

		return AddContextJob<TJob, TContext>(cronExpression, JsonSerializer.Serialize(context), jobKey, contextVersion: null);
	}

	/// <inheritdoc />
	public IJobConfigurator AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob, TContext>(
		string cronExpression, TContext context, JsonTypeInfo<TContext> typeInfo, string? jobKey = null, string contextVersion = "1")
		where TJob : class, IBackgroundJob<TContext>
		where TContext : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(typeInfo);
		ArgumentException.ThrowIfNullOrWhiteSpace(contextVersion);
		var key = new JobKey(jobKey ?? typeof(TJob).Name);
		var metadataKey = (typeof(TJob), key, contextVersion);
		if (_services.Any(d => d.IsKeyedService && d.ServiceType == typeof(JsonTypeInfo<TContext>) && Equals(d.ServiceKey, metadataKey)))
		{
			throw new InvalidOperationException($"JSON metadata is already registered for job '{key}'.");
		}

		var json = JsonSerializer.Serialize(context, typeInfo);
		_ = _services.AddKeyedSingleton(metadataKey, typeInfo);
		return AddContextJob<TJob, TContext>(cronExpression, json, jobKey, contextVersion);
	}

	private IJobConfigurator AddContextJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob, TContext>(
		string cronExpression, string json, string? jobKey, string? contextVersion)
		where TJob : class, IBackgroundJob<TContext>
		where TContext : class
	{
		var key = jobKey ?? typeof(TJob).Name;
		// GetImplementationInstance(), never the raw getter: this very class registers a KEYED descriptor
		// (AddKeyedSingleton for the JSON metadata), and ServiceDescriptor.ImplementationInstance throws
		// InvalidOperationException when read on a keyed descriptor. Enumerating the collection with the
		// raw getter therefore crashes as soon as any keyed service is present, ours included.
		if (_services.Any(d => d.GetImplementationInstance() is RegisteredContextJob registration && registration.Key == key))
		{
			throw new InvalidOperationException($"A context job is already registered for '{key}'.");
		}
		_ = _services.AddSingleton(new RegisteredContextJob(key));
		_ = _services.AddTransient<TJob>();
		_ = _services.AddQuartz(q =>
		{
			var quartzJobKey = new JobKey(key);
			_ = q.AddJob<QuartzGenericJobAdapter<TJob, TContext>>(opts =>
			{
				_ = opts.WithIdentity(quartzJobKey);
				_ = opts.UsingJobData("ContextType", typeof(TContext).AssemblyQualifiedName ?? typeof(TContext).FullName ?? typeof(TContext).Name);
				_ = opts.UsingJobData("ContextData", json);
				_ = opts.UsingJobData("ContextSerialization", contextVersion is null ? "reflection-v1" : "metadata-v1:" + contextVersion);
			});
			_ = q.AddTrigger(opts => _ = opts.ForJob(quartzJobKey)
				.WithIdentity($"{key}-trigger").WithCronSchedule(cronExpression));
		});
		return this;
	}

	/// <inheritdoc />
	public IJobConfigurator AddOneTimeJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(
		string? jobKey = null)
		where TJob : class, IBackgroundJob
	{
		var key = jobKey ?? typeof(TJob).Name;

		// Register the job type and its persisted identity without runtime type discovery.
		_ = _services.AddTransient<TJob>();
		_ = _services.AddSingleton(new RegisteredJobType(typeof(TJob)));

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
	public IJobConfigurator AddDelayedJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(
		TimeSpan delay, string? jobKey = null)
		where TJob : class, IBackgroundJob
	{
		var key = jobKey ?? typeof(TJob).Name;

		// Register the job type and its persisted identity without runtime type discovery.
		_ = _services.AddTransient<TJob>();
		_ = _services.AddSingleton(new RegisteredJobType(typeof(TJob)));

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
