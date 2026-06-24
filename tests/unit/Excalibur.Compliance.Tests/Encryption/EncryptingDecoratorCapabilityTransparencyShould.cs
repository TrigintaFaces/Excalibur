// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Encryption;

// bd-dziy0x + bd-stlcgg (S841, ADR-336 decorator-transparency ruling, SA 14115/PM 14116) — the encrypting store
// decorators become the resolved IInboxStore/IOutboxStore in opt-in encryption scenarios. They MUST forward the
// new durable capabilities (IProcessingTrackingInboxStore / IDeadLetterableOutboxStore) to a capable inner, and
// MUST throw NotSupportedException over a non-capable inner — NEVER a silent no-op (which would lose the durable
// Processing / terminal-DeadLettered guarantee behind the encryption layer). Independent engage-test (author≠impl).
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingDecoratorCapabilityTransparencyShould
{
	private static EncryptingInboxStoreDecorator CreateInboxDecorator(IInboxStore inner) =>
		new(inner, A.Fake<IEncryptionProviderRegistry>(), Options.Create(new EncryptionOptions()));

	private static EncryptingOutboxStoreDecorator CreateOutboxDecorator(IOutboxStore inner) =>
		new(inner, A.Fake<IEncryptionProviderRegistry>(), Options.Create(new EncryptionOptions()));

	[Fact]
	public async Task ForwardMarkProcessing_ToACapableInboxInner()
	{
		var inner = A.Fake<IInboxStore>(b => b.Implements<IProcessingTrackingInboxStore>());
		var decorator = CreateInboxDecorator(inner);

		await decorator.MarkProcessingAsync("msg-1", "TestHandler", CancellationToken.None);

		A.CallTo(() => ((IProcessingTrackingInboxStore)inner)
				.MarkProcessingAsync("msg-1", "TestHandler", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowNotSupported_WhenInboxInnerCannotTrackProcessing()
	{
		var inner = A.Fake<IInboxStore>();
		var decorator = CreateInboxDecorator(inner);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => decorator.MarkProcessingAsync("msg-1", "TestHandler", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ForwardMarkDeadLettered_ToACapableOutboxInner()
	{
		var inner = A.Fake<IOutboxStore>(b => b.Implements<IDeadLetterableOutboxStore>());
		var decorator = CreateOutboxDecorator(inner);

		await decorator.MarkDeadLetteredAsync("msg-1", "retries exhausted", CancellationToken.None);

		A.CallTo(() => ((IDeadLetterableOutboxStore)inner)
				.MarkDeadLetteredAsync("msg-1", "retries exhausted", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowNotSupported_WhenOutboxInnerCannotDeadLetter()
	{
		var inner = A.Fake<IOutboxStore>();
		var decorator = CreateOutboxDecorator(inner);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => decorator.MarkDeadLetteredAsync("msg-1", "retries exhausted", CancellationToken.None).AsTask());
	}
}
