// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexLifecycleManagement;

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Internal helper that translates the framework's domain
/// <see cref="IndexLifecyclePolicy"/> (+ per-phase configs) into the Elastic
/// SDK's fluent <see cref="IlmPolicyDescriptor"/>. Extracted from
/// <see cref="IndexManagement.IndexLifecycleManager"/> during the S800 seam
/// migration so descriptor plumbing lives beside the
/// <see cref="IndexLifecycleOperationsAdapter"/> that uses it.
/// </summary>
internal static class IlmPolicyDescriptorBuilder
{
	/// <summary>
	/// Configures the ILM policy descriptor from the domain policy model.
	/// </summary>
	public static IlmPolicyDescriptor Configure(IlmPolicyDescriptor descriptor, IndexLifecyclePolicy policy)
	{
		return descriptor.Phases(phases =>
		{
			if (policy.Hot is not null)
			{
				_ = phases.Hot(hot => ConfigureHot(hot, policy.Hot));
			}

			if (policy.Warm is not null)
			{
				_ = phases.Warm(warm => ConfigureWarm(warm, policy.Warm));
			}

			if (policy.Cold is not null)
			{
				_ = phases.Cold(cold => ConfigureCold(cold, policy.Cold));
			}

			if (policy.Delete is not null)
			{
				_ = phases.Delete(delete => ConfigureDelete(delete, policy.Delete));
			}
		});
	}

	private static PhaseDescriptor ConfigureHot(PhaseDescriptor descriptor, HotPhaseConfiguration config)
	{
		if (config.MinAge.HasValue)
		{
			_ = descriptor.MinAge(ToDuration(config.MinAge.Value));
		}

		_ = descriptor.Actions(actions =>
		{
			if (config.Rollover is not null)
			{
				_ = actions.Rollover(rollover =>
				{
					if (config.Rollover.MaxAge.HasValue)
					{
						_ = rollover.MaxAge(ToDuration(config.Rollover.MaxAge.Value));
					}

					if (config.Rollover.MaxDocs.HasValue)
					{
						_ = rollover.MaxDocs(config.Rollover.MaxDocs.Value);
					}

					if (!string.IsNullOrEmpty(config.Rollover.MaxSize))
					{
						_ = rollover.MaxSize(new ByteSize(config.Rollover.MaxSize));
					}

					if (!string.IsNullOrEmpty(config.Rollover.MaxPrimaryShardSize))
					{
						_ = rollover.MaxPrimaryShardSize(new ByteSize(config.Rollover.MaxPrimaryShardSize));
					}
				});
			}

			if (config.Priority.HasValue)
			{
				_ = actions.SetPriority(sp => sp.Priority(config.Priority.Value));
			}
		});

		return descriptor;
	}

	private static PhaseDescriptor ConfigureWarm(PhaseDescriptor descriptor, WarmPhaseConfiguration config)
	{
		if (config.MinAge.HasValue)
		{
			_ = descriptor.MinAge(ToDuration(config.MinAge.Value));
		}

		_ = descriptor.Actions(actions =>
		{
			if (config.ShrinkNumberOfShards.HasValue)
			{
				_ = actions.Shrink(shrink => shrink.NumberOfShards(config.ShrinkNumberOfShards.Value));
			}

			if (config.Priority.HasValue)
			{
				_ = actions.SetPriority(sp => sp.Priority(config.Priority.Value));
			}

			if (config.NumberOfReplicas.HasValue)
			{
				_ = actions.Allocate(allocate => allocate.NumberOfReplicas(config.NumberOfReplicas.Value));
			}
		});

		return descriptor;
	}

	private static PhaseDescriptor ConfigureCold(PhaseDescriptor descriptor, ColdPhaseConfiguration config)
	{
		if (config.MinAge.HasValue)
		{
			_ = descriptor.MinAge(ToDuration(config.MinAge.Value));
		}

		_ = descriptor.Actions(actions =>
		{
			if (config.Priority.HasValue)
			{
				_ = actions.SetPriority(sp => sp.Priority(config.Priority.Value));
			}

			if (config.NumberOfReplicas.HasValue)
			{
				_ = actions.Allocate(allocate => allocate.NumberOfReplicas(config.NumberOfReplicas.Value));
			}
		});

		return descriptor;
	}

	private static PhaseDescriptor ConfigureDelete(PhaseDescriptor descriptor, DeletePhaseConfiguration config)
	{
		if (config.MinAge.HasValue)
		{
			_ = descriptor.MinAge(ToDuration(config.MinAge.Value));
		}

		_ = descriptor.Actions(actions =>
		{
			_ = actions.Delete(_ => { });

			if (!string.IsNullOrEmpty(config.WaitForSnapshotPolicy))
			{
				_ = actions.WaitForSnapshot(w => w.Policy(config.WaitForSnapshotPolicy));
			}
		});

		return descriptor;
	}

	private static Duration ToDuration(TimeSpan timeSpan)
	{
		if (timeSpan.TotalDays >= 1)
		{
			return new Duration($"{(long)timeSpan.TotalDays}d");
		}

		if (timeSpan.TotalHours >= 1)
		{
			return new Duration($"{(long)timeSpan.TotalHours}h");
		}

		if (timeSpan.TotalMinutes >= 1)
		{
			return new Duration($"{(long)timeSpan.TotalMinutes}m");
		}

		if (timeSpan.TotalSeconds >= 1)
		{
			return new Duration($"{(long)timeSpan.TotalSeconds}s");
		}

		return new Duration($"{(long)timeSpan.TotalMilliseconds}ms");
	}
}
