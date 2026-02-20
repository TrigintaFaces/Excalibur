// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Composite deduplication strategy that combines multiple strategies.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="CompositeDeduplicationStrategy" /> class. </remarks>
/// <param name="primary"> Primary deduplication strategy. </param>
/// <param name="secondary"> Secondary deduplication strategies. </param>
/// <param name="options"> Deduplication options. </param>
public sealed class CompositeDeduplicationStrategy(
	IDeduplicationStrategy primary,
	IEnumerable<IDeduplicationStrategy> secondary,
	DeduplicationOptions options) : IDeduplicationStrategy
{
	private readonly IDeduplicationStrategy _primary = primary ?? throw new ArgumentNullException(nameof(primary));
	private readonly IDeduplicationStrategy[] _secondary = secondary?.ToArray() ?? [];
	private readonly DeduplicationOptions _options = options ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public TimeSpan DefaultExpiration => _options.DeduplicationWindow;

	/// <inheritdoc />
	public string GenerateDeduplicationId(string messageBody, IDictionary<string, object>? messageAttributes = null) =>

		// Use primary strategy for sync generation
		_primary.GenerateDeduplicationId(messageBody, messageAttributes);

	/// <inheritdoc />
	public async Task<string> GenerateIdAsync(string messageBody, IDictionary<string, object>? messageAttributes,
		CancellationToken cancellationToken)
	{
		// Generate IDs from all strategies and combine
		var primaryId = await _primary.GenerateIdAsync(messageBody, messageAttributes, cancellationToken).ConfigureAwait(false);

		if (_secondary.Length == 0)
		{
			return primaryId;
		}

		var ids = new List<string> { primaryId };
		foreach (var strategy in _secondary)
		{
			var id = await strategy.GenerateIdAsync(messageBody, messageAttributes, cancellationToken).ConfigureAwait(false);
			ids.Add(id);
		}

		// Combine all IDs
		return string.Join(':', ids);
	}

	/// <inheritdoc />
	public async Task<bool> IsDuplicateAsync(string deduplicationId, CancellationToken cancellationToken) =>

		// Check primary first
		await _primary.IsDuplicateAsync(deduplicationId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task MarkAsProcessedAsync(string deduplicationId, TimeSpan? expiration,
		CancellationToken cancellationToken)
	{
		// Mark in primary
		await _primary.MarkAsProcessedAsync(deduplicationId, expiration, cancellationToken).ConfigureAwait(false);

		// Also mark in secondary strategies
		foreach (var strategy in _secondary)
		{
			await strategy.MarkAsProcessedAsync(deduplicationId, expiration, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task<bool> RemoveAsync(string deduplicationId, CancellationToken cancellationToken)
	{
		// Remove from all strategies
		var primaryResult = await _primary.RemoveAsync(deduplicationId, cancellationToken).ConfigureAwait(false);

		foreach (var strategy in _secondary)
		{
			_ = await strategy.RemoveAsync(deduplicationId, cancellationToken).ConfigureAwait(false);
		}

		return primaryResult;
	}
}
