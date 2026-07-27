// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Serialization;

/// <summary>
/// Independent regression lock (author≠impl) for m084l4: the message-ingress payload-size guard must fail
/// <strong>closed</strong> — a payload over the configured maximum is rejected with
/// <see cref="PayloadTooLargeException"/> before materialization, a payload at or under the limit passes,
/// and a <see langword="null"/> limit is an explicit opt-out. Non-vacuous: the pre-fix ingress had no
/// size check at all, so every assertion below RED-proves the guard's rejection behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PayloadSizeGuardShould
{
	[Fact]
	public void Pass_a_payload_below_the_limit()
	{
		Should.NotThrow(() => PayloadSizeGuard.EnsureWithinLimit(length: 512, maxBytes: 1024));
	}

	[Fact]
	public void Pass_a_payload_exactly_at_the_limit()
	{
		// Boundary: at-limit is allowed; only strictly-greater is rejected.
		Should.NotThrow(() => PayloadSizeGuard.EnsureWithinLimit(length: 1024, maxBytes: 1024));
	}

	[Fact]
	public void Reject_a_payload_one_byte_over_the_limit()
	{
		var ex = Should.Throw<PayloadTooLargeException>(
			() => PayloadSizeGuard.EnsureWithinLimit(length: 1025, maxBytes: 1024));

		ex.ActualBytes.ShouldBe(1025);
		ex.MaxBytes.ShouldBe(1024);
	}

	[Fact]
	public void Opt_out_when_the_limit_is_null_even_for_a_huge_payload()
	{
		Should.NotThrow(() => PayloadSizeGuard.EnsureWithinLimit(length: int.MaxValue, maxBytes: (int?)null));
	}

	[Fact]
	public void Enforce_the_nullable_overload_when_a_limit_is_present()
	{
		var ex = Should.Throw<PayloadTooLargeException>(
			() => PayloadSizeGuard.EnsureWithinLimit(length: 2048, maxBytes: (int?)1024));

		ex.ActualBytes.ShouldBe(2048);
		ex.MaxBytes.ShouldBe(1024);
	}

	[Fact]
	public void Default_the_maximum_to_four_mebibytes()
	{
		PayloadSizeGuard.DefaultMaxPayloadBytes.ShouldBe(4 * 1024 * 1024);
	}
}
