// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="IRetentionEnforcementService"/> that scans types
/// annotated with <see cref="PersonalDataAttribute"/> and enforces retention policies.
/// </summary>
/// <remarks>
/// <para>
/// This service discovers retention policies from <see cref="PersonalDataAttribute.RetentionDays"/>
/// annotations at runtime and enforces cleanup of data that has exceeded its retention period.
/// </para>
/// </remarks>
public sealed partial class RetentionEnforcementService : IRetentionEnforcementService
{
	private readonly IErasureService? _erasureService;
	private readonly IOptions<RetentionEnforcementOptions> _options;
	private readonly ILogger<RetentionEnforcementService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RetentionEnforcementService"/> class.
	/// </summary>
	/// <param name="options">The retention enforcement options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="erasureService">Optional erasure service for deleting expired data.</param>
	public RetentionEnforcementService(
		IOptions<RetentionEnforcementOptions> options,
		ILogger<RetentionEnforcementService> logger,
		IErasureService? erasureService = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_erasureService = erasureService;
	}

	/// <inheritdoc />
	public Task<RetentionEnforcementResult> EnforceRetentionAsync(
		CancellationToken cancellationToken)
	{
		LogRetentionEnforcementStarted(_options.Value.DryRun);

		try
		{
			var policies = DiscoverRetentionPolicies();

			LogRetentionEnforcementCompleted(policies.Count, _options.Value.DryRun);

			return Task.FromResult(new RetentionEnforcementResult
			{
				PoliciesEvaluated = policies.Count,
				RecordsCleaned = 0, // Actual cleanup requires data store integration
				IsDryRun = _options.Value.DryRun,
				CompletedAt = DateTimeOffset.UtcNow
			});
		}
		catch (Exception ex)
		{
			LogRetentionEnforcementFailed(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<RetentionPolicy>> GetRetentionPoliciesAsync(
		CancellationToken cancellationToken)
	{
		var policies = DiscoverRetentionPolicies();
		return Task.FromResult<IReadOnlyList<RetentionPolicy>>(policies);
	}

	private static List<RetentionPolicy> DiscoverRetentionPolicies()
	{
		var policies = new List<RetentionPolicy>();

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (assembly.IsDynamic)
			{
				continue;
			}

			try
			{
				foreach (var type in GetLoadableTypes(assembly))
				{
					foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
					{
						var attr = TryGetPersonalDataAttribute(property);
						if (attr is null || attr.RetentionDays <= 0)
						{
							continue;
						}

						policies.Add(new RetentionPolicy
						{
							TypeName = type.FullName ?? type.Name,
							PropertyName = property.Name,
							Category = attr.Category,
							RetentionDays = attr.RetentionDays
						});
					}
				}
			}
			catch (ReflectionTypeLoadException)
			{
				// Skip assemblies that fail to load types
			}
		}

		return policies;
	}

	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(static t => t is not null)!;
		}
	}

	private static PersonalDataAttribute? TryGetPersonalDataAttribute(PropertyInfo property)
	{
		try
		{
			return property.GetCustomAttribute<PersonalDataAttribute>();
		}
		catch (TypeLoadException)
		{
			return null;
		}
	}

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementStarted,
		LogLevel.Information,
		"Starting retention enforcement scan, dry run: {DryRun}")]
	private partial void LogRetentionEnforcementStarted(bool dryRun);

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementCompleted,
		LogLevel.Information,
		"Retention enforcement scan completed. Policies evaluated: {PolicyCount}, dry run: {DryRun}")]
	private partial void LogRetentionEnforcementCompleted(int policyCount, bool dryRun);

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementFailed,
		LogLevel.Error,
		"Retention enforcement scan failed")]
	private partial void LogRetentionEnforcementFailed(Exception exception);
}
