// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.ObjectPool;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// A <see cref="PooledObjectPolicy{T}" /> that creates and resets pooled instances through caller-supplied delegates.
/// </summary>
/// <typeparam name="T"> The pooled object type. </typeparam>
/// <param name="factory"> Creates a new instance when the pool is empty. </param>
/// <param name="reset"> Resets an instance before it is returned to the pool. </param>
internal sealed class DelegatePooledObjectPolicy<T>(Func<T> factory, Action<T> reset) : PooledObjectPolicy<T>
	where T : class
{
	private readonly Func<T> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
	private readonly Action<T> _reset = reset ?? throw new ArgumentNullException(nameof(reset));

	/// <inheritdoc />
	public override T Create() => _factory();

	/// <inheritdoc />
	public override bool Return(T obj)
	{
		if (obj is null)
		{
			return false;
		}

		_reset(obj);
		return true;
	}
}
