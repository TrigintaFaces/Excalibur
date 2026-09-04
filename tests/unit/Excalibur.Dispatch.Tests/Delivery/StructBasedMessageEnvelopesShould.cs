// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// The struct-envelope pooling path was advertised through a registration entry point whose factory
/// resolved a pool interface that had no implementation anywhere, so every consumer that called it got an
/// <see cref="InvalidOperationException"/> on first resolve. The capability was removed rather than
/// completed; these arms bind the removal so it cannot be reintroduced half-built.
/// </summary>
public sealed class StructBasedMessageEnvelopesShould
{
	[Fact]
	public void NotAdvertiseARegistrationEntryPointForAPoolThatDoesNotExist()
	{
		var extensions = typeof(DeliveryMessageEnvelopeExtensions);

		extensions.GetMethod("AddStructBasedMessageEnvelopes").ShouldBeNull();
		extensions.GetMethod("WithStructBasedEnvelopes").ShouldBeNull();
	}

	[Fact]
	public void NotShipAPoolInterfaceWithNoImplementation()
	{
		typeof(IDispatcher).Assembly.GetType("Excalibur.Dispatch.IMessagePool").ShouldBeNull();
		typeof(DeliveryMessageEnvelopeExtensions).Assembly
			.GetType("Excalibur.Dispatch.Delivery.IMessageEnvelopePool").ShouldBeNull();
	}
}
