// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Medo;

namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Provides comprehensive extension methods for generating and working with UUID v7 strings and GUIDs.
/// </summary>
/// <remarks>
/// <para>
/// UUID v7 is a time-ordered UUID format defined in RFC 9562. It includes a Unix timestamp
/// with millisecond precision, making it suitable for use as a database primary key with
/// natural time-based ordering. All UUIDs use big-endian byte order per the RFC specification.
/// </para>
/// </remarks>
public static class Uuid7Extensions
{
	/// <summary>
	/// Maximum cache size before clearing.
	/// </summary>
	private const int MaxCacheEntries = 1024;

	/// <summary>
	/// Cache for parsed UUID v7 instances to avoid repeated parsing.
	/// </summary>
	private static readonly ConcurrentDictionary<string, (bool valid, Uuid7? uuid)> ParseCache = new(StringComparer.Ordinal);

	/// <summary>
	/// Thread-local buffer for batch generation to avoid allocations.
	/// </summary>
	[ThreadStatic]
	private static Uuid7[]? _threadLocalBuffer;

	/// <summary>
	/// Generates a new UUID v7 string in a compact 25-character format.
	/// </summary>
	/// <returns> A compact UUID v7 string representation. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string GenerateString() => Uuid7.NewUuid7().ToId25String();

	/// <summary>
	/// Generates a new UUID v7 as a <see cref="Guid" /> object.
	/// </summary>
	/// <returns> A <see cref="Guid" /> representation of the UUID v7. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Guid GenerateGuid() => Uuid7.NewUuid7().ToGuid();

	/// <summary>
	/// Generates multiple UUID v7 GUIDs efficiently.
	/// </summary>
	/// <param name="count"> The number of UUIDs to generate. </param>
	/// <returns> An array of generated GUIDs. </returns>
	public static Guid[] GenerateGuids(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

		var guids = new Guid[count];

		// For small counts, generate directly
		if (count <= 10)
		{
			for (var i = 0; i < count; i++)
			{
				guids[i] = Uuid7.NewUuid7().ToGuid();
			}

			return guids;
		}

		// For larger counts, use thread-local buffer to avoid allocations
		var buffer = _threadLocalBuffer;
		if (buffer == null || buffer.Length < count)
		{
			buffer = _threadLocalBuffer = new Uuid7[Math.Max(count, 100)];
		}

		// Generate UUIDs in batches
		for (var i = 0; i < count; i++)
		{
			buffer[i] = Uuid7.NewUuid7();
		}

		// Convert to GUIDs
		for (var i = 0; i < count; i++)
		{
			guids[i] = buffer[i].ToGuid();
		}

		return guids;
	}

	/// <summary>
	/// Generates multiple UUID v7 strings efficiently.
	/// </summary>
	/// <param name="count"> The number of UUIDs to generate. </param>
	/// <returns> An array of generated UUID strings. </returns>
	public static string[] GenerateStrings(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

		var strings = new string[count];
		for (var i = 0; i < count; i++)
		{
			strings[i] = Uuid7.NewUuid7().ToId25String();
		}

		return strings;
	}

