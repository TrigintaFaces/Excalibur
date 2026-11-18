// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Resilience;

/// <summary>Retains a trusted provider's positive transient observation on the original exception.</summary>
/// <remarks>
/// This is classification evidence, not ownership or delivery evidence. It survives flow-scope exit
/// without retaining the exception solely through this table. Consumers cannot forge it through
/// exception Data or message headers. Only a positively recognized provider failure may be recorded.
/// </remarks>
internal static class KnownTransientFailureEvidence
{
	private static readonly ConditionalWeakTable<Exception, Observation> Observations = new();
	private static readonly Observation KnownTransient = new();

	internal static void Record(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		_ = Observations.GetValue(exception, static _ => KnownTransient);
	}

	internal static bool Contains(Exception exception) => Observations.TryGetValue(exception, out _);

	private sealed class Observation { }
}
