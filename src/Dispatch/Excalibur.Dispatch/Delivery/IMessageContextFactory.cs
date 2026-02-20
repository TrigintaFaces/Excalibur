// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Factory for creating message context instances.
/// </summary>
public interface IMessageContextFactory
{
	/// <summary>
	/// Creates a new message context.
	/// </summary>
	/// <returns> A new message context instance. </returns>
	IMessageContext CreateContext();

	/// <summary>
	/// Creates a new message context with the specified properties.
	/// </summary>
	/// <param name="properties"> Initial properties for the context. </param>
	/// <returns> A new message context instance. </returns>
	IMessageContext CreateContext(IDictionary<string, object> properties);

	/// <summary>
	/// Creates a child context from a parent context, propagating cross-cutting identifiers.
	/// </summary>
	/// <param name="parent">The parent context to derive from.</param>
	/// <returns>A new message context with propagated identifiers from the parent.</returns>
	/// <remarks>
	/// The child context propagates:
	/// <list type="bullet">
	/// <item><description>CorrelationId - Copied from parent</description></item>
	/// <item><description>TenantId - Copied from parent</description></item>
	/// <item><description>UserId - Copied from parent</description></item>
	/// <item><description>SessionId - Copied from parent</description></item>
	/// <item><description>WorkflowId - Copied from parent</description></item>
	/// <item><description>TraceParent - Copied from parent</description></item>
	/// <item><description>Source - Copied from parent</description></item>
	/// <item><description>CausationId - Set to parent's MessageId</description></item>
	/// </list>
	/// A new MessageId is generated for the child context.
	/// </remarks>
	IMessageContext CreateChildContext(IMessageContext parent);

	/// <summary>
	/// Returns a message context to the pool after use.
	/// </summary>
	/// <param name="context">The context to return.</param>
	/// <remarks>
	/// <para>
	/// This method should be called when a message context is no longer needed.
	/// For pooled factory implementations, the context is returned to the pool for reuse.
	/// For non-pooled implementations, this is a no-op.
	/// </para>
	/// <para>
	/// Usage pattern:
	/// <code>
	/// var context = factory.CreateContext();
	/// try
	/// {
	///     await ProcessAsync(context);
	/// }
	/// finally
	/// {
	///     factory.Return(context);
	/// }
	/// </code>
	/// </para>
	/// </remarks>
	void Return(IMessageContext context);
}
