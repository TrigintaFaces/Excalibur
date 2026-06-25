// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Encryption;

// Sprint 849 / Lane R3 (gejhft+ffxglb), decorated-path lock. When field-level encryption is enabled the resolved
// IOutboxStore is the EncryptingOutboxStoreDecorator. For outbox backoff-apply to work under encryption, the
// decorator MUST implement IBackoffSchedulableOutboxStore and forward MarkFailedWithBackoffAsync (carrying the
// absolute NextAttemptAt) to a capable inner store. Over a NON-capable inner it MUST fail-open to the plain
// MarkFailedAsync (backoff is an optimization, not a mandatory terminal transition) — never throw, never drop the
// failure. Independent engage-test (author≠impl). RED pre-fix: the decorator did not implement the capability.
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingOutboxStoreBackoffForwardingShould
{
	[Fact]
	public void ExposeTheBackoffSchedulableCapability()
	{
		var decorator = CreateDecorator(A.Fake<IOutboxStore>(b => b.Implements<IBackoffSchedulableOutboxStore>()));

		decorator.ShouldBeAssignableTo<IBackoffSchedulableOutboxStore>();
	}

	[Fact]
	public async Task ForwardMarkFailedWithBackoff_ToACapableInner()
	{
		var inner = A.Fake<IOutboxStore>(b => b.Implements<IBackoffSchedulableOutboxStore>());
		var decorator = (IBackoffSchedulableOutboxStore)CreateDecorator(inner);

		var nextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30);
		await decorator.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, CancellationToken.None);

		A.CallTo(() => ((IBackoffSchedulableOutboxStore)inner)
				.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FailOpenToMarkFailed_WhenInnerCannotSchedule()
	{
		var inner = A.Fake<IOutboxStore>(); // IOutboxStore only — NOT IBackoffSchedulableOutboxStore
		var decorator = (IBackoffSchedulableOutboxStore)CreateDecorator(inner);

		var nextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30);
		await decorator.MarkFailedWithBackoffAsync("msg-1", "boom", 2, nextAttemptAt, CancellationToken.None);

		A.CallTo(() => inner.MarkFailedAsync("msg-1", "boom", 2, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private static EncryptingOutboxStoreDecorator CreateDecorator(IOutboxStore inner)
	{
		var registry = A.Fake<IEncryptionProviderRegistry>();
		var options = Options.Create(new EncryptionOptions());
		return new EncryptingOutboxStoreDecorator(inner, registry, options);
	}
}