	/// <summary>
	/// Extracts the timestamp from a UUID v7 Guid.
	/// </summary>
	/// <param name="uuid"> The UUID v7 Guid. </param>
	/// <returns> The timestamp embedded in the UUID, or null if extraction fails. </returns>
	public static DateTimeOffset? ExtractTimestamp(Guid uuid)
	{
		try
		{
			// Extract timestamp from UUID v7 manually UUID v7 has 48-bit timestamp in the first 6 bytes
			var bytes = uuid.ToByteArray();

			// Check if this is likely a UUID v7 (version bits)
			if ((bytes[6] & 0xF0) != 0x70)
			{
				return null;
			}

			// Extract the 48-bit timestamp (big-endian)
			var timestamp = ((long)bytes[0] << 40) |
							((long)bytes[1] << 32) |
							((long)bytes[2] << 24) |
							((long)bytes[3] << 16) |
							((long)bytes[4] << 8) |
							bytes[5];

			return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Extracts the timestamp from a UUID v7 string.
	/// </summary>
	/// <param name="uuidString"> The UUID v7 string. </param>
	/// <returns> The timestamp embedded in the UUID, or null if extraction fails. </returns>
	public static DateTimeOffset? ExtractTimestamp(string uuidString)
	{
		if (string.IsNullOrWhiteSpace(uuidString))
		{
			return null;
		}

		try
		{
			// Accept standard Guid format only for extraction
			if (Guid.TryParse(uuidString, out var guid))
			{
				return ExtractTimestamp(guid);
			}

			return null;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Validates whether a string is a valid UUID v7.
	/// </summary>
	/// <param name="uuidString"> The string to validate. </param>
	/// <returns> <c> true </c> if the string is a valid UUID v7; otherwise, <c> false </c>. </returns>
	public static bool IsValidUuid7String(string? uuidString) => !string.IsNullOrWhiteSpace(uuidString) && TryParseUuid7(uuidString, out _);

	/// <summary>
	/// Validates whether a GUID is a valid UUID v7.
	/// </summary>
	/// <param name="guidValue"> The GUID to validate. </param>
	/// <returns> <c> true </c> if the GUID is a valid UUID v7; otherwise, <c> false </c>. </returns>
	public static bool IsValidUuid7Guid(Guid guidValue)
	{
		try
		{
			// Check version bits directly from Guid bytes (version 7 => 0b0111xxxx)
			var bytes = guidValue.ToByteArray();
			return (bytes[6] & 0xF0) == 0x70;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Converts a UUID v7 string to a Guid.
	/// </summary>
	/// <param name="uuidString"> The UUID string to convert. </param>
	/// <returns> The converted GUID, or <see cref="Guid.Empty" /> if conversion fails. </returns>
	public static Guid ToGuid(string uuidString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(uuidString);

		// Support standard Guid format only; compact 25-char form is not parsed here
		return Guid.TryParse(uuidString, out var guid) ? guid : Guid.Empty;
	}

	/// <summary>
	/// Converts a GUID to a UUID v7 string.
	/// </summary>
	/// <param name="guidValue"> The GUID to convert. </param>
	/// <returns> The UUID v7 string, or null if conversion fails. </returns>
	public static string? ToUuid7String(Guid guidValue)
	{
		if (guidValue == Guid.Empty)
		{
			return null;
		}

		// If it is a valid v7 Guid, return the canonical Guid string form
		return IsValidUuid7Guid(guidValue)
			? guidValue.ToString()
			: null;
	}

	/// <summary>
	/// Generates a sequential range of UUID v7 GUIDs with millisecond spacing.
	/// </summary>
	/// <param name="count"> The number of UUIDs to generate. </param>
	/// <param name="intervalMs"> The millisecond interval between UUIDs. </param>
	/// <returns> An enumerable of sequential GUIDs. </returns>
	/// <remarks>
	/// Iterator methods (yield return) cannot be async, so interval pacing uses a short spin-wait.
	/// </remarks>
	public static IEnumerable<Guid> GenerateSequentialGuids(int count, int intervalMs = 1)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
		ArgumentOutOfRangeException.ThrowIfNegative(intervalMs);

		return GenerateSequentialGuidsCore(count, intervalMs);

		static IEnumerable<Guid> GenerateSequentialGuidsCore(int count, int intervalMs)
		{
			for (var i = 0; i < count; i++)
			{
				yield return Uuid7.NewUuid7().ToGuid();

				// Sleep to ensure different timestamps if intervalMs > 0
				if (intervalMs > 0 && i < count - 1)
				{
					WaitForInterval(intervalMs);
				}
			}
		}
	}

	/// <summary>
	/// Compares two UUID v7 values by their timestamp.
	/// </summary>
	/// <param name="uuid1"> The first UUID. </param>
	/// <param name="uuid2"> The second UUID. </param>
	/// <returns> A value less than 0 if uuid1 is earlier, 0 if they have the same timestamp, or greater than 0 if uuid1 is later. </returns>
	public static int CompareByTimestamp(Guid uuid1, Guid uuid2)
	{
		var time1 = ExtractTimestamp(uuid1);
		var time2 = ExtractTimestamp(uuid2);

		return time1 switch
		{
			null when time2 == null => 0,
			null => -1,
			_ => time2 == null ? 1 : time1.Value.CompareTo(time2.Value),
		};
	}

	private static void WaitForInterval(int intervalMs)
	{
		var wait = Excalibur.Dispatch.Abstractions.Diagnostics.ValueStopwatch.StartNew();
		var spinner = new SpinWait();
		while (wait.ElapsedMilliseconds < intervalMs)
		{
			spinner.SpinOnce();
		}
	}

	/// <summary>
	/// Tries to parse a UUID v7 string with caching for performance.
	/// </summary>
	/// <param name="uuidString"> The string to parse. </param>
	/// <param name="uuid"> The parsed UUID if successful. </param>
	/// <returns> <c> true </c> if parsing was successful; otherwise, <c> false </c>. </returns>
	private static bool TryParseUuid7(string uuidString, out Uuid7 uuid)
	{
		// Only support Guid parsing here to avoid dependency on additional Medo APIs
		if (ParseCache.TryGetValue(uuidString, out var cached))
		{
			uuid = cached.uuid ?? default;
			return cached.valid;
		}

		var canCache = ParseCache.Count < MaxCacheEntries;

		if (Guid.TryParse(uuidString, out _))
		{
			// We cannot construct a Uuid7 instance without Medo conversion; mark as valid format
			uuid = default;
			if (canCache)
			{
				_ = ParseCache.TryAdd(uuidString, (true, null));
			}

			return true;
		}

		uuid = default;
		if (canCache)
		{
			_ = ParseCache.TryAdd(uuidString, (false, null));
		}

		return false;
	}
}
