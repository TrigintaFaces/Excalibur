// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
///     Tests for the <see cref="NullCircuitBreakerPolicy" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NullCircuitBreakerPolicyShould
{
	[Fact]
	public void ProvideSingletonInstance()
	{
		var instance = NullCircuitBreakerPolicy.Instance;

		instance.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameInstanceOnMultipleAccesses()
	{
		NullCircuitBreakerPolicy.Instance.ShouldBeSameAs(NullCircuitBreakerPolicy.Instance);
	}

	[Fact]
	public void AlwaysBeInClosedState()
	{
		NullCircuitBreakerPolicy.Instance.State.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void HaveZeroConsecutiveFailures()
	{
		NullCircuitBreakerPolicy.Instance.ConsecutiveFailures.ShouldBe(0);
	}

	[Fact]
	public void HaveNullLastOpenedAt()
	{
		NullCircuitBreakerPolicy.Instance.LastOpenedAt.ShouldBeNull();
	}

	[Fact]
	public async Task ExecuteActionDirectly()
	{
		var result = await NullCircuitBreakerPolicy.Instance
			.ExecuteAsync(ct => Task.FromResult(42), CancellationToken.None)
			.ConfigureAwait(false);

		result.ShouldBe(42);
	}

	[Fact]
	public async Task PropagateExceptionsWithoutCircuitBreaking()
	{
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await NullCircuitBreakerPolicy.Instance
				.ExecuteAsync<string>(ct => throw new InvalidOperationException("test"), CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		// State should still be closed after failure
		NullCircuitBreakerPolicy.Instance.State.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void NotThrowOnRecordSuccess()
	{
		Should.NotThrow(() => NullCircuitBreakerPolicy.Instance.RecordSuccess());
	}

	[Fact]
	public void NotThrowOnRecordFailure()
	{
		Should.NotThrow(() => NullCircuitBreakerPolicy.Instance.RecordFailure());
		Should.NotThrow(() => NullCircuitBreakerPolicy.Instance.RecordFailure(new InvalidOperationException("test")));
	}

	[Fact]
	public void NotThrowOnReset()
	{
		Should.NotThrow(() => NullCircuitBreakerPolicy.Instance.Reset());
	}

	[Fact]
	public void ImplementICircuitBreakerPolicy()
	{
		NullCircuitBreakerPolicy.Instance.ShouldBeAssignableTo<ICircuitBreakerPolicy>();
	}

	[Fact]
	public void ImplementICircuitBreakerDiagnostics()
	{
		NullCircuitBreakerPolicy.Instance.ShouldBeAssignableTo<ICircuitBreakerDiagnostics>();
	}

	[Fact]
	public void ImplementICircuitBreakerEvents()
	{
		NullCircuitBreakerPolicy.Instance.ShouldBeAssignableTo<ICircuitBreakerEvents>();
	}

	[Fact]
	public void AllowEventSubscriptionWithoutErrors()
	{
		EventHandler<CircuitStateChangedEventArgs> handler = (_, _) => { };

		Should.NotThrow(() =>
		{
			NullCircuitBreakerPolicy.Instance.StateChanged += handler;
			NullCircuitBreakerPolicy.Instance.StateChanged -= handler;
		});
	}

	[Fact]
	public void RemainClosedAfterMultipleFailures()
	{
		var sut = NullCircuitBreakerPolicy.Instance;

		for (var i = 0; i < 100; i++)
		{
			sut.RecordFailure(new InvalidOperationException($"failure {i}"));
		}

		sut.State.ShouldBe(CircuitState.Closed);
		sut.ConsecutiveFailures.ShouldBe(0);
	}
}
