// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.Fixtures;

/// <summary>
/// Records whether a test class's REQUIRED container actually started, and refuses to let any test
/// proceed — or pass — when it did not.
/// </summary>
/// <remarks>
/// <para>
/// This exists to close a specific failure shape: a class that catches its container-start exception,
/// stores the failure in a <see langword="bool" />, and has every test read that flag and
/// <c>return</c> early. Such a test reports <em>passed</em>. A green run over a suite written that way
/// is indistinguishable from a run in which nothing executed at all, because the runner increments
/// <c>executed</c> and <c>passed</c> either way — so no exit code, no counter, and no shard summary can
/// detect it. Absence looks exactly like success.
/// </para>
/// <para>
/// <b>The distinction this type deliberately does NOT make.</b> "Docker is not installed here" and
/// "Docker is installed and the container failed to start" are genuinely different situations, but they
/// are not distinguished per-machine at runtime. They are settled per-infrastructure, up front, the way
/// <see cref="ContainerFixtureBase" /> already settles them: infrastructure declared REQUIRED fails
/// loudly however it became unavailable, and infrastructure declared OPTIONAL degrades to a reported
/// skip. Deciding it by probing the daemon instead would mean the same broken image silently skips on a
/// laptop and fails in CI — the machine, not the contract, would decide whether a fault is allowed to
/// hide. That is the reasoning that produced the early-return in the first place.
/// </para>
/// <para>
/// Use this type for REQUIRED infrastructure. For genuinely optional infrastructure — a cloud emulator
/// that is not present everywhere — use the established skip path instead, which reports the test as
/// not-executed with a diagnostic reason and never as passed.
/// </para>
/// </remarks>
public sealed class RequiredContainer
{
	private readonly string _infrastructure;
	private Exception? _cause;
	private bool _started;

	/// <summary>
	/// Initializes a new instance of the <see cref="RequiredContainer" /> class.
	/// </summary>
	/// <param name="infrastructure">
	/// Human-readable name of the infrastructure, for example <c>"SQL Server (Docker)"</c>. It appears
	/// verbatim in the failure message, so a reader of the CI log can tell which dependency was missing.
	/// </param>
	public RequiredContainer(string infrastructure) => _infrastructure = infrastructure;

	/// <summary>
	/// Records that the container started and the test class may run.
	/// </summary>
	public void MarkStarted()
	{
		_started = true;
		_cause = null;
	}

	/// <summary>
	/// Records the cause of a failed start and returns the exception the caller must throw.
	/// </summary>
	/// <remarks>
	/// Returning the exception rather than throwing it keeps the <c>throw</c> at the call site, so the
	/// initializer visibly propagates instead of appearing to swallow. Callers write
	/// <c>throw container.Failed(ex);</c>.
	/// </remarks>
	/// <param name="cause">The exception thrown by the container start attempt.</param>
	/// <returns>The exception to throw from the initializer.</returns>
	public InvalidOperationException Failed(Exception cause)
	{
		_started = false;
		_cause = cause;
		return Fault();
	}

	/// <summary>
	/// Throws when the container did not start, so a test can never run — or pass — without it.
	/// </summary>
	/// <remarks>
	/// This is the liveness arm. The initializer already propagates its own failure, which fails every
	/// test in the class; this call additionally makes the vacuous early-return inexpressible, so the
	/// defect cannot be reintroduced by a later edit that restores a <c>catch</c>.
	/// </remarks>
	public void Require()
	{
		if (!_started)
		{
			throw Fault();
		}
	}

	private InvalidOperationException Fault() =>
		new(
			$"{_infrastructure} did not start, and this suite REQUIRES it: "
			+ $"{_cause?.Message ?? "the container was never started"}. "
			+ "This is thrown deliberately rather than returning early. A test that returns early when its "
			+ "infrastructure is missing is satisfied by doing nothing and is reported as passed, which makes "
			+ "a green suite indistinguishable from a suite that never ran.",
			_cause);
}
