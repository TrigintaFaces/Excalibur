// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Options for long polling receiver.
/// </summary>
public sealed class LongPollingOptions
{
	public Uri? QueueUrl { get; set; }

	public int MinPollers { get; set; } = 5;

	public int MaxPollers { get; set; } = 20;

	public int ChannelCapacity { get; set; } = 1000;

	public int VisibilityTimeout { get; set; } = 300;

	public int AdaptiveIntervalSeconds { get; set; } = 30;
}
