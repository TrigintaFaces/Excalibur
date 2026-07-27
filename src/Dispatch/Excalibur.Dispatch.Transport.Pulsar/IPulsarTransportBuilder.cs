// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// Fluent builder for configuring the Apache Pulsar transport.
/// </summary>
public interface IPulsarTransportBuilder
{
	/// <summary>
	/// Sets the Pulsar broker service URL (for example <c>pulsar://localhost:6650</c>).
	/// </summary>
	/// <param name="serviceUrl">The Pulsar service URL.</param>
	/// <returns>The builder for chaining.</returns>
	IPulsarTransportBuilder ServiceUrl(string serviceUrl);

	/// <summary>
	/// Sets the Pulsar topic name.
	/// </summary>
	/// <param name="topic">The topic name.</param>
	/// <returns>The builder for chaining.</returns>
	IPulsarTransportBuilder Topic(string topic);

	/// <summary>
	/// Sets the subscription name — the durable consumer identity shared by competing consumers.
	/// </summary>
	/// <param name="subscriptionName">The subscription name.</param>
	/// <returns>The builder for chaining.</returns>
	IPulsarTransportBuilder SubscriptionName(string subscriptionName);

	/// <summary>
	/// Sets the subscription type controlling message distribution across consumers.
	/// </summary>
	/// <param name="subscriptionType">The subscription type.</param>
	/// <returns>The builder for chaining.</returns>
	IPulsarTransportBuilder SubscriptionType(PulsarSubscriptionType subscriptionType);

	/// <summary>
	/// Configures receive-side tuning (batch size and payload limits).
	/// </summary>
	/// <param name="configure">The tuning configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	IPulsarTransportBuilder ConfigureReceive(Action<PulsarReceiveTuningOptions> configure);
}
