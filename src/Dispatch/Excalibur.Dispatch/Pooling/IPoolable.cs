// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// Defines a contract for objects that support automated cleanup and reset operations for object pooling scenarios.
/// </summary>
/// <remarks>
/// This interface enables objects to participate in pooling systems by providing a standardized mechanism for returning to a clean,
/// reusable state. Objects implementing this interface can be efficiently reused by object pools without manual cleanup code.
/// <para> <strong> Implementation Guidelines: </strong> </para>
/// <para>The Reset method should restore the object to the same state as a newly constructed instance, including:</para>
/// <para>
/// - Clearing collections and resetting field values
/// - Disposing or releasing internal resources appropriately
/// - Resetting state machines or workflow positions
/// - Clearing event handlers and subscriptions
/// - Reverting configuration to defaults.
/// </para>
/// <para> <strong> Performance Considerations: </strong> </para>
/// Reset operations should be lightweight and fast since they occur in the object pooling hot path. Avoid expensive operations like I/O,
/// network calls, or complex computations during reset.
/// <para> <strong> Thread Safety: </strong> </para>
/// Reset implementations must be thread-safe if the pooled objects will be used concurrently. Consider using atomic operations or
/// appropriate synchronization.
/// <para> <strong> Common Use Cases: </strong> </para>
/// - Message objects that accumulate data during processing
/// - Buffer objects that need to be cleared between uses
/// - Connection objects that need to reset state
/// - Temporary objects with mutable state.
/// </remarks>
/// <example>
/// <code>
/// public class PoolableMessage : IPoolable
/// {
/// public string Content { get; set; } = string.Empty;
/// public Dictionary&lt;string, object&gt; Properties { get; } = new();
///
/// public void Reset()
/// {
/// Content = string.Empty;
/// Properties.Clear();
/// }
/// }
/// </code>
/// </example>
public interface IPoolable
{
	/// <summary>
	/// Resets the object to its initial, clean state suitable for reuse.
	/// </summary>
	/// <remarks>
	/// This method is called automatically by object pool implementations when objects are returned to the pool or before being rented out.
	/// The implementation should efficiently restore the object to the same state as a freshly constructed instance.
	/// <para> <strong> Implementation Requirements: </strong> </para>
	/// - Must be idempotent (safe to call multiple times)
	/// - Should complete quickly without blocking operations
	/// - Must handle partially initialized objects gracefully
	/// - Should not throw exceptions under normal circumstances.
	/// <para> <strong> Resource Management: </strong> </para>
	/// Care should be taken when resetting objects that hold resources. Consider whether resources should be disposed, released, or reused.
	/// For expensive-to-create resources, consider keeping them allocated but reset to a clean state.
	/// </remarks>
	void Reset();
}
