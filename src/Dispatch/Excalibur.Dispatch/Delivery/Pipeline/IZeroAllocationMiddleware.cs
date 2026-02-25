// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery.Pipeline;

/// <summary>
/// Base interface for zero-allocation middleware.
/// </summary>
public interface IZeroAllocationMiddleware
{
	/// <summary>
	/// Gets the middleware stage.
	/// </summary>
	/// <value>
	/// The middleware stage.
	/// </value>
	DispatchMiddlewareStage Stage { get; }

	/// <summary>
	/// Processes the message without allocations.
	/// </summary>
	[RequiresUnreferencedCode("Uses reflection which may break with AOT compilation")]
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	ValueTask<(MiddlewareResult Result, MiddlewareContext Context)> ProcessAsync(
			MessageEnvelope<IDispatchMessage> envelope,
			MiddlewareContext context,
	CancellationToken cancellationToken);
}
