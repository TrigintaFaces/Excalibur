// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.IbmMq;

namespace Excalibur.Dispatch.Transport.Tests.IbmMq;

/// <summary>
/// Regression locks for the IBM MQ ingress payload-size guard defaulting on (parity with the other
/// transports — Pulsar/RabbitMQ/etc. — so the cross-cutting guard is not silently opt-in).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class IbmMqReceivePayloadGuardShould
{
	// The shared PayloadSizeGuard.DefaultMaxPayloadBytes (internal) is 4 MiB; asserted as a literal here
	// because the guard type is internal to Excalibur.Dispatch.Abstractions.
	private const int SharedGuardDefaultBytes = 4 * 1024 * 1024;

	private static IbmMqOptions ValidBaseOptions() => new()
	{
		QueueManager = "QM1",
		Host = "localhost",
		Port = 1414,
		Channel = "DEV.APP.SVRCONN",
		QueueName = "DEV.QUEUE.1",
	};

	[Fact]
	public void DefaultMaxPayloadBytesToTheSharedGuardDefault()
	{
		// RED on the pre-fix null default (guard off). The ingress size guard must be on by default.
		new IbmMqReceiveTuningOptions().MaxPayloadBytes.ShouldBe(SharedGuardDefaultBytes);
	}

	[Fact]
	public void RejectMaxPayloadBytesBelowOne()
	{
		var validator = new IbmMqOptionsValidator();
		var options = ValidBaseOptions();
		options.Receive.MaxPayloadBytes = 0;

		var result = validator.Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.Failures.ShouldContain(f => f.Contains(nameof(IbmMqReceiveTuningOptions.MaxPayloadBytes), StringComparison.Ordinal));
	}

	[Fact]
	public void AllowNullMaxPayloadBytesAsExplicitOptOut()
	{
		var validator = new IbmMqOptionsValidator();
		var options = ValidBaseOptions();
		options.Receive.MaxPayloadBytes = null;

		var result = validator.Validate(name: null, options);

		result.Succeeded.ShouldBeTrue();
	}
}
