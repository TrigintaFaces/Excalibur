namespace Excalibur.Domain;

/// <summary>
///     Defines the contract for an activity context that provides key-value storage for managing contextual data.
/// </summary>
public interface IActivityContext
{
	/// <summary>
	///     Retrieves a value from the context by its key, or returns a default value if the key is not found.
	/// </summary>
	/// <typeparam name="T"> The type of the value to retrieve. </typeparam>
	/// <param name="key"> The key associated with the value to retrieve. </param>
	/// <param name="defaultValue"> The default value to return if the key does not exist or the value is <c> null </c>. </param>
	/// <returns> The value associated with the specified key, or <paramref name="defaultValue" /> if the key does not exist. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="key" /> is <c> null </c>, empty, or whitespace. </exception>
	public T Get<T>(string key, T defaultValue);

	/// <summary>
	///     Sets a value in the context for the specified key.
	/// </summary>
	/// <typeparam name="T"> The type of the value to set. </typeparam>
	/// <param name="key"> The key to associate with the value. </param>
	/// <param name="value"> The value to store in the context. </param>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="key" /> is <c> null </c>, empty, or whitespace. </exception>
	public void Set<T>(string key, T value);

	/// <summary>
	///     Checks if the context contains a value for the specified key.
	/// </summary>
	/// <param name="key"> The key to check for existence in the context. </param>
	/// <returns> <c> true </c> if the key exists in the context; otherwise, <c> false </c>. </returns>
	public bool ContainsKey(string key);

	/// <summary>
	///     Removes a value from the context for the specified key.
	/// </summary>
	/// <param name="key"> The key associated with the value to remove. </param>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="key" /> is <c> null </c>, empty, or whitespace. </exception>
	public void Remove(string key);
}
