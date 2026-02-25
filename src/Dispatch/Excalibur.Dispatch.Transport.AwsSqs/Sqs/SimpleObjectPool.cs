// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Simple object pool implementation.
/// </summary>
internal sealed class SimpleObjectPool<T>(Func<T> objectGenerator, Action<T> resetAction)
	where T : class
{
	private readonly ConcurrentBag<T> _objects = [];
	private readonly Func<T> _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
	private readonly Action<T> _resetAction = resetAction ?? throw new ArgumentNullException(nameof(resetAction));

	public T Rent() => _objects.TryTake(out var item) ? item : _objectGenerator();

	/// <summary>
	/// Alias for compatibility.
	/// </summary>
	public T Get() => Rent();

	public void Return(T item)
	{
		_resetAction(item);
		_objects.Add(item);
	}
}
