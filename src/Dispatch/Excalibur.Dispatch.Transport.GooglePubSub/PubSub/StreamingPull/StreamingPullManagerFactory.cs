// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Default implementation of the streaming pull manager factory.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="StreamingPullManagerFactory" /> class. </remarks>
internal sealed class StreamingPullManagerFactory(
	ILoggerFactory loggerFactory,
	SubscriberServiceApiClient subscriberClient,
	IOptions<StreamingPullOptions> options) : IStreamingPullManagerFactory
{
	private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

	private readonly SubscriberServiceApiClient _subscriberClient =
		subscriberClient ?? throw new ArgumentNullException(nameof(subscriberClient));

	private readonly IOptions<StreamingPullOptions> _options = options ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Transport.Google.StreamingPullManager.StreamingPullManager(ILogger<StreamingPullManager>, ILoggerFactory, SubscriberServiceApiClient, SubscriptionName, StreamingPullOptions, MessageProcessor)")]
	public StreamingPullManager CreateManager(
		SubscriptionName subscriptionName,
		MessageStreamProcessor.MessageProcessor messageHandler)
	{
		ArgumentNullException.ThrowIfNull(subscriptionName);
		ArgumentNullException.ThrowIfNull(messageHandler);

		return new StreamingPullManager(
			_loggerFactory.CreateLogger<StreamingPullManager>(),
			_loggerFactory,
			_subscriberClient,
			subscriptionName,
			_options.Value,
			messageHandler);
	}
}
