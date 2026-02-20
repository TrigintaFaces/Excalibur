// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Example zero-allocation middleware for context initialization.
/// </summary>
public sealed class ZeroAllocationContextMiddleware : ZeroAllocationMiddlewareBase
{
	/// <inheritdoc />
	public override DispatchMiddlewareStage Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	[RequiresUnreferencedCode("Uses reflection which may break with AOT compilation")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public override ValueTask<(MiddlewareResult Result, MiddlewareContext Context)> ProcessAsync(
			MessageEnvelope<IDispatchMessage> envelope,
			MiddlewareContext context,
	CancellationToken cancellationToken)
	{
		// Access metadata without allocation
		var metadata = envelope.Metadata;

		// Validate message has required metadata
		if (string.IsNullOrEmpty(metadata.MessageId))
		{
			return new ValueTask<(MiddlewareResult, MiddlewareContext)>(
				(MiddlewareResult.StopWithError("Message ID is required"), context));
		}

		// Update context if needed (through the full context reference)
		if (envelope.Context != null)
		{
			// Set context values without allocation
			envelope.Context.MessageId = metadata.MessageId;

			// Note: DeliveryCount is not available in the record-based MessageMetadata It would need to be extracted from headers or
			// context if needed
			if (envelope.Context is MessageContext && envelope.Headers.TryGetValue("DeliveryCount", out var dc) &&
				int.TryParse(dc, out var deliveryCount))
			{
				envelope.Context.DeliveryCount = deliveryCount;
			}
		}

		// Continue execution
		return new ValueTask<(MiddlewareResult, MiddlewareContext)>((MiddlewareResult.Continue(), context));
	}
}
