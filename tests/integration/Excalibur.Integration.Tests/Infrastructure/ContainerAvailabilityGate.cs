// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Integration.Tests.Infrastructure;

/// <summary>
/// Process-wide cache of "this kind of container could not be started here".
/// </summary>
/// <remarks>
/// <para>
/// A container that cannot start costs its full start timeout before it fails — minutes, not seconds.
/// Paying that once is the price of finding out; paying it again for every later fixture that wants the
/// same infrastructure is pure waste, and it is what turns an unavailable-infrastructure run into a
/// wall-clock overrun: the CI job that motivated this gate spent ~50 seconds per <em>skipped</em> test,
/// because the start was attempted first and only then did the skip fire.
/// </para>
/// <para>
/// The first failure for a key is recorded here and every later fixture asking for the same key
/// short-circuits immediately. This is the same shape as a static lazy availability probe: attempt once,
/// remember the answer.
/// </para>
/// <para>
/// <b>This gate never converts a failure into a pass.</b> It only makes an already-failing start fail
/// faster, and it carries the original cause forward so the skip or failure message still says <i>why</i>
/// rather than degrading to a generic "unavailable". A test whose infrastructure is missing is still
/// reported skipped or failed by its own call site — never passed.
/// </para>
/// </remarks>
internal static class ContainerAvailabilityGate
{
	private static readonly ConcurrentDictionary<string, Exception> Failures = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets the recorded first-failure cause for <paramref name="key"/>, if this process has already
	/// discovered that the infrastructure cannot be started.
	/// </summary>
	/// <param name="key">The infrastructure kind (for example, the container image).</param>
	/// <param name="cause">The exception from the first failed start attempt.</param>
	/// <returns><see langword="true"/> when a prior attempt already failed; otherwise <see langword="false"/>.</returns>
	public static bool TryGetFailure(string key, [NotNullWhen(true)] out Exception? cause) =>
		Failures.TryGetValue(key, out cause);

	/// <summary>
	/// Records the cause of a failed start so later fixtures for the same key short-circuit.
	/// </summary>
	/// <param name="key">The infrastructure kind (for example, the container image).</param>
	/// <param name="cause">The exception from the failed start attempt.</param>
	public static void RecordFailure(string key, Exception cause) => _ = Failures.TryAdd(key, cause);

	/// <summary>
	/// Builds a skip reason that names the infrastructure and preserves the original failure cause.
	/// </summary>
	/// <remarks>
	/// Reporting every startup failure as a bare "infrastructure unavailable" makes a fixable fault — an
	/// image pull, a port collision, an out-of-memory container — indistinguishable from an absent Docker
	/// daemon, and undiagnosable from the CI log. The cause is therefore always appended.
	/// </remarks>
	/// <param name="infrastructure">Human-readable infrastructure name, e.g. "OpenSearch (Docker)".</param>
	/// <param name="cause">The captured failure cause, if any.</param>
	/// <returns>The skip reason.</returns>
	public static string SkipReason(string infrastructure, Exception? cause)
	{
		var reason =
			$"[infrastructure-unavailable] {infrastructure} is not available, so this fact did NOT execute. "
			+ "It is reported skipped, never passed: a test that returns early on missing infrastructure is "
			+ "satisfied by doing nothing.";

		return cause is null ? reason : $"{reason} Cause: {cause.GetType().Name}: {cause.Message}";
	}
}
