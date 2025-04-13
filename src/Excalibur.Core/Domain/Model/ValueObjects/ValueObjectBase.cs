namespace Excalibur.Core.Domain.Model.ValueObjects;

/// <summary>
///     Serves as a base class for value objects, providing common functionality for equality checks and hashing.
/// </summary>
public abstract class ValueObjectBase : IValueObject
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ValueObjectBase" /> class.
	/// </summary>
	protected ValueObjectBase()
	{
	}

	/// <summary>
	///     Determines whether the specified object is equal to the current value object.
	/// </summary>
	/// <param name="obj"> The object to compare with the current value object. </param>
	/// <returns> <c> true </c> if the specified object is equal to the current value object; otherwise, <c> false </c>. </returns>
	public override bool Equals(object obj) => EqualsInternal(obj as IValueObject);

	/// <summary>
	///     Serves as the default hash function.
	/// </summary>
	/// <returns> A hash code for the current value object. </returns>
	public override int GetHashCode() => HashCode.Combine(GetType(), GetHashCodeInternal());

	/// <summary>
	///     Determines whether the specified value object is equal to the current value object.
	/// </summary>
	/// <param name="other"> The value object to compare with the current value object. </param>
	/// <returns> <c> true </c> if the specified value object is equal to the current value object; otherwise, <c> false </c>. </returns>
	/// <remarks> Derived classes must implement this method to define the equality logic specific to the value object. </remarks>
	protected abstract bool EqualsInternal(IValueObject? other);

	/// <summary>
	///     Computes the hash code for the current value object.
	/// </summary>
	/// <returns> A hash code for the current value object. </returns>
	/// <remarks> Derived classes must implement this method to define how the hash code is calculated for the value object. </remarks>
	protected abstract int GetHashCodeInternal();
}
