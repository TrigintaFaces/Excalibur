// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.ObjectPool;

namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// Pooled object policy for message objects.
/// </summary>
/// <typeparam name="T"> The type of message. </typeparam>
internal sealed class
	MessagePooledObjectPolicy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> : IPooledObjectPolicy<T>
	where T : class, IDispatchMessage, new()
{
	/// <inheritdoc />
	public T Create() => new();

	/// <inheritdoc />
	public bool Return(T obj)
	{
		// Reset common message properties
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

		// For messages that don't implement IPoolable, perform basic cleanup
		try
		{
			ResetMessageProperties(obj);
			return true;
		}
		catch
		{
			// If reset fails, don't return to pool
			return false;
		}
	}

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2072:'type' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicParameterlessConstructor' in call to target method",
		Justification = "Message types are preserved through source generation and DI registration")]
	private static void ResetMessageProperties(T message)
	{
		// Use reflection sparingly here - in a real implementation, we'd use source generators to create reset methods
		var type = typeof(T);
		var properties = type.GetProperties(
			BindingFlags.Public |
			BindingFlags.Instance);

		foreach (var property in properties)
		{
			if (!property.CanWrite)
			{
				continue;
			}

			var propertyType = property.PropertyType;

			// Reset to default values based on type
			if (propertyType.IsValueType)
			{
				property.SetValue(message, RuntimeHelpers.GetUninitializedObject(propertyType));
			}
			else if (propertyType == typeof(string))
			{
				property.SetValue(message, value: null);
			}
			else if (propertyType.IsClass)
			{
				property.SetValue(message, value: null);
			}
		}
	}
}
