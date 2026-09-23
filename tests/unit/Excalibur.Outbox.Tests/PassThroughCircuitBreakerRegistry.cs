// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// A circuit-breaker registry that runs every operation and opens for nothing, for tests whose subject
/// is not the breaker.
/// </summary>
/// <remarks>
/// <para>
/// The drains take <c>ITransportCircuitBreakerRegistry</c> as a required dependency, so every test that
/// constructs one has to supply something. A <c>A.Fake&lt;ITransportCircuitBreakerRegistry&gt;()</c>
/// looks like the obvious answer and is the wrong one: its <c>GetOrCreate</c> returns a faked policy
/// whose <c>ExecuteAsync</c> never invokes the callback, so the dispatch under test silently does not
/// happen and the arm fails for a reason that has nothing to do with what it asserts.
/// </para>
/// <para>
/// This runs the callback and reports <see cref="CircuitState.Closed"/> forever. It is deliberately not
/// a no-op: a policy that swallowed the callback would be the same trap in a different costume. A test
/// whose subject IS the breaker should use a fake or a real registry and assert on it, not this.
/// </para>
/// </remarks>
internal sealed class PassThroughCircuitBreakerRegistry : ITransportCircuitBreakerRegistry
{
	public static PassThroughCircuitBreakerRegistry Instance { get; } = new();

	public ICircuitBreakerPolicy GetOrCreate(string transportName) => PassThroughPolicy.Instance;

	public ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options) =>
		PassThroughPolicy.Instance;

	public ICircuitBreakerPolicy? TryGet(string transportName) => PassThroughPolicy.Instance;

	private sealed class PassThroughPolicy : ICircuitBreakerPolicy
	{
		public static PassThroughPolicy Instance { get; } = new();

		public CircuitState State => CircuitState.Closed;

		public Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> action,
			CancellationToken cancellationToken) => action(cancellationToken);

		public Task<TResult> ExecuteAsync<TResult>(
			Func<CancellationToken, Task<TResult>> action,
			Func<TResult, bool> isFailure,
			CancellationToken cancellationToken) => action(cancellationToken);

		public Task ResetAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
