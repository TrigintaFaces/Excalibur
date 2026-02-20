// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Adapts an <see cref="IDictionary{TKey,TValue}" /> of <see cref="object" /> values to expose nullable values as required by <see cref="IMessageContext.Properties" />.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PropertyDictionary" /> class. </remarks>
/// <param name="items"> The underlying dictionary to adapt. </param>
public sealed class PropertyDictionary(IDictionary<string, object> items) : IDictionary<string, object?>
{
	/// <summary>
	/// Gets a collection containing the keys in the dictionary.
	/// </summary>
	/// <value> The current <see cref="Keys" /> value. </value>
	public ICollection<string> Keys => items.Keys;

	/// <summary>
	/// Gets a collection containing the values in the dictionary.
	/// </summary>
	public ICollection<object?> Values => [.. items.Values.Cast<object?>()];

	/// <summary>
	/// Gets the number of elements in the dictionary.
	/// </summary>
	/// <value> The current <see cref="Count" /> value. </value>
	public int Count => items.Count;

	/// <summary>
	/// Gets a value indicating whether the dictionary is read-only.
	/// </summary>
	/// <value> The current <see cref="IsReadOnly" /> value. </value>
	public bool IsReadOnly => false;

	/// <summary>
	/// Gets or sets the element with the specified key.
	/// </summary>
	/// <param name="key"> The key of the element to get or set. </param>
	/// <returns> The element with the specified key, or null if the key does not exist. </returns>
	public object? this[string key]
	{
		get => items.TryGetValue(key, out var value) ? value : null;
		set
		{
			if (value is null)
			{
				_ = items.Remove(key);
			}
			else
			{
				items[key] = value;
			}
		}
	}

	/// <summary>
	/// Adds an element with the specified key and value to the dictionary.
	/// When <paramref name="value"/> is <see langword="null"/>, the key is removed instead
	/// (consistent with ASP.NET Core route value semantics).
	/// </summary>
	/// <param name="key"> The key of the element to add. </param>
	/// <param name="value"> The value of the element to add, or <see langword="null"/> to remove the key. </param>
	public void Add(string key, object? value)
	{
		if (value is null)
		{
			_ = items.Remove(key);
		}
		else
		{
			items[key] = value;
		}
	}

	/// <summary>
	/// Adds the specified key-value pair to the dictionary.
	/// When the value is <see langword="null"/>, the key is removed instead
	/// (consistent with ASP.NET Core route value semantics).
	/// </summary>
	/// <param name="item"> The key-value pair to add. </param>
	public void Add(KeyValuePair<string, object?> item)
	{
		if (item.Value is null)
		{
			_ = items.Remove(item.Key);
		}
		else
		{
			items[item.Key] = item.Value;
		}
	}

	/// <summary>
	/// Removes all elements from the dictionary.
	/// </summary>
	public void Clear() => items.Clear();

	/// <summary>
	/// Determines whether the dictionary contains a specific key-value pair.
	/// </summary>
	/// <param name="item"> The key-value pair to locate. </param>
	/// <returns> true if the key-value pair is found; otherwise, false. </returns>
	public bool Contains(KeyValuePair<string, object?> item)
	{
		if (!items.TryGetValue(item.Key, out var value))
		{
			return false;
		}

		return Equals(value, item.Value);
	}

	/// <summary>
	/// Determines whether the dictionary contains the specified key.
	/// </summary>
	/// <param name="key"> The key to locate. </param>
	/// <returns> true if the dictionary contains an element with the specified key; otherwise, false. </returns>
	public bool ContainsKey(string key) => items.ContainsKey(key);

	/// <summary>
	/// Copies the elements of the dictionary to an array, starting at the specified array index.
	/// </summary>
	/// <param name="array"> The destination array. </param>
	/// <param name="arrayIndex"> The zero-based index in the array at which copying begins. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="array" /> is null. </exception>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="arrayIndex" /> is negative. </exception>
	/// <exception cref="ArgumentException"> Thrown when the destination array is not large enough. </exception>
	public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
	{
		ArgumentNullException.ThrowIfNull(array);
		ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

		if (array.Length - arrayIndex < items.Count)
		{
			throw new ArgumentException(ErrorMessages.DestinationArrayIsNotLargeEnough, nameof(array));
		}

		foreach (var kvp in items)
		{
			array[arrayIndex++] = new KeyValuePair<string, object?>(kvp.Key, kvp.Value);
		}
	}

	/// <summary>
	/// Returns an enumerator that iterates through the dictionary.
	/// </summary>
	/// <returns> An enumerator for the dictionary. </returns>
	public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
		items.Select(static kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)).GetEnumerator();

	/// <summary>
	/// Removes the element with the specified key from the dictionary.
	/// </summary>
	/// <param name="key"> The key of the element to remove. </param>
	/// <returns> true if the element was successfully removed; otherwise, false. </returns>
	public bool Remove(string key) => items.Remove(key);

	/// <summary>
	/// Removes the specified key-value pair from the dictionary.
	/// </summary>
	/// <param name="item"> The key-value pair to remove. </param>
	/// <returns> true if the key-value pair was successfully removed; otherwise, false. </returns>
	public bool Remove(KeyValuePair<string, object?> item)
	{
		if (!Contains(item))
		{
			return false;
		}

		return items.Remove(item.Key);
	}

	/// <summary>
	/// Gets the value associated with the specified key.
	/// </summary>
	/// <param name="key"> The key of the value to get. </param>
	/// <param name="value"> When this method returns, contains the value associated with the specified key, if found; otherwise, null. </param>
	/// <returns> true if the dictionary contains an element with the specified key; otherwise, false. </returns>
	public bool TryGetValue(string key, [NotNullWhen(true)] out object? value)
	{
		if (items.TryGetValue(key, out var rawValue))
		{
			value = rawValue;
			return true;
		}

		value = null;
		return false;
	}

	/// <summary>
	/// Returns an enumerator that iterates through the dictionary.
	/// </summary>
	/// <returns> An enumerator for the dictionary. </returns>
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
