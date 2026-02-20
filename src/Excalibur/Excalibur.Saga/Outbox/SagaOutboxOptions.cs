// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Saga.Outbox;

/// <summary>
/// Configuration options for saga outbox integration.
/// </summary>
public sealed class SagaOutboxOptions
{
	/// <summary>
	/// Gets or sets the delegate that publishes events through the outbox.
	/// </summary>
	/// <value>
	/// A delegate that accepts a list of events, a saga identifier, and a cancellation token.
	/// Must be configured by the host application to integrate with the chosen outbox implementation.
	/// </value>
	public Func<IReadOnlyList<IDomainEvent>, string, CancellationToken, Task>? PublishDelegate { get; set; }
}
