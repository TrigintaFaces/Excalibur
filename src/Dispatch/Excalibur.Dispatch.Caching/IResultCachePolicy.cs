// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Determines whether results should be cached for a dispatch message.
/// </summary>
public interface IResultCachePolicy
{
	/// <summary>
	/// Returns <c> true </c> when the result should be cached.
	/// </summary>
	/// <param name="message"> The dispatch message. </param>
	/// <param name="result"> The action result. </param>
	/// <returns> <c> true </c> to cache the result. </returns>
	bool ShouldCache(IDispatchMessage message, object? result);
}

/// <summary>
/// Determines whether results should be cached for a specific message type.
/// </summary>
/// <typeparam name="TMessage"> The message type. </typeparam>
public interface IResultCachePolicy<in TMessage>
	where TMessage : IDispatchMessage
{
	/// <summary>
	/// Returns <c> true </c> when the result should be cached.
	/// </summary>
	/// <param name="message"> The dispatch message. </param>
	/// <param name="result"> The action result. </param>
	/// <returns> <c> true </c> to cache the result. </returns>
	bool ShouldCache(TMessage message, object? result);
}
