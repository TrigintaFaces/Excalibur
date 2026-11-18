// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using Excalibur.Dispatch.Outbox;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Outbox")]
public sealed class OutboxAuthorityDomainShould
{
	[Fact]
	public async Task ShareAnExplicitDomainAcrossTransientWrappersAndKeepOtherDomainsIndependent()
	{
		var domain = new EqualDomain();
		var firstGate = new Wrapper(domain);
		var secondGate = new Wrapper(domain);
		var otherGate = new Wrapper(new EqualDomain());
		var first = new MessageBusOutboxTenureShould.Harness(sharedGate: firstGate);
		var second = new MessageBusOutboxTenureShould.Harness(sharedGate: secondGate);
		var other = new MessageBusOutboxTenureShould.Harness(sharedGate: otherGate);
		first.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(first.Rows("a"));
		second.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(second.Rows("b"));
		other.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(other.Rows("c"));
		first.Complete = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
		await first.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		await second.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		second.ClaimTokens.ShouldBeEmpty();
		(await other.Publisher.PublishPendingMessagesAsync(CancellationToken.None)).SuccessCount.ShouldBe(1);
		secondGate.Token = 8;
		(await second.Publisher.PublishPendingMessagesAsync(CancellationToken.None)).SuccessCount.ShouldBe(1);
		firstGate.DomainReads.ShouldBe(1);
		secondGate.DomainReads.ShouldBe(1);
		otherGate.DomainReads.ShouldBe(1);
	}

	[Fact]
	public void RejectNullOrThrowingExplicitIdentityAndPreserveInactiveModes()
	{
		var nullGate = new Wrapper(null);
		Should.Throw<ArgumentException>(() => new OutboxTenureAdmission(true, nullGate))
			.Message.ShouldContain("stable nonnull");
		var error = new IOException("identity getter failed");
		var throwing = new Wrapper(new object()) { IdentityError = error };
		Should.Throw<IOException>(() => new OutboxTenureAdmission(true, throwing)).ShouldBeSameAs(error);
		var inactive = new OutboxTenureAdmission(false, throwing);
		inactive.Capture().ShouldBeNull();
		inactive.TryAdmit(null).ShouldBeTrue();
		throwing.DomainReads.ShouldBe(1); // Inactive mode never invokes identity code.
	}

	[Fact]
	public void PreserveRejectionWhileTheExplicitOwnerRemainsReachableWithoutRetainingDeadDomains()
	{
		var domain = new object();
		RejectThroughFreshHandle(domain);
		GC.Collect();
		GC.WaitForPendingFinalizers();
		var fresh = new OutboxTenureAdmission(true, new Wrapper(domain));
		fresh.TryAdmit(fresh.Capture()).ShouldBeFalse();
		GC.KeepAlive(domain);

		var weak = CreateUnrootedDomain();
		for (var attempt = 0; attempt < 10 && weak.IsAlive; attempt++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}
		weak.IsAlive.ShouldBeFalse(); // Bounded no-retention control, not an immediate GC timing guarantee.
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void RejectThroughFreshHandle(object domain)
	{
		var admission = new OutboxTenureAdmission(true, new Wrapper(domain));
		admission.TryAdmit(admission.Capture()).ShouldBeTrue();
		admission.Reject(7);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static WeakReference CreateUnrootedDomain()
	{
		var domain = new object();
		RejectThroughFreshHandle(domain);
		return new WeakReference(domain);
	}

	private sealed class EqualDomain
	{
		public override bool Equals(object? obj) => obj is EqualDomain;
		public override int GetHashCode() => 0;
	}

	private sealed class Wrapper(object? domain) : ILeaderProcessingGate, ILeaderProcessingAuthorityDomain
	{
		internal long Token = 7;
		internal int DomainReads;
		internal Exception? IdentityError;
		public bool ShouldProcess => true;
		public long? FencingToken => Token;
		public object AuthorityDomain
		{
			get
			{
				DomainReads++;
				if (IdentityError is { } error) { throw error; }
				return domain!;
			}
		}
	}
}
