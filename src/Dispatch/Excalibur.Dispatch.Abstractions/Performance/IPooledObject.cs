// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines an object that can be pooled and reused.
/// </summary>
/// <remarks>
/// Objects implementing this interface can participate in object pooling by providing their own reset logic. This ensures objects are
/// properly cleaned before being returned to the pool for reuse.
/// </remarks>
public interface IPooledObject
{
	/// <summary>
	/// Gets a value indicating whether this object can be returned to the pool.
	/// </summary>
	/// <remarks>
	/// Some objects may become unsuitable for pooling due to their state or resource consumption. When false, the object should be
	/// discarded rather than returned to the pool.
	/// </remarks>
	bool CanBePooled { get; }

	/// <summary>
	/// Resets the object to a clean state for reuse.
	/// </summary>
	/// <remarks>
	/// This method is called before the object is returned to the pool. Implementations should clear all state, reset fields to defaults,
	/// and ensure the object is ready for the next use. This method should be idempotent and should not throw exceptions.
	/// </remarks>
	void Reset();
}
