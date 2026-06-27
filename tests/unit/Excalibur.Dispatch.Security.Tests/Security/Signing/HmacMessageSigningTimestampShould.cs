// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Author≠impl regression lock for S851 Lane 4 · <c>qtogpu</c> — timestamped HMAC signatures must
/// actually verify (the pre-fix "never verifies" bug) by <b>reusing the transmitted signing timestamp</b>
/// rather than re-deriving the current time on the verify side, and the signature-age window is a
/// <b>bidirectional</b> clock-skew tolerance.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (BackendDeveloper), against the committed
/// <see cref="HmacMessageSigningService"/> surface. A controllable <see cref="TimeProvider"/> (injected via
/// the new optional ctor param) makes sign/verify deterministic; a faked <see cref="IKeyProvider"/> returns
/// a fixed key so the HMAC is reproducible.
/// </para>
/// <para>
/// <b>RED on the pre-fix surface:</b> (1) re-deriving <c>UtcNow</c> on verify (the root bug) ⇒ a signature
/// created at T0 never reproduces its HMAC once the clock advances → <see cref="VerifyReusesTransmittedSignedAt_ValidAfterClockAdvance"/> RED; (2) substituting <c>UtcNow</c> when the transmitted
/// <c>SignedAt</c> is absent (instead of failing closed) ⇒ <see cref="FailClosed_WhenTimestampedSignatureHasNoSignedAt"/> RED; (3) a one-directional age check (<c>age &gt; max</c> only) ⇒ a future-dated
/// (negative-age) signature slips through → <see cref="RejectFutureDatedSignature_BidirectionalSkew"/> RED.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HmacMessageSigningTimestampShould
{
	private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
	private static readonly byte[] FixedKey = new byte[32]; // deterministic, shared by sign + verify

	[Fact]
	public async Task VerifyReusesTransmittedSignedAt_ValidAfterClockAdvance()
	{
		var time = new MutableTimeProvider(T0);
		var svc = CreateService(time);

		var signed = await svc.CreateSignedMessageAsync("payload", NewContext(), CancellationToken.None);

		// Clock advances within the age window: verify must reuse the transmitted SignedAt (T0), NOT the
		// current time — re-deriving UtcNow here would never reproduce the signer's HMAC (the root bug).
		time.Advance(TimeSpan.FromMinutes(2));
		var verified = await svc.ValidateSignedMessageAsync(signed, NewContext(), CancellationToken.None);

		verified.ShouldBe("payload");
	}

	[Fact]
	public async Task AcceptSignature_WithinAgeWindow()
	{
		var time = new MutableTimeProvider(T0);
		var svc = CreateService(time);
		var signed = await svc.CreateSignedMessageAsync("payload", NewContext(), CancellationToken.None);

		time.Advance(TimeSpan.FromMinutes(4)); // < 5-min MaxSignatureAgeMinutes
		(await svc.ValidateSignedMessageAsync(signed, NewContext(), CancellationToken.None)).ShouldBe("payload");
	}

	[Fact]
	public async Task RejectExpiredSignature_BeyondMaxAge()
	{
		var time = new MutableTimeProvider(T0);
		var svc = CreateService(time);
		var signed = await svc.CreateSignedMessageAsync("payload", NewContext(), CancellationToken.None);

		time.Advance(TimeSpan.FromMinutes(6)); // > 5-min window
		(await svc.ValidateSignedMessageAsync(signed, NewContext(), CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task RejectFutureDatedSignature_BidirectionalSkew()
	{
		var time = new MutableTimeProvider(T0);
		var svc = CreateService(time);
		var signed = await svc.CreateSignedMessageAsync("payload", NewContext(), CancellationToken.None);

		// Verifier clock is 6 min BEHIND the signature (a skewed/ahead signer) ⇒ negative age, |age| > max.
		// A one-directional `age > max` check would wrongly accept this; the bidirectional Math.Abs rejects it.
		time.Set(T0 - TimeSpan.FromMinutes(6));
		(await svc.ValidateSignedMessageAsync(signed, NewContext(), CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task FailClosed_WhenTimestampedSignatureHasNoSignedAt()
	{
		var time = new MutableTimeProvider(T0);
		var svc = CreateService(time);

		var ctx = NewContext();
		var signature = await svc.SignMessageAsync("payload", ctx, CancellationToken.None);

		// Verify a timestamped signature WITHOUT the transmitted SignedAt: must fail closed (false),
		// never substitute the current time (which, at T0 == sign time, would falsely validate).
		var verifyCtx = NewContext();
		verifyCtx.SignedAt = null;
		(await svc.VerifySignatureAsync("payload", signature, verifyCtx, CancellationToken.None)).ShouldBeFalse();
	}

	private static SigningContext NewContext() => new()
	{
		IncludeTimestamp = true,
		Algorithm = SigningAlgorithm.HMACSHA256,
		KeyId = "test-key",
	};

	private static HmacMessageSigningService CreateService(TimeProvider time)
	{
		var keyProvider = A.Fake<IKeyProvider>();
		_ = A.CallTo(() => keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._)).Returns(FixedKey);

		var options = Microsoft.Extensions.Options.Options.Create(
			new SigningOptions { MaxSignatureAgeMinutes = 5, DefaultKeyId = "test-key" });
		return new HmacMessageSigningService(options, keyProvider, NullLogger<HmacMessageSigningService>.Instance, time);
	}

	/// <summary>A minimal controllable <see cref="TimeProvider"/> (the service only calls <c>GetUtcNow</c>).</summary>
	private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
	{
		private DateTimeOffset _now = start;
		public override DateTimeOffset GetUtcNow() => _now;
		public void Advance(TimeSpan delta) => _now += delta;
		public void Set(DateTimeOffset now) => _now = now;
	}
}
