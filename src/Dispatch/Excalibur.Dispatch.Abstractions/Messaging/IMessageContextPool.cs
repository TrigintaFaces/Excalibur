// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Defines a pool for reusable message context instances.
/// </summary>
public interface IMessageContextPool
{
	/// <summary>
	/// Rents a message context from the pool without initializing it with a message.
	/// </summary>
	/// <remarks>
	/// Use this overload when the message will be set later during the dispatch pipeline.
	/// This enables lazy message binding for scenarios where the context needs to be
	/// created before the message is available.
	/// </remarks>
	/// <returns>A message context from the pool with <see cref="IMessageContext.Message"/> unset.</returns>
	IMessageContext Rent();

	/// <summary>
	/// Rents a message context from the pool, initializing it with the specified message.
	/// </summary>
	/// <param name="message">The dispatch message to associate with the context.</param>
	/// <returns>A message context from the pool.</returns>
	IMessageContext Rent(IDispatchMessage message);

	/// <summary>
	/// Returns a message context to the pool for reuse.
	/// </summary>
	/// <param name="context">The context to return.</param>
	void ReturnToPool(IMessageContext context);
}
