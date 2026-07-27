// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Regression lock for 887vwl: <see cref="OutboxDeliveryOptions.MaxPayloadBytes"/> must reject a non-positive
/// limit at startup validation. A negative or zero maximum makes the outbox-read ingress guard reject every
/// payload (length &gt; 0 &gt; max), silently bricking all delivery; <see langword="null"/> stays a valid opt-out.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class OutboxDeliveryOptionsMaxPayloadBytesShould
{
	private static ValidateOptionsResult Validate(int? maxPayloadBytes) =>
		new OutboxDeliveryOptionsValidator().Validate(
			name: null,
			new OutboxDeliveryOptions { MaxPayloadBytes = maxPayloadBytes });

	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	public void Reject_a_non_positive_limit(int maxPayloadBytes)
	{
		// Non-vacuity: pre-fix a non-positive MaxPayloadBytes passed validation and bricked delivery at runtime.
		var result = Validate(maxPayloadBytes);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(OutboxDeliveryOptions.MaxPayloadBytes));
	}

	[Fact]
	public void Accept_null_as_the_unbounded_opt_out()
	{
		Validate(maxPayloadBytes: null).Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Accept_a_positive_limit()
	{
		Validate(maxPayloadBytes: 1).Succeeded.ShouldBeTrue();
		Validate(maxPayloadBytes: 4 * 1024 * 1024).Succeeded.ShouldBeTrue();
	}
}
