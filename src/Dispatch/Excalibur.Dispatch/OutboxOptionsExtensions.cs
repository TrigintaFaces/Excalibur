// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides extension methods for OutboxOptions to simplify configuration and access to outbox settings.
/// </summary>
public static class OutboxOptionsExtensions
{
	/// <summary>
	/// Gets the message batch size configuration from the outbox options.
	/// </summary>
	/// <param name="options"> The outbox options instance to extract the batch size from. </param>
	/// <returns> The configured producer batch size for message processing. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when options is null. </exception>
	public static int GetMessageBatchSize(this OutboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return options.ProducerBatchSize;
	}

	/// <summary>
	/// Gets the BatchSize property for backward compatibility.
	/// </summary>
	public static int? BatchSize(this OutboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return options.ProducerBatchSize;
	}
}
