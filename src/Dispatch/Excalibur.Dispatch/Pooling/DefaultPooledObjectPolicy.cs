// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.ObjectPool;

namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// Default pooled object policy that uses IPoolable interface or factory functions.
/// </summary>
/// <typeparam name="T"> The type of objects to pool. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="DefaultPooledObjectPolicy{T}" /> class. </remarks>
/// <param name="factory"> Optional factory function to create instances. </param>
/// <param name="resetAction"> Optional action to reset instances. </param>
public sealed class DefaultPooledObjectPolicy<T>(Func<T>? factory = null, Action<T>? resetAction = null)
	: IPooledObjectPolicy<T>
	where T : class, new()
{
	/// <inheritdoc />
	public T Create() => factory?.Invoke() ?? new T();

	/// <inheritdoc />
	public bool Return(T obj)
	{
		if (obj is IPoolable poolable)
		{
			try
			{
				poolable.Reset();
				return true;
			}
			catch
			{
				// If reset fails, don't return to pool
				return false;
			}
		}

		if (resetAction != null)
		{
			try
			{
				resetAction(obj);
				return true;
			}
			catch
			{
				// If reset fails, don't return to pool
				return false;
			}
		}

		// If no reset mechanism is provided, assume the object is stateless
		return true;
	}
}
