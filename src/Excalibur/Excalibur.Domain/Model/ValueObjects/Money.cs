// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;

namespace Excalibur.Domain.Model.ValueObjects;

/// <summary>
/// Represents a monetary value with culture-specific formatting.
/// </summary>
public sealed class Money : ValueObjectBase
{
	private const string DefaultCultureName = "en-US";
	private static readonly CompositeFormat InvalidFormatFormat =
			CompositeFormat.Parse(Resources.Money_InvalidFormat);

	/// <summary>
	/// Initializes a new instance of the <see cref="Money" /> class.
	/// </summary>
	/// <param name="amount"> The monetary amount. </param>
	/// <param name="cultureName"> The culture name for formatting (e.g., "en-US"). </param>
	public Money(decimal amount, string? cultureName = DefaultCultureName)
	{
		Amount = amount;
		CultureName = cultureName ?? DefaultCultureName;
		var culture = CultureInfo.GetCultureInfo(CultureName);
		CurrencySymbol = culture.NumberFormat.CurrencySymbol;
		ISOCurrencyCode = new RegionInfo(culture.Name).ISOCurrencySymbol;
		Denomination = null;
		UnitCount = 0;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Money" /> class with a specific denomination.
	/// </summary>
	/// <param name="amount"> The monetary amount. </param>
	/// <param name="cultureName"> The culture name for formatting (e.g., "en-US"). </param>
	/// <param name="denomination"> The denomination value (e.g., 20 for twenty-dollar bills). </param>
	public Money(decimal amount, string? cultureName, decimal denomination)
	{
		Amount = amount;
		CultureName = cultureName ?? DefaultCultureName;
		var culture = CultureInfo.GetCultureInfo(CultureName);
		CurrencySymbol = culture.NumberFormat.CurrencySymbol;
		ISOCurrencyCode = new RegionInfo(culture.Name).ISOCurrencySymbol;
		Denomination = denomination;
		UnitCount = denomination != 0 ? (int)(amount / denomination) : 0;
	}

	/// <summary>
	/// Gets the monetary amount.
	/// </summary>
	/// <value>
	/// The monetary amount.
	/// </value>
	public decimal Amount { get; }

	/// <summary>
	/// Gets the culture name used for formatting.
	/// </summary>
	/// <value>
	/// The culture name used for formatting.
	/// </value>
	public string CultureName { get; }

	/// <summary>
	/// Gets the currency symbol for the culture.
	/// </summary>
	/// <value>
	/// The currency symbol for the culture.
	/// </value>
	public string CurrencySymbol { get; }

	/// <summary>
	/// Gets the ISO currency code for the culture.
	/// </summary>
	/// <value>
	/// The ISO currency code for the culture.
	/// </value>
	public string ISOCurrencyCode { get; }

	/// <summary>
	/// Gets the denomination value (e.g., 20 for twenty-dollar bills), if specified.
	/// </summary>
	/// <value>
	/// The denomination value (e.g., 20 for twenty-dollar bills), if specified.
	/// </value>
	public decimal? Denomination { get; }

	/// <summary>
	/// Gets the number of units of the denomination (e.g., number of bills).
	/// </summary>
	/// <value>
	/// The number of units of the denomination (e.g., number of bills).
	/// </value>
	public int UnitCount { get; }

	/// <summary>
	/// Gets a value indicating whether this represents only bills (denomination &gt;= 1).
	/// </summary>
	/// <value>
	/// A value indicating whether this represents only bills (denomination &gt;= 1).
	/// </value>
	public bool IsBillsOnly => Denomination is >= 1m;

	/// <summary> Gets a value indicating whether this represents only coins (denomination &lt; 1). </summary>
	/// <value>
	///  A value indicating whether this represents only coins (denomination &lt; 1).
	/// </value>
	public bool IsCoinsOnly => Denomination is < 1m;

	/// <summary>
	/// Creates a new Money instance from the specified amount and culture.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="cultureName">The culture name for formatting (e.g., "en-US").</param>
	public static Money From(decimal amount, string cultureName = DefaultCultureName) => new(amount, cultureName);

	/// <summary>
	/// Creates a new Money instance from a double value.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="cultureName">The culture name for formatting (e.g., "en-US").</param>
	public static Money From(double amount, string cultureName = DefaultCultureName) => new((decimal)amount, cultureName);

	/// <summary>
	/// Creates a new Money instance from a float value.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="cultureName">The culture name for formatting (e.g., "en-US").</param>
	public static Money From(float amount, string cultureName = DefaultCultureName) => new((decimal)amount, cultureName);

	/// <summary>
	/// Creates a new Money instance from an integer value.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="cultureName">The culture name for formatting (e.g., "en-US").</param>
	public static Money From(int amount, string cultureName = DefaultCultureName) => new(amount, cultureName);

	/// <summary>
	/// Creates a new Money instance from a long value.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="cultureName">The culture name for formatting (e.g., "en-US").</param>
	public static Money From(long amount, string cultureName = DefaultCultureName) => new(amount, cultureName);

	/// <summary>
	/// Creates a new Money instance in US Dollars (USD).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money USD(decimal amount) => new(amount, "en-US");

	/// <summary>
	/// Creates a new Money instance in Euros (EUR).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money EUR(decimal amount) => new(amount, "fr-FR");

	/// <summary>
	/// Creates a new Money instance in British Pounds (GBP).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money GBP(decimal amount) => new(amount, "en-GB");

	/// <summary>
	/// Creates a new Money instance in Japanese Yen (JPY).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money JPY(decimal amount) => new(amount, "ja-JP");

	/// <summary>
	/// Creates a new Money instance in Swiss Francs (CHF).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money CHF(decimal amount) => new(amount, "de-CH");

	/// <summary>
	/// Creates a new Money instance in Canadian Dollars (CAD).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money CAD(decimal amount) => new(amount, "en-CA");

	/// <summary>
	/// Creates a new Money instance in Australian Dollars (AUD).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money AUD(decimal amount) => new(amount, "en-AU");

	/// <summary>
	/// Creates a new Money instance from a string value.
	/// </summary>
	/// <param name="amount">The monetary amount as a string.</param>
	/// <param name="cultureName">The culture name for formatting (e.g., "en-US").</param>
	/// <exception cref="FormatException"></exception>
	public static Money From(string amount, string cultureName = DefaultCultureName)
	{
		var culture = CultureInfo.GetCultureInfo(cultureName);
		if (!decimal.TryParse(amount, NumberStyles.Currency, culture, out var parsedAmount))
		{
			throw new FormatException(
					string.Format(
							CultureInfo.CurrentCulture,
							InvalidFormatFormat,
							amount,
							cultureName));
		}

		return new Money(parsedAmount, cultureName);
	}

	/// <summary>
	/// Adds two monetary values.
	/// </summary>
	/// <param name="left">The first monetary value.</param>
	/// <param name="right">The second monetary value.</param>
	public static Money operator +(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return new Money(left.Amount + right.Amount, left.CultureName);
	}

	/// <summary>
	/// Subtracts one monetary value from another.
	/// </summary>
	/// <param name="left">The monetary value to subtract from.</param>
	/// <param name="right">The monetary value to subtract.</param>
	public static Money operator -(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return new Money(left.Amount - right.Amount, left.CultureName);
	}

	/// <summary>
	/// Multiplies a monetary value by a scalar.
	/// </summary>
	/// <param name="money">The monetary value.</param>
	/// <param name="multiplier">The multiplier.</param>
	public static Money operator *(Money money, decimal multiplier)
	{
		ArgumentNullException.ThrowIfNull(money);
		return new(money.Amount * multiplier, money.CultureName);
	}

	/// <summary>
	/// Multiplies a monetary value by a scalar.
	/// </summary>
	/// <param name="multiplier">The multiplier.</param>
	/// <param name="money">The monetary value.</param>
	public static Money operator *(decimal multiplier, Money money) => money * multiplier;

	/// <summary>
	/// Divides a monetary value by a scalar.
	/// </summary>
	/// <param name="money">The monetary value.</param>
	/// <param name="divisor">The divisor.</param>
	public static Money operator /(Money money, decimal divisor)
	{
		ArgumentNullException.ThrowIfNull(money);

		if (divisor == 0)
		{
			throw new DivideByZeroException(Resources.Money_DivideByZero);
		}

		return new Money(money.Amount / divisor, money.CultureName);
	}

	/// <summary>
	/// Determines whether one monetary value is greater than another.
	/// </summary>
	/// <param name="left">The first monetary value.</param>
	/// <param name="right">The second monetary value.</param>
	public static bool operator >(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return left.Amount > right.Amount;
	}

	/// <summary>
	/// Determines whether one monetary value is less than another.
	/// </summary>
	/// <param name="left">The first monetary value.</param>
	/// <param name="right">The second monetary value.</param>
	public static bool operator <(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return left.Amount < right.Amount;
	}

	/// <summary>
	/// Determines whether one monetary value is greater than or equal to another.
	/// </summary>
	/// <param name="left">The first monetary value.</param>
	/// <param name="right">The second monetary value.</param>
	public static bool operator >=(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return left.Amount >= right.Amount;
	}

	/// <summary>
	/// Determines whether one monetary value is less than or equal to another.
	/// </summary>
	/// <param name="left">The first monetary value.</param>
	/// <param name="right">The second monetary value.</param>
	public static bool operator <=(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return left.Amount <= right.Amount;
	}

	/// <summary>
	/// Adds two monetary values (friendly alternative to + operator).
	/// </summary>
	/// <param name="left">The first monetary value.</param>
	/// <param name="right">The second monetary value.</param>
	/// <returns>The sum of the two monetary values.</returns>
	public static Money Add(Money left, Money right) => left + right;

	/// <summary>
	/// Subtracts one monetary value from another (friendly alternative to - operator).
	/// </summary>
	/// <param name="left">The monetary value to subtract from.</param>
	/// <param name="right">The monetary value to subtract.</param>
	/// <returns>The difference between the two monetary values.</returns>
	public static Money Subtract(Money left, Money right) => left - right;

	/// <summary>
	/// Multiplies a monetary value by a scalar (friendly alternative to * operator).
	/// </summary>
	/// <param name="money">The monetary value.</param>
	/// <param name="multiplier">The multiplier.</param>
	/// <returns>The product of the monetary value and the multiplier.</returns>
	public static Money Multiply(Money money, decimal multiplier) => money * multiplier;

	/// <summary>
	/// Divides a monetary value by a scalar (friendly alternative to / operator).
	/// </summary>
	/// <param name="money">The monetary value.</param>
	/// <param name="divisor">The divisor.</param>
	/// <returns>The quotient of the monetary value and the divisor.</returns>
	public static Money Divide(Money money, decimal divisor) => money / divisor;

	/// <summary>
	/// Compares this monetary value to another (friendly alternative to comparison operators).
	/// </summary>
	/// <param name="other">The monetary value to compare to.</param>
	/// <returns>
	/// A value less than zero if this instance is less than <paramref name="other"/>,
	/// zero if equal, or greater than zero if greater than <paramref name="other"/>.
	/// </returns>
	public int CompareTo(Money? other)
	{
		if (other is null)
		{
			return 1;
		}

		EnsureSameCurrency(this, other);
		return Amount.CompareTo(other.Amount);
	}

	/// <inheritdoc />
	public override IEnumerable<object?> GetEqualityComponents()
	{
		yield return Amount;
		yield return ISOCurrencyCode;
		yield return Denomination;
	}

	/// <summary>
	/// Returns a string representation of the monetary value.
	/// </summary>
	public override string ToString()
	{
		var culture = CultureInfo.GetCultureInfo(CultureName);
		return Amount.ToString("C", culture);
	}

	/// <summary>
	/// Returns a string representation of the monetary value with the specified format.
	/// </summary>
	/// <param name="format">The format string.</param>
	public string ToString(string format)
	{
		var culture = CultureInfo.GetCultureInfo(CultureName);
		return Amount.ToString(format, culture);
	}

	private static void EnsureSameCurrency(Money left, Money right)
	{
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		if (!string.Equals(left.ISOCurrencyCode, right.ISOCurrencyCode, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Cannot perform operations on money with different currencies: '{left.ISOCurrencyCode}' and '{right.ISOCurrencyCode}'.");
		}
	}
}
