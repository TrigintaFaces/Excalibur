// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Internal extension contract for handler invocation paths that can return <see cref="ValueTask{TResult}"/>.
/// </summary>
internal interface IValueTaskHandlerInvoker
{
	/// <summary>
	/// Invokes a handler and returns a ValueTask for allocation-free synchronous completion.
	/// </summary>
	ValueTask<object?> InvokeValueTaskAsync(object handler, IDispatchMessage message, CancellationToken cancellationToken);
}
