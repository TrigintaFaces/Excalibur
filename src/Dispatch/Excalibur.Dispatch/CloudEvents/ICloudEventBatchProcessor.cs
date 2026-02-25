// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Interface for CloudEvent batch processing.
/// </summary>
public interface ICloudEventBatchProcessor
{
	/// <summary>
	/// Processes a batch of CloudEvents.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task ProcessBatchAsync(CloudEventBatch batch, CancellationToken cancellationToken);

	/// <summary>
	/// Creates batches from a collection of events.
	/// </summary>
	IReadOnlyList<CloudEventBatch> CreateBatches(IEnumerable<IDispatchEvent> events, IMessageContext context);
}
