// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Queues;

/// <summary>
/// Factory interface for creating and managing distributed ordered set queues across different storage backends.
/// </summary>
public interface IDistributedOrderedSetQueueFactory
{
	/// <summary>
	/// Gets or creates a distributed ordered set queue with the specified name and optional capacity.
	/// </summary>
	/// <param name="name"> The name of the queue to retrieve or create. </param>
	/// <param name="capacity"> Optional capacity limit for the queue. </param>
	/// <returns> A distributed ordered set queue instance for string elements. </returns>
	IDistributedOrderedSetQueue<string> GetQueue(string name, int? capacity = null);
}
