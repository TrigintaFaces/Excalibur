// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.Scheduler;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS EventBridge implementation of message scheduler.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AwsEventBridgeScheduler" /> class. </remarks>
/// <param name="options"> The scheduler options. </param>
/// <param name="scheduler"> The AWS Scheduler client. </param>
/// <param name="logger"> The logger. </param>
public sealed class AwsEventBridgeScheduler(
	 IOptions<AwsEventBridgeSchedulerOptions> options,
	 IAmazonScheduler scheduler,
	 ILogger<AwsEventBridgeScheduler> logger)
		: EventBridgeMessageScheduler(options, scheduler, logger)
{
}
