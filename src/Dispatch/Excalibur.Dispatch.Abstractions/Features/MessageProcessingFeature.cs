// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Default implementation of <see cref="IMessageProcessingFeature"/>.
/// </summary>
public sealed class MessageProcessingFeature : IMessageProcessingFeature
{
	/// <inheritdoc />
	public int ProcessingAttempts { get; set; }

	/// <inheritdoc />
	public bool IsRetry { get; set; }

	/// <inheritdoc />
	public DateTimeOffset? FirstAttemptTime { get; set; }

	/// <inheritdoc />
	public int DeliveryCount { get; set; }
}
