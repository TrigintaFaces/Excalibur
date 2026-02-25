// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DeduplicationOptions = Excalibur.Dispatch.Options.Delivery.DeduplicationOptions;

namespace Excalibur.Tests.Infrastructure;

/// <summary>
///     Extension methods for deduplication strategies to maintain backward compatibility with tests.
/// </summary>
public static class DeduplicationExtensions
{
	/// <summary>
	///     Generates a deduplication key from an inbox message.
	/// </summary>
	public static string GenerateKey(this IDeduplicationStrategy strategy, IInboxMessage message)
	{
		ArgumentNullException.ThrowIfNull(strategy);
		ArgumentNullException.ThrowIfNull(message);

		// Use the message body and attributes to generate the key
		var attributes = new Dictionary<string, object>
		{
			["MessageId"] = message.ExternalMessageId ?? string.Empty,
			["MessageType"] = message.MessageType ?? string.Empty,
		};

		return strategy.GenerateDeduplicationId(message.MessageBody ?? string.Empty, attributes);
	}

	/// <summary>
	///     Generates a deduplication key from an inbox message for ContentHashDeduplicationStrategy.
	/// </summary>
	public static string GenerateKey(this ContentHashDeduplicationStrategy strategy, IInboxMessage message)
	{
		ArgumentNullException.ThrowIfNull(strategy);
		ArgumentNullException.ThrowIfNull(message);

		// Use the message body and attributes to generate the key
		var attributes = new Dictionary<string, object>
		{
			["MessageId"] = message.ExternalMessageId ?? string.Empty,
			["MessageType"] = message.MessageType ?? string.Empty,
		};

		return strategy.GenerateDeduplicationId(message.MessageBody ?? string.Empty, attributes);
	}

	/// <summary>
	///     Generates a deduplication key from an inbox message for CompositeDeduplicationStrategy.
	/// </summary>
	public static string GenerateKey(this CompositeDeduplicationStrategy strategy, IInboxMessage message)
	{
		ArgumentNullException.ThrowIfNull(strategy);
		ArgumentNullException.ThrowIfNull(message);

		// Use the message body and attributes to generate the key
		var attributes = new Dictionary<string, object>
		{
			["MessageId"] = message.ExternalMessageId ?? string.Empty,
			["MessageType"] = message.MessageType ?? string.Empty,
		};

		return strategy.GenerateDeduplicationId(message.MessageBody ?? string.Empty, attributes);
	}
}

/// <summary>
///     Helper class to provide backward compatibility for DeduplicationOptions.
/// </summary>
public static class DeduplicationOptionsExtensions
{
	/// <summary>
	///     Gets or sets the TimeWindow property (maps to DeduplicationWindow).
	/// </summary>
	public static TimeSpan TimeWindow(this DeduplicationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return options.DeduplicationWindow;
	}

	/// <summary>
	///     Sets the TimeWindow property (maps to DeduplicationWindow).
	/// </summary>
	public static DeduplicationOptions WithTimeWindow(this DeduplicationOptions options, TimeSpan timeWindow)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.DeduplicationWindow = timeWindow;
		return options;
	}
}
