// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// A decorator that automatically upcasts versioned messages after deserialization.
/// </summary>
/// <remarks>
/// <para>
/// This decorator wraps <see cref="IConfluentFormatDeserializer"/> and applies
/// upcasting for messages implementing <see cref="IVersionedMessage"/> when their
/// version is below the latest registered version.
/// </para>
/// <para>
/// Upcasting uses the existing BFS-based path finding in <see cref="IUpcastingPipeline"/>
/// to automatically migrate messages through intermediate versions (e.g., V1 → V2 → V3).
/// </para>
/// </remarks>
public sealed class UpcastingConfluentDeserializer : IConfluentFormatDeserializer
{
	private readonly IConfluentFormatDeserializer _inner;
	private readonly IUpcastingPipeline _upcastingPipeline;
	private readonly ILogger<UpcastingConfluentDeserializer> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="UpcastingConfluentDeserializer"/> class.
	/// </summary>
	/// <param name="inner">The inner deserializer to decorate.</param>
	/// <param name="upcastingPipeline">The upcasting pipeline for version migration.</param>
	/// <param name="logger">The logger.</param>
	public UpcastingConfluentDeserializer(
		IConfluentFormatDeserializer inner,
		IUpcastingPipeline upcastingPipeline,
		ILogger<UpcastingConfluentDeserializer> logger)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_upcastingPipeline = upcastingPipeline ?? throw new ArgumentNullException(nameof(upcastingPipeline));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<T> DeserializeAsync<T>(
		string topic,
		ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken)
	{
		var result = await _inner.DeserializeAsync<T>(topic, data, cancellationToken)
			.ConfigureAwait(false);

		// Check if upcasting is needed using pattern matching
		if (result is IVersionedMessage versioned and IDispatchMessage dispatchMessage)
		{
			var upcasted = TryUpcast(versioned, dispatchMessage);
			if (upcasted != null)
			{
				return (T)upcasted;
			}
		}

		return result;
	}

	/// <inheritdoc/>
	public async Task<DeserializationResult> DeserializeAsync(
		string topic,
		ReadOnlyMemory<byte> data,
		CancellationToken cancellationToken)
	{
		var result = await _inner.DeserializeAsync(topic, data, cancellationToken)
			.ConfigureAwait(false);

		// Check if upcasting is needed using pattern matching
		if (result.Message is IVersionedMessage versioned and IDispatchMessage dispatchMessage)
		{
			var upcasted = TryUpcast(versioned, dispatchMessage);
			if (upcasted != null)
			{
				// Return new result with upcasted message
				var newVersion = (upcasted as IVersionedMessage)?.Version ?? result.Version;
				return new DeserializationResult(upcasted, upcasted.GetType(), result.SchemaId, newVersion);
			}
		}

		return result;
	}

	private object? TryUpcast(IVersionedMessage versioned, IDispatchMessage message)
	{
		var messageType = versioned.MessageType;
		var currentVersion = versioned.Version;
		var latestVersion = _upcastingPipeline.GetLatestVersion(messageType);

		// No upcasting needed if already at latest version or no versions registered
		if (latestVersion == 0 || currentVersion >= latestVersion)
		{
			return null;
		}

		// Check if path exists
		if (!_upcastingPipeline.CanUpcast(messageType, currentVersion, latestVersion))
		{
			_logger.LogWarning(
				"No upcast path exists for {MessageType} from V{CurrentVersion} to V{LatestVersion}",
				messageType,
				currentVersion,
				latestVersion);
			return null;
		}

		_logger.LogDebug(
			"Upcasting {MessageType} from V{CurrentVersion} to V{LatestVersion}",
			messageType,
			currentVersion,
			latestVersion);

		// Perform the upcast via the pipeline (handles multi-hop automatically)
		var upcasted = _upcastingPipeline.Upcast(message);

		_logger.LogDebug(
			"Successfully upcasted {MessageType} from V{CurrentVersion} to V{NewVersion}",
			messageType,
			currentVersion,
			(upcasted as IVersionedMessage)?.Version ?? latestVersion);

		return upcasted;
	}
}
