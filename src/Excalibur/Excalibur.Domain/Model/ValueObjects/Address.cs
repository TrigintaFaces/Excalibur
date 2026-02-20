// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Model.ValueObjects;

/// <summary>
/// Represents a physical address.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Address" /> class.
/// </remarks>
/// <param name="street"> The street address. </param>
/// <param name="city"> The city. </param>
/// <param name="state"> The state or province. </param>
/// <param name="country"> The country. </param>
/// <param name="postalCode"> The postal or ZIP code. </param>
public sealed class Address(string street, string city, string state, string country, string postalCode) : ValueObjectBase
{
	/// <summary>
	/// Gets the street address.
	/// </summary>
	/// <value>
	/// The street address.
	/// </value>
	public string Street { get; } = street ?? throw new ArgumentNullException(nameof(street));

	/// <summary>
	/// Gets the city.
	/// </summary>
	/// <value>
	/// The city.
	/// </value>
	public string City { get; } = city ?? throw new ArgumentNullException(nameof(city));

	/// <summary>
	/// Gets the state or province.
	/// </summary>
	/// <value>
	/// The state or province.
	/// </value>
	public string State { get; } = state ?? throw new ArgumentNullException(nameof(state));

	/// <summary>
	/// Gets the country.
	/// </summary>
	/// <value>
	/// The country.
	/// </value>
	public string Country { get; } = country ?? throw new ArgumentNullException(nameof(country));

	/// <summary>
	/// Gets the postal or ZIP code.
	/// </summary>
	/// <value>
	/// The postal or ZIP code.
	/// </value>
	public string PostalCode { get; } = postalCode ?? throw new ArgumentNullException(nameof(postalCode));

	/// <inheritdoc />
	public override IEnumerable<object?> GetEqualityComponents()
	{
		yield return Street;
		yield return City;
		yield return State;
		yield return Country;
		yield return PostalCode;
	}

	/// <summary>
	/// Returns a string representation of the address.
	/// </summary>
	public override string ToString() => $"{Street}, {City}, {State} {PostalCode}, {Country}";
}
