// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// Azure Service Bus has no native priority field, so a caller who sets one is not honoured there.
/// </summary>
/// <remarks>
/// Locks the property key both sides of that gap agree on. The RabbitMQ sender reads this exact key and
/// maps it to the broker's priority; the Service Bus sender drops every dispatch-prefixed key and now
/// warns on this one. If the constant changes, RabbitMQ silently stops prioritising and Service Bus
/// silently stops warning -- the same message would then be ignored on both transports with nothing said,
/// which is the state this work removed.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PriorityNotSupportedWarningShould
{
	[Fact]
	public void KeepTheWireKeyBothSendersAgreeOn() =>
		TransportTelemetryConstants.PropertyKeys.Priority.ShouldBe("dispatch.priority");

	[Fact]
	public void KeepThePriorityKeyDispatchPrefixed() =>
		// The Service Bus sender's property copy skips dispatch-prefixed keys, which is what makes the
		// priority invisible there rather than arriving as a meaningless application property. Losing the
		// prefix would leak it onto the wire as an uninterpreted string.
		TransportTelemetryConstants.PropertyKeys.Priority.ShouldStartWith("dispatch.");
}
