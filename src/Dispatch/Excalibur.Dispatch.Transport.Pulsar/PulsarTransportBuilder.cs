// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// Default <see cref="IPulsarTransportBuilder"/> that accumulates configuration into a
/// <see cref="PulsarOptions"/> instance applied at registration time.
/// </summary>
internal sealed class PulsarTransportBuilder : IPulsarTransportBuilder
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PulsarTransportBuilder"/> class as a view over
	/// <paramref name="options"/>. The builder does not own the instance: every fluent call mutates the
	/// caller's options directly, so there is no second copy for a property to be dropped from.
	/// </summary>
	/// <param name="options">The options instance this builder configures.</param>
	public PulsarTransportBuilder(PulsarOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		Options = options;
	}

	/// <summary>
	/// Gets the options being configured by this builder.
	/// </summary>
	public PulsarOptions Options { get; }

	/// <inheritdoc />
	public IPulsarTransportBuilder ServiceUrl(string serviceUrl)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);
		Options.ServiceUrl = serviceUrl;
		return this;
	}

	/// <inheritdoc />
	public IPulsarTransportBuilder RequireTls(bool required)
	{
		Options.RequireTls = required;
		return this;
	}

	/// <inheritdoc />
	public IPulsarTransportBuilder Topic(string topic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		Options.Topic = topic;
		return this;
	}

	/// <inheritdoc />
	public IPulsarTransportBuilder SubscriptionName(string subscriptionName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
		Options.SubscriptionName = subscriptionName;
		return this;
	}

	/// <inheritdoc />
	public IPulsarTransportBuilder SubscriptionType(PulsarSubscriptionType subscriptionType)
	{
		Options.SubscriptionType = subscriptionType;
		return this;
	}

	/// <inheritdoc />
	public IPulsarTransportBuilder SubscriptionInitialPosition(PulsarSubscriptionInitialPosition initialPosition)
	{
		Options.SubscriptionInitialPosition = initialPosition;
		return this;
	}

	/// <inheritdoc />
	public IPulsarTransportBuilder ConfigureReceive(Action<PulsarReceiveTuningOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(Options.Receive);
		return this;
	}
}
