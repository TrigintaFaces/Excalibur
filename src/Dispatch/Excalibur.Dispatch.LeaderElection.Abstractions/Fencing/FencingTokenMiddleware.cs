// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

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
/// If the token is stale (lower than the current token), the message is rejected with a failed result.
/// </para>
/// <para>
/// Validation faults and cancellation propagate without invoking the next delegate. They are not
/// converted to a stale-token result: an unavailable authority is not proof of staleness, and preserving
/// the original failure allows outer retry/classification and cancellation policies to act on it.
/// </para>
/// <para>
/// <b>THIS IS A PRE-FLIGHT CHECK AND IT DOES NOT FENCE THE HANDLER'S WRITES. Read this before relying
/// on it.</b> The validation happens, and then the handler runs. A successor can advance the
/// high-water mark in the interval between the check returning and the handler's write, so a message
/// admitted here can still be processed by a superseded leader. That is not a defect in this
/// middleware, it is what a check-then-act sequence is: the validation describes an instant that has
/// already passed by the time the handler executes, and no comparison performed here can change that.
/// </para>
/// <para>
/// <b>What it DOES establish, which is worth having:</b> a message whose token was ALREADY superseded
/// at the moment of the check is refused before the handler runs at all, which turns a stale delivery
/// into a reported failure instead of a silent reprocessing. It narrows the window; it does not close
/// it, and it cannot prevent split-brain on its own.
/// </para>
/// <para>
/// <b>What closes it</b> is enforcement inside the write: a conditional update in the handler's own
/// store, predicated on a stored high-water column, so the store either applies the write or rejects
/// it in one step. Only the store holding the data can make that atomic with the write, so it is
/// outside this framework. A handler whose correctness depends on not being superseded must do that,
/// with this middleware as a cheap early rejection rather than as the guarantee.
/// </para>
/// <para>
/// Fencing tokens are <b>strictly positive</b>. A context item that is present but holds a non-positive
/// value is not a token -- it is either an unset value or an item of the wrong type -- and the message is
/// rejected rather than validated. Zero in particular must never reach validation: against a provider
/// whose own high-water mark is unset, a <c>token &gt;= mark</c> comparison would admit it.
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

		// A FENCING TOKEN IS STRICTLY POSITIVE, SO A NON-POSITIVE VALUE IS NOT A TOKEN AND MUST BE REFUSED
		// RATHER THAN VALIDATED. The read above cannot distinguish a genuine zero from an item of the wrong
		// type -- GetItem<long> yields default(long) for either -- and zero is the one value that a provider
		// whose high-water mark is also unset-as-zero would ADMIT under a `token >= mark` comparison. The
		// message asserted fencing by carrying both keys, so passing it through unchecked is not the
		// conservative option: refusing is.
		if (token <= 0)
		{
			LogNonPositiveFencingToken(token, resourceId, context.MessageId ?? string.Empty);
			return MessageResult.Failed(
				$"Fencing token {token} is not a valid token for resource '{resourceId}'. Operation rejected.");
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

	[LoggerMessage(3002, LogLevel.Warning,
		"Fencing token {Token} for resource {ResourceId} on message {MessageId} is not strictly positive - rejecting operation")]
	private partial void LogNonPositiveFencingToken(long token, string resourceId, string messageId);

	[LoggerMessage(3001, LogLevel.Debug,
		"Fencing token {Token} validated for resource {ResourceId} on message {MessageId}")]
	private partial void LogFencingTokenValid(long token, string resourceId, string messageId);
}
