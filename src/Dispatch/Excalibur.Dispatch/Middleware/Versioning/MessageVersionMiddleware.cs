// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Versioning;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Middleware.Versioning;

/// <summary>
/// Middleware that handles message schema versioning by inspecting transport headers
/// and delegating to registered <see cref="IMessageVersionMapper"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// When a message arrives with an <c>x-message-version</c> header that differs from
/// the expected version, this middleware looks for a registered mapper that can transform
/// the message to the expected version.
/// </para>
/// <para>
/// If no mapper is registered or the version matches, the message passes through unchanged.
/// </para>
/// </remarks>
internal sealed partial class MessageVersionMiddleware : IDispatchMiddleware
{
	/// <summary>
	/// The transport header key for message schema version.
	/// </summary>
	internal const string VersionHeaderKey = "x-message-version";

	/// <summary>
	/// The context key for the expected message version.
	/// </summary>
	internal const string ExpectedVersionKey = "x-expected-message-version";

	private readonly IEnumerable<IMessageVersionMapper> _mappers;
	private readonly ILogger<MessageVersionMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageVersionMiddleware"/> class.
	/// </summary>
	/// <param name="mappers">The registered version mappers.</param>
	/// <param name="logger">The logger instance.</param>
	public MessageVersionMiddleware(
		IEnumerable<IMessageVersionMapper> mappers,
		ILogger<MessageVersionMiddleware> logger)
	{
		_mappers = mappers ?? throw new ArgumentNullException(nameof(mappers));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Serialization;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Check for version header
		if (!TryGetVersion(context, VersionHeaderKey, out var fromVersion))
		{
			// No version header -- pass through
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		if (!TryGetVersion(context, ExpectedVersionKey, out var toVersion))
		{
			// No expected version -- pass through
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		if (fromVersion == toVersion)
		{
			// Version matches -- no mapping needed
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Find a mapper
		var messageType = message.GetType().FullName ?? message.GetType().Name;
		foreach (var mapper in _mappers)
		{
			if (mapper.CanMap(messageType, fromVersion, toVersion))
			{
				LogVersionMapping(messageType, fromVersion, toVersion);
				// Mapper found -- future: transform the message payload
				// For now, log and pass through (concrete mappers deferred to future sprint)
				break;
			}
		}

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	private static bool TryGetVersion(IMessageContext context, string key, out int version)
	{
		version = 0;
		if (context.Items.TryGetValue(key, out var value) && value is int v)
		{
			version = v;
			return true;
		}

		if (value is string s && int.TryParse(s, out v))
		{
			version = v;
			return true;
		}

		return false;
	}

	[LoggerMessage(2210, LogLevel.Information,
		"Message schema version mapping: '{MessageType}' from v{FromVersion} to v{ToVersion}.")]
	private partial void LogVersionMapping(string messageType, int fromVersion, int toVersion);
}
