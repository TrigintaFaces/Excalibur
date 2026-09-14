// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests.Fencing;

/// <summary>
/// Locks the retry policy for minting a fencing token.
/// </summary>
/// <remarks>
/// This loop used to be copied into each provider, and the copies disagreed: only one of the three
/// stopped on an exhausted token domain, while the other two retried it and then wrapped it. The arms
/// here are the behaviours that differed.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FencingTokenMintingShould
{
	private const string Resource = "orders";

	private sealed class Provider(params Func<long>[] behaviours) : IFencingTokenProvider
	{
		public int Calls { get; private set; }

		public ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
		{
			var behaviour = behaviours[Math.Min(Calls, behaviours.Length - 1)];
			Calls++;
			return new ValueTask<long>(behaviour());
		}

		// Not exercised here: this fake exists to drive the mint retry policy only.
		public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken) =>
			throw new NotSupportedException();
	}

	[Fact]
	public async Task StopOnAnExhaustedTokenDomain_WithoutRetrying()
	{
		// Exhaustion is permanent. Retrying returns the same answer while spending the grace period in
		// which the node still believes it may lead, so the attempt count is part of the contract.
		var provider = new Provider(() => throw new FencingTokenExhaustedException(Resource));

		_ = await Should.ThrowAsync<FencingTokenExhaustedException>(
			() => FencingTokenMinting.MintWithRetryAsync(provider, Resource, CancellationToken.None));

		provider.Calls.ShouldBe(1, "an exhausted token domain must not be retried.");
	}

	[Fact]
	public async Task SurfaceExhaustion_AsItsOwnType_NotWrapped()
	{
		// Callers are documented to handle this type. Wrapping it puts the one actionable signal behind
		// a generic failure, where a catch written against the documented contract will not see it.
		var provider = new Provider(() => throw new FencingTokenExhaustedException(Resource));

		var thrown = await Should.ThrowAsync<FencingTokenExhaustedException>(
			() => FencingTokenMinting.MintWithRetryAsync(provider, Resource, CancellationToken.None));

		thrown.ShouldBeOfType<FencingTokenExhaustedException>();
	}

	[Fact]
	public async Task RetryATransientFailure_AndReturnTheToken()
	{
		var attempts = 0;
		var provider = new Provider(() => ++attempts < 3 ? throw new TimeoutException() : 42L);

		var token = await FencingTokenMinting.MintWithRetryAsync(provider, Resource, CancellationToken.None);

		token.ShouldBe(42L);
		provider.Calls.ShouldBe(3);
	}

	[Fact]
	public async Task GiveUpAfterTheAttemptBudget_KeepingTheLastFailure()
	{
		var provider = new Provider(() => throw new TimeoutException("backend down"));

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => FencingTokenMinting.MintWithRetryAsync(provider, Resource, CancellationToken.None));

		provider.Calls.ShouldBe(FencingTokenMinting.DefaultMaxAttempts);
		thrown.InnerException.ShouldBeOfType<TimeoutException>();
		thrown.Message.ShouldContain("relinquishing leadership");
	}

	[Fact]
	public async Task NotAttemptAMint_WhenAlreadyCancelled()
	{
		var provider = new Provider(() => 1L);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => FencingTokenMinting.MintWithRetryAsync(provider, Resource, cts.Token));

		provider.Calls.ShouldBe(0);
	}
}
