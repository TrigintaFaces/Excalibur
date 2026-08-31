// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

namespace Excalibur.Compliance.Aws;

/// <summary>
/// Owns the tags this provider uses to record what AWS KMS does not model for it: which version of a
/// logical key a CMK is, what it is for, and whether it has been rotated out.
/// </summary>
/// <remarks>
/// <para>
/// AWS symmetric customer master keys have no consumer-visible version of their own. This provider rotates
/// by creating a new CMK and repointing an alias, so each version is a distinct key — and the version
/// number, the purpose, and the superseded marker are all ours to record. They live on the key as tags
/// rather than in process memory because this provider advertises durable key management: a dictionary
/// does not survive a restart and is not shared between instances, so authoritative state cannot live
/// there.
/// </para>
/// <para>
/// Extracted from the provider deliberately. Keeping the tag vocabulary in one place gives the version
/// model a single home, and keeps the provider's own coupling from growing every time the model does.
/// </para>
/// </remarks>
internal static class AwsKmsKeyTags
{
    /// <summary>Tag recording which version of a logical key a CMK is.</summary>
    public const string Version = "ExcaliburKeyVersion";

    /// <summary>Tag recording that a CMK has been rotated out and must not receive new encryptions.</summary>
    /// <remarks>
    /// Presence of this tag is what makes a key decrypt-only. It is deliberately NOT expressed by disabling
    /// the key: AWS documents a disabled key as unusable for every cryptographic operation, decryption
    /// included, so disabling a superseded key makes all ciphertext under it unreadable until an operator
    /// re-enables it. The key stays enabled and is marked instead.
    /// </remarks>
    public const string SupersededAt = "ExcaliburSupersededAt";

    /// <summary>Tag recording what a key is for.</summary>
    public const string Purpose = "Purpose";

    /// <summary>
    /// Reads a key's tags, returning an empty set when they cannot be read.
    /// </summary>
    /// <param name="client">The KMS client.</param>
    /// <param name="kmsKeyId">The KMS key identifier or ARN.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tags on the key.</returns>
    /// <remarks>
    /// Best-effort by design: a caller holding DescribeKey but not ListResourceTags should still be able to
    /// read a key. When tags are unavailable the metadata falls back to version 1 with no purpose, which is
    /// what this provider reported before it recorded either.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, string>> ReadAsync(
        IAmazonKeyManagementService client,
        string kmsKeyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.ListResourceTagsAsync(
                new ListResourceTagsRequest { KeyId = kmsKeyId },
                cancellationToken).ConfigureAwait(false);

            var tags = new Dictionary<string, string>(StringComparer.Ordinal);

            // An untagged key answers with no Tags collection at all, not an empty one: this SDK major
            // leaves collection properties null unless the service populated them. Iterating that directly
            // threw past the catch below and turned "this key has no tags" -- the ordinary state of any key
            // not created by this provider -- into a NullReferenceException out of a read.
            foreach (var tag in response.Tags ?? [])
            {
                if (!string.IsNullOrEmpty(tag.TagKey))
                {
                    tags[tag.TagKey] = tag.TagValue ?? string.Empty;
                }
            }

            return tags;
        }
        catch (AmazonKeyManagementServiceException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Marks a key as superseded, so it is reported decrypt-only while remaining able to decrypt.
    /// </summary>
    /// <param name="client">The KMS client.</param>
    /// <param name="kmsKeyId">The KMS key identifier or ARN.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public static async Task MarkSupersededAsync(
        IAmazonKeyManagementService client,
        string kmsKeyId,
        CancellationToken cancellationToken) =>
        _ = await client.TagResourceAsync(
            new TagResourceRequest
            {
                KeyId = kmsKeyId,
                Tags =
                [
                    new Tag
                    {
                        TagKey = SupersededAt,
                        TagValue = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    },
                ],
            },
            cancellationToken).ConfigureAwait(false);

    /// <summary>Reads the version a CMK's tags record, defaulting to 1.</summary>
    /// <param name="tags">The key's tags.</param>
    /// <returns>The recorded version.</returns>
    public static int VersionOf(IReadOnlyDictionary<string, string>? tags) =>
        tags is not null
        && tags.TryGetValue(Version, out var raw)
        && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 1;

    /// <summary>Reads the purpose a CMK's tags record.</summary>
    /// <param name="tags">The key's tags.</param>
    /// <returns>The recorded purpose, or <see langword="null"/>.</returns>
    public static string? PurposeOf(IReadOnlyDictionary<string, string>? tags) =>
        tags is not null && tags.TryGetValue(Purpose, out var value) ? value : null;

    /// <summary>Reports whether a CMK has been rotated out.</summary>
    /// <param name="tags">The key's tags.</param>
    /// <returns><see langword="true"/> when the key is superseded.</returns>
    public static bool IsSuperseded(IReadOnlyDictionary<string, string>? tags) =>
        tags is not null && tags.ContainsKey(SupersededAt);
}
