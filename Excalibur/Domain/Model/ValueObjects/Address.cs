namespace Excalibur.Domain.Model.ValueObjects;

/// <summary>
///     Represents a postal address as a value object.
/// </summary>
public class Address : ValueObjectBase
{
	/// <summary>
	///     Initializes a new instance of the <see cref="Address" /> class.
	/// </summary>
	/// <param name="address1"> The primary address line. </param>
	/// <param name="address2"> The secondary address line (optional). </param>
	/// <param name="city"> The city of the address. </param>
	/// <param name="state"> The state or province of the address. </param>
	/// <param name="zip"> The postal or ZIP code of the address. </param>
	[Newtonsoft.Json.JsonConstructor]
	[System.Text.Json.Serialization.JsonConstructor]
	public Address(string address1, string? address2, string city, string state, string zip)
	{
		Address1 = address1;
		Address2 = address2;
		City = city;
		State = state;
		Zip = zip;
	}

	/// <summary>
	///     Gets or sets the primary address line.
	/// </summary>
	public string Address1 { get; set; }

	/// <summary>
	///     Gets or sets the secondary address line (optional).
	/// </summary>
	public string? Address2 { get; set; }

	/// <summary>
	///     Gets or sets the city of the address.
	/// </summary>
	public string City { get; set; }

	/// <summary>
	///     Gets or sets the state or province of the address.
	/// </summary>
	public string State { get; set; }

	/// <summary>
	///     Gets or sets the postal or ZIP code of the address.
	/// </summary>
	public string Zip { get; set; }

	/// <inheritdoc />
	protected override bool EqualsInternal(IValueObject? other)
	{
		if (other is not Address otherAddress)
		{
			return false;
		}

		return Address1 == otherAddress.Address1 &&
			   Address2 == otherAddress.Address2 &&
			   City == otherAddress.City &&
			   State == otherAddress.State &&
			   Zip == otherAddress.Zip;
	}

	/// <inheritdoc />
	protected override int GetHashCodeInternal() => HashCode.Combine(Address1, Address2, City, State, Zip);
}
