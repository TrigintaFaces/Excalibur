// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Retention;

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
	private readonly IOptions<RetentionEnforcementOptions> _options;
	private readonly ILogger<RetentionEnforcementService> _logger;
	private readonly IReadOnlyList<IRetentionContributor> _contributors;

	/// <summary>
	/// Initializes a new instance of the <see cref="RetentionEnforcementService"/> class.
	/// </summary>
	/// <param name="options">The retention enforcement options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="contributors">
	/// The registered store-specific retention contributors that perform the actual deletion of expired
	/// data. When none are registered, enforcement logs a warning and reports zero records cleaned.
	/// </param>
	public RetentionEnforcementService(
		IOptions<RetentionEnforcementOptions> options,
		ILogger<RetentionEnforcementService> logger,
		IEnumerable<IRetentionContributor>? contributors = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_contributors = contributors is null ? [] : [.. contributors];
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Uses AppDomain.GetAssemblies() and reflection to discover PersonalDataAttribute annotations at runtime.")]
	public async Task<RetentionEnforcementResult> EnforceRetentionAsync(
		CancellationToken cancellationToken)
	{
		var dryRun = _options.Value.DryRun;
		LogRetentionEnforcementStarted(dryRun);

		try
		{
			var policies = DiscoverRetentionPolicies();

			// Enforcement is policy-driven by the framework, but the actual data-store deletion is performed
			// by registered IRetentionContributor implementations (mirrors the IErasureContributor seam).
			// When none are registered we MUST NOT report success while deleting nothing — log a warning and
			// return zero records cleaned (honest contract; MS-bar: build the fix, never a silent no-op).
			if (_contributors.Count == 0)
			{
				LogRetentionNoContributorsRegistered(policies.Count);

				return new RetentionEnforcementResult
				{
					PoliciesEvaluated = policies.Count,
					RecordsCleaned = 0,
					IsDryRun = dryRun,
					CompletedAt = DateTimeOffset.UtcNow,
				};
			}

			var context = new RetentionContributorContext
			{
				Policies = policies,
				DryRun = dryRun,
				AsOf = DateTimeOffset.UtcNow,
			};

			var totalRecordsCleaned = 0;

			// Fail-open per contributor: a single contributor's failure must not abort the others
			// (mirrors ErasureService). Failures are logged; the overall pass still reports what was cleaned.
			foreach (var contributor in _contributors)
			{
				try
				{
					var result = await contributor.EnforceAsync(context, cancellationToken).ConfigureAwait(false);

					if (result.Success)
					{
						totalRecordsCleaned += result.RecordsCleaned;
						LogRetentionContributorCompleted(contributor.Name, result.RecordsCleaned, dryRun);
					}
					else
					{
						LogRetentionContributorFailed(contributor.Name, result.ErrorMessage ?? "Unknown error", null);
					}
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					LogRetentionContributorFailed(contributor.Name, ex.Message, ex);
				}
			}

			LogRetentionEnforcementCompleted(policies.Count, dryRun);

			return new RetentionEnforcementResult
			{
				PoliciesEvaluated = policies.Count,
				RecordsCleaned = totalRecordsCleaned,
				IsDryRun = dryRun,
				CompletedAt = DateTimeOffset.UtcNow,
			};
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogRetentionEnforcementFailed(ex);
			throw;
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Uses AppDomain.GetAssemblies() and reflection to discover PersonalDataAttribute annotations at runtime.")]
	public Task<IReadOnlyList<RetentionPolicy>> GetRetentionPoliciesAsync(
		CancellationToken cancellationToken)
	{
		var policies = DiscoverRetentionPolicies();
		return Task.FromResult<IReadOnlyList<RetentionPolicy>>(policies);
	}

	[RequiresUnreferencedCode("Uses AppDomain.GetAssemblies() and Assembly.GetType() for runtime type scanning.")]
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

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
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
#pragma warning restore IL2026

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

	[LoggerMessage(
		ComplianceEventId.RetentionNoContributorsRegistered,
		LogLevel.Warning,
		"Retention enforcement is enabled and evaluated {PolicyCount} policies, but no IRetentionContributor is registered — no data was deleted. Register a retention contributor to enforce cleanup.")]
	private partial void LogRetentionNoContributorsRegistered(int policyCount);

	[LoggerMessage(
		ComplianceEventId.RetentionContributorCompleted,
		LogLevel.Information,
		"Retention contributor {ContributorName} cleaned {RecordsCleaned} record(s), dry run: {DryRun}")]
	private partial void LogRetentionContributorCompleted(string contributorName, int recordsCleaned, bool dryRun);

	[LoggerMessage(
		ComplianceEventId.RetentionContributorFailed,
		LogLevel.Error,
		"Retention contributor {ContributorName} failed: {Error}")]
	private partial void LogRetentionContributorFailed(string contributorName, string error, Exception? exception);
}
