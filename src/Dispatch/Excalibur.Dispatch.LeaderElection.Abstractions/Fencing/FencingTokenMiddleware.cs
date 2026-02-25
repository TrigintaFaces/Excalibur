// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// Middleware that validates fencing tokens on incoming messages to prevent
/// stale leader operations in leader election scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This middleware checks for a fencing token in the message context's Items
/// dictionary (key: <c>FencingToken</c>) and validates it against the current
/// token for the resource (identified by <c>FencingResourceId</c> in Items).
/// </para>
/// <para>
/// If the token is stale (lower than the current token), the message is rejected
/// with a failed result to prevent split-brain scenarios.
/// </para>
/// </remarks>
/// <param name="provider">The fencing token provider for validation.</param>
/// <param name="logger">The logger for diagnostic output.</param>
[AppliesTo(MessageKinds.Action | MessageKinds.Event)]
public sealed partial class FencingTokenMiddleware(
	IFencingTokenProvider provider,
	ILogger<FencingTokenMiddleware> logger) : IDispatchMiddleware
{
	/// <summary>
	/// The context item key for the fencing token value.
	/// </summary>
	public const string FencingTokenKey = "FencingToken";

	/// <summary>
	/// The context item key for the fencing resource identifier.
	/// </summary>
	public const string FencingResourceIdKey = "FencingResourceId";

	private readonly IFencingTokenProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
	private readonly ILogger<FencingTokenMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

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

		// Only validate if fencing token is present in context
		if (!context.ContainsItem(FencingTokenKey) || !context.ContainsItem(FencingResourceIdKey))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var token = context.GetItem<long>(FencingTokenKey);
		var resourceId = context.GetItem<string>(FencingResourceIdKey);

		if (string.IsNullOrEmpty(resourceId))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var isValid = await _provider.ValidateTokenAsync(resourceId, token, cancellationToken)
			.ConfigureAwait(false);

		if (!isValid)
		{
			LogStaleFencingToken(token, resourceId, context.MessageId ?? string.Empty);
			return MessageResult.Failed(
				$"Fencing token {token} is stale for resource '{resourceId}'. Operation rejected.");
		}

		LogFencingTokenValid(token, resourceId, context.MessageId ?? string.Empty);
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	[LoggerMessage(3000, LogLevel.Warning,
		"Stale fencing token {Token} for resource {ResourceId} on message {MessageId} - rejecting operation")]
	private partial void LogStaleFencingToken(long token, string resourceId, string messageId);

	[LoggerMessage(3001, LogLevel.Debug,
		"Fencing token {Token} validated for resource {ResourceId} on message {MessageId}")]
	private partial void LogFencingTokenValid(long token, string resourceId, string messageId);
}
