// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.Pulsar;

namespace Excalibur.Dispatch.Transport.Tests.Pulsar;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PulsarReceiveTuningOptionsShould
{
	[Fact]
	public void DefaultMaxPayloadBytesToTheSharedGuardDefault()
	{
		// The poison-message guard must be active by default, matching the other transports
		// (Kafka/RabbitMQ/gRPC) — a null default would leave Pulsar's guard inert.
		var options = new PulsarReceiveTuningOptions();

		// 4 MiB == PayloadSizeGuard.DefaultMaxPayloadBytes (the shared cross-transport default).
		options.MaxPayloadBytes.ShouldBe(4 * 1024 * 1024);
	}

	[Fact]
	public void DefaultMaxBatchSizeToTen()
	{
		var options = new PulsarReceiveTuningOptions();

		options.MaxBatchSize.ShouldBe(10);
	}

	[Fact]
	public void AllowOptingOutOfThePayloadLimitExplicitly()
	{
		var options = new PulsarReceiveTuningOptions { MaxPayloadBytes = null };

		options.MaxPayloadBytes.ShouldBeNull();
	}
}
