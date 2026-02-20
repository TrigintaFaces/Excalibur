// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Deduplication strategy based on message content hash.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ContentHashDeduplicationStrategy" /> class. </remarks>
/// <param name="store"> Deduplication store. </param>
/// <param name="options"> Deduplication options. </param>
public sealed class ContentHashDeduplicationStrategy(IDeduplicationStore store, DeduplicationOptions options) : IDeduplicationStrategy
{
	private readonly IDeduplicationStore _store = store ?? throw new ArgumentNullException(nameof(store));
	private readonly DeduplicationOptions _options = options ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public TimeSpan DefaultExpiration => _options.DeduplicationWindow;

	/// <inheritdoc />
	public string GenerateDeduplicationId(string messageBody, IDictionary<string, object>? messageAttributes = null)
	{
		if (string.IsNullOrEmpty(messageBody))
		{
			throw new ArgumentNullException(nameof(messageBody));
		}

		// Generate hash from message body
		var bytes = Encoding.UTF8.GetBytes(messageBody);
		var hashBytes = SHA256.HashData(bytes);

		// Convert to hex string
		return Convert.ToHexString(hashBytes);
	}

	/// <inheritdoc />
	public Task<string> GenerateIdAsync(string messageBody, IDictionary<string, object>? messageAttributes,
		CancellationToken cancellationToken) =>
		Task.FromResult(GenerateDeduplicationId(messageBody, messageAttributes));

	/// <inheritdoc />
	public async Task<bool> IsDuplicateAsync(string deduplicationId, CancellationToken cancellationToken)
	{
		var context = new DeduplicationContext { ProcessorId = Environment.MachineName, Source = "ContentHash", MessageType = "Generic" };
		var result = await _store.CheckAndMarkAsync(deduplicationId, context, cancellationToken).ConfigureAwait(false);
		return result.IsDuplicate;
	}

	/// <inheritdoc />
	public async Task MarkAsProcessedAsync(string deduplicationId, TimeSpan? expiration,
		CancellationToken cancellationToken)
	{
		var context = new DeduplicationContext { ProcessorId = Environment.MachineName, Source = "ContentHash", MessageType = "Generic" };

		// Note: expiration is handled internally by the store based on options
		_ = await _store.CheckAndMarkAsync(deduplicationId, context, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> RemoveAsync(string deduplicationId, CancellationToken cancellationToken) =>
		await _store.RemoveAsync(deduplicationId, cancellationToken).ConfigureAwait(false);
}
