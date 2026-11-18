// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Frozen;

namespace Excalibur.Cdc.SqlServer;

/// <summary>Captures the configuration used for one processor lifetime.</summary>
/// <remarks>
/// The caller must keep the input stable while capture runs. Delegate identity is preserved;
/// mutable state captured by a callback is still owned by the caller.
/// </remarks>
internal sealed class DatabaseOptionsSnapshot : IDatabaseOptions
{
	private DatabaseOptionsSnapshot(IDatabaseOptions source)
	{
		DatabaseName = source.DatabaseName;
		DatabaseConnectionIdentifier = source.DatabaseConnectionIdentifier;
		StateConnectionIdentifier = source.StateConnectionIdentifier;
		StopOnMissingTableHandler = source.StopOnMissingTableHandler;
		QueueSize = source.QueueSize;
		ProducerBatchSize = source.ProducerBatchSize;
		ConsumerBatchSize = source.ConsumerBatchSize;
		CaptureInstances = (string[])source.CaptureInstances.Clone();
		var sourceMap = source.CaptureInstanceToTableNameMap;
		var effectiveMap = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var instance in CaptureInstances)
		{
			// Queries label each CDC row with the configured capture-instance literal. Preserve
			// the source dictionary's lookup semantics on exactly that reachable query domain.
			effectiveMap[instance] = sourceMap.TryGetValue(instance, out var table) ? table : instance;
		}
		CaptureInstanceToTableNameMap = effectiveMap.ToFrozenDictionary(StringComparer.Ordinal);
		var recovery = source.RecoveryOptions;
		RecoveryOptions = recovery is null ? null : new CdcRecoveryOptions
		{
			RecoveryStrategy = recovery.RecoveryStrategy,
			OnPositionReset = recovery.OnPositionReset,
			MaxRecoveryAttempts = recovery.MaxRecoveryAttempts,
			RecoveryAttemptDelay = recovery.RecoveryAttemptDelay,
			EnableStructuredLogging = recovery.EnableStructuredLogging,
		};
	}

	public string DatabaseName { get; }
	public string DatabaseConnectionIdentifier { get; }
	public string StateConnectionIdentifier { get; }
	public bool StopOnMissingTableHandler { get; }
	public string[] CaptureInstances { get; }
	public int QueueSize { get; }
	public int ProducerBatchSize { get; }
	public int ConsumerBatchSize { get; }
	public CdcRecoveryOptions? RecoveryOptions { get; }
	public IReadOnlyDictionary<string, string> CaptureInstanceToTableNameMap { get; }

	internal static DatabaseOptionsSnapshot Capture(IDatabaseOptions source)
	{
		ArgumentNullException.ThrowIfNull(source);
		return new DatabaseOptionsSnapshot(source);
	}
}
