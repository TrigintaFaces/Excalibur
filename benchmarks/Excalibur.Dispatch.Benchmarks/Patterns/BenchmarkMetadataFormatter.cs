// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using MemoryPack;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Member formatter for the benchmark event's string-valued metadata workload.
/// This is not a general object metadata formatter or a production metadata limit.
/// </summary>
internal sealed class BenchmarkMetadataFormatter : MemoryPackFormatter<IDictionary<string, object>>
{
	private const int MaxEntries = 1024;

	public override void Serialize<TBufferWriter>(
		ref MemoryPackWriter<TBufferWriter> writer,
		scoped ref IDictionary<string, object>? value)
	{
		if (value is null)
		{
			writer.WriteNullCollectionHeader();
			return;
		}

		if (value.Count > MaxEntries)
			throw new InvalidOperationException("Benchmark metadata exceeds its entry limit.");

		writer.WriteCollectionHeader(value.Count);
		foreach (var pair in value)
		{
			if (pair.Key is null || pair.Value is not (null or string))
				throw new InvalidOperationException("Benchmark metadata supports non-null keys and string/null values only.");

			writer.WriteString(pair.Key);
			writer.WriteString((string?)pair.Value);
		}
	}

	public override void Deserialize(ref MemoryPackReader reader, scoped ref IDictionary<string, object>? value)
	{
		if (!reader.TryReadCollectionHeader(out var count))
		{
			value = null;
			return;
		}

		// Even empty/null strings require two four-byte headers per entry.
		// Validate before allocating the dictionary; publish only a complete member.
		if (count < 0 || count > MaxEntries || count > reader.Remaining / 8)
			throw new InvalidOperationException("Malformed benchmark metadata count.");

		var result = new Dictionary<string, object>(count, StringComparer.Ordinal);
		for (var i = 0; i < count; i++)
		{
			var key = reader.ReadString() ?? throw new InvalidOperationException("Null benchmark metadata key.");
			var text = reader.ReadString();
			if (!result.TryAdd(key, text!))
				throw new InvalidOperationException("Duplicate benchmark metadata key.");
		}

		value = result;
	}
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class BenchmarkMetadataFormatterAttribute
	: MemoryPackCustomFormatterAttribute<IDictionary<string, object>>
{
	private static readonly BenchmarkMetadataFormatter Formatter = new();

	public override IMemoryPackFormatter<IDictionary<string, object>> GetFormatter() => Formatter;
}
