// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Extension methods for MessageEnvelopeStruct.
/// </summary>
public static class MessageEnvelopeStructExtensions
{
	/// <summary>
	/// Create a new envelope with modified headers.
	/// </summary>
	public static MessageEnvelopeStruct WithHeaders(
		this MessageEnvelopeStruct envelope,
		params (string key, string value)[] headers)
	{
		ArgumentNullException.ThrowIfNull(headers);
		using var pooled = DictionaryPools.StringDictionary.CreatePooled();

		// Copy existing headers
		if (envelope.Headers != null)
		{
			foreach (var kvp in envelope.Headers)
			{
				pooled.Dictionary[kvp.Key] = kvp.Value;
			}
		}

		// Add new headers
		foreach (var (key, value) in headers)
		{
			pooled.Dictionary[key] = value;
		}

		return new MessageEnvelopeStruct(
			envelope.MessageId,
			envelope.Body,
			envelope.TimestampTicks,
			new Dictionary<string, string>(pooled.Dictionary, StringComparer.Ordinal),
			envelope.CorrelationId,
			envelope.MessageType,
			envelope.Priority,
			envelope.TimeToLiveSeconds);
	}
}
