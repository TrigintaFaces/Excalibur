// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Example zero-allocation validation middleware.
/// </summary>
public sealed class ZeroAllocationValidationMiddleware : ZeroAllocationMiddlewareBase
{
	/// <inheritdoc />
	public override DispatchMiddlewareStage Stage => DispatchMiddlewareStage.Validation;

	/// <inheritdoc />
	[RequiresUnreferencedCode("Uses reflection which may break with AOT compilation")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public override ValueTask<(MiddlewareResult Result, MiddlewareContext Context)> ProcessAsync(
			MessageEnvelope<IDispatchMessage> envelope,
			MiddlewareContext context,
	CancellationToken cancellationToken)
	{
		// Perform validation (example)
		if (envelope.Message == null)
		{
			return new ValueTask<(MiddlewareResult, MiddlewareContext)>(
				(MiddlewareResult.StopWithError("Message cannot be null"), context));
		}

		// Zero-allocation validation - no state storage needed
		return new ValueTask<(MiddlewareResult, MiddlewareContext)>((MiddlewareResult.Continue(), context));
	}
}
