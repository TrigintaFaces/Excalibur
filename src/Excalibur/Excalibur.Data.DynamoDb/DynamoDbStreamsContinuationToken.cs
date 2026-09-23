// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Encodes and decodes the opaque continuation token for a DynamoDB Streams change feed.
/// </summary>
/// <remarks>
/// <para>
/// A change feed exposes one continuation token, but a DynamoDB stream has many shards and each has its
/// own sequence space. There is no correct way to apply a single sequence number to every shard, so the
/// whole per-shard position map is encoded inside the one opaque string the consumer stores.
/// </para>
/// <para>
/// <b>The token is opaque by contract.</b> Its layout is an implementation detail and is deliberately not
/// documented for consumers: the moment a consumer can construct or parse one, the encoding becomes a
/// contract and can never change. The consumer's obligation is only to store the string and hand it back.
/// </para>
/// <para>
/// The payload is tagged with a provider identifier and a version for two reasons, both of which are
/// failure modes rather than conveniences. A token produced by a different provider must be refused rather
/// than silently misread, and the encoding must be changeable later without a token from an older build
/// being interpreted under new rules. An unrecognised token is always a refusal, never a silent restart:
/// restarting from either end of a stream is a data defect, and a silent one.
/// </para>
/// </remarks>
internal static class DynamoDbStreamsContinuationToken
{
	/// <summary>Identifies the provider that produced a token, so another provider's token is refused.</summary>
	private const string ProviderTag = "ddbstreams";

	/// <summary>The encoding version. Increment when the payload shape changes.</summary>
	private const int CurrentVersion = 1;

	private const char Separator = ':';

	/// <summary>
	/// Encodes a per-shard sequence map into a single opaque token.
	/// </summary>
	/// <param name="shardPositions">The last sequence number read per shard id.</param>
	/// <returns>The opaque token, or <see langword="null"/> when no position has been established yet.</returns>
	public static string? Encode(IReadOnlyDictionary<string, string> shardPositions)
	{
		ArgumentNullException.ThrowIfNull(shardPositions);

		if (shardPositions.Count == 0)
		{
			// No position is not the same as position zero. A token that encoded an empty map would read
			// back as "resume from nowhere", which has no honest iterator type.
			return null;
		}

		var json = JsonSerializer.Serialize(
			shardPositions,
			DynamoDbStreamsContinuationTokenSerializerContext.Default.IReadOnlyDictionaryStringString);

		var payload = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

		return string.Create(
			System.Globalization.CultureInfo.InvariantCulture,
			$"{ProviderTag}{Separator}{CurrentVersion}{Separator}{payload}");
	}

	/// <summary>
	/// Decodes an opaque token back into its per-shard sequence map.
	/// </summary>
	/// <param name="token">A token previously produced by <see cref="Encode"/>.</param>
	/// <returns>The last sequence number read per shard id.</returns>
	/// <exception cref="ArgumentException">
	/// The token was not produced by this provider, carries a version this build cannot read, or is
	/// malformed. Each is a refusal rather than a restart, because resuming from the wrong position
	/// silently replays or silently skips.
	/// </exception>
	public static IReadOnlyDictionary<string, string> Decode(string token)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(token);

		var parts = token.Split(Separator, 3);
		if (parts.Length != 3)
		{
			throw new ArgumentException(
				"The change-feed continuation token is malformed. A continuation token is opaque and may only be passed back to the provider that issued it.",
				nameof(token));
		}

		if (!string.Equals(parts[0], ProviderTag, StringComparison.Ordinal))
		{
			throw new ArgumentException(
				$"The change-feed continuation token was issued by a different provider ('{parts[0]}'), not by the DynamoDB Streams change feed. A token may only be handed back to the provider that produced it.",
				nameof(token));
		}

		if (!int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var version)
			|| version != CurrentVersion)
		{
			throw new ArgumentException(
				$"The change-feed continuation token uses encoding version '{parts[1]}', which this build cannot read (it understands version {CurrentVersion}). Start a new subscription from an explicit position rather than resuming from a token this build cannot interpret.",
				nameof(token));
		}

		Dictionary<string, string>? positions;
		try
		{
			var json = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[2]));
			positions = JsonSerializer.Deserialize(
				json,
				DynamoDbStreamsContinuationTokenSerializerContext.Default.DictionaryStringString);
		}
		catch (Exception ex) when (ex is FormatException or JsonException or DecoderFallbackException)
		{
			throw new ArgumentException(
				"The change-feed continuation token payload could not be read. A continuation token is opaque and must be stored and returned unmodified.",
				nameof(token),
				ex);
		}

		if (positions is null || positions.Count == 0)
		{
			throw new ArgumentException(
				"The change-feed continuation token carries no shard positions. Pass no token to start from the configured position rather than an empty one.",
				nameof(token));
		}

		return positions;
	}
}

/// <summary>
/// Source-generated serializer context for the continuation-token payload, so the token can be read and
/// written under trimming and native AOT without reflection.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
internal sealed partial class DynamoDbStreamsContinuationTokenSerializerContext : JsonSerializerContext;
