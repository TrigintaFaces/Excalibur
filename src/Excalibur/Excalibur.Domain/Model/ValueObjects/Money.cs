// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace Excalibur.Domain.Model.ValueObjects;

/// <summary>
/// Represents a monetary value with an ISO 4217 currency code.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Money"/> separates currency identity (what the money <em>is</em>) from
/// display formatting (how it <em>looks</em>). The currency is identified by its
/// <see href="https://en.wikipedia.org/wiki/ISO_4217">ISO 4217</see> code (e.g., "USD",
/// "EUR", "GBP"), not by a culture or locale.
/// </para>
/// <para>
/// Formatting is a display concern handled by <see cref="ToString(CultureInfo)"/>:
/// <code>
/// var price = Money.EUR(1234.56m);
/// price.ToString(new CultureInfo("de-DE")); // "1.234,56 €"
/// price.ToString(new CultureInfo("fr-FR")); // "1 234,56 €"
/// </code>
/// </para>
/// <para>
/// This follows the same separation used by <c>java.util.Currency</c> + <c>NumberFormat</c>,
/// NodaMoney, and other financial libraries.
/// </para>
/// </remarks>
public sealed class Money : ValueObjectBase
{
	/// <summary>
	/// The default currency code when none is specified.
	/// </summary>
	private const string DefaultCurrencyCode = "USD";

	private static readonly CompositeFormat InvalidFormatFormat =
			CompositeFormat.Parse(Resources.Money_InvalidFormat);

	/// <summary>
	/// JSON deserialization constructor. Handles all property combinations including
	/// nullable <see cref="Denomination"/> and computed <see cref="UnitCount"/>.
	/// </summary>
	[JsonConstructor]
	private Money(decimal amount, string currencyCode, decimal? denomination, int unitCount)
	{
		Amount = amount;
		CurrencyCode = (currencyCode ?? DefaultCurrencyCode).ToUpperInvariant();
		Denomination = denomination;
		UnitCount = denomination is not null and not 0
			? (int)(amount / denomination.Value)
			: unitCount;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Money" /> class.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="currencyCode">
	/// The ISO 4217 currency code (e.g., "USD", "EUR", "GBP").
	/// Defaults to "USD".
	/// </param>
	public Money(decimal amount, string currencyCode = DefaultCurrencyCode)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
		Amount = amount;
		CurrencyCode = currencyCode.ToUpperInvariant();
		Denomination = null;
		UnitCount = 0;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Money" /> class with a specific denomination.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="currencyCode">
	/// The ISO 4217 currency code (e.g., "USD", "EUR", "GBP").
	/// </param>
	/// <param name="denomination">The denomination value (e.g., 20 for twenty-dollar bills).</param>
	public Money(decimal amount, string currencyCode, decimal denomination)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
		Amount = amount;
		CurrencyCode = currencyCode.ToUpperInvariant();
		Denomination = denomination;
		UnitCount = denomination != 0 ? (int)(amount / denomination) : 0;
	}

	/// <summary>
	/// Gets the monetary amount.
	/// </summary>
	/// <value>The monetary amount.</value>
	public decimal Amount { get; }

	/// <summary>
	/// Gets the ISO 4217 currency code (e.g., "USD", "EUR", "GBP").
	/// </summary>
	/// <value>The ISO 4217 currency code.</value>
	public string CurrencyCode { get; }

	/// <summary>
	/// Gets the denomination value (e.g., 20 for twenty-dollar bills), if specified.
	/// </summary>
	/// <value>The denomination value, or <c>null</c> if no denomination was specified.</value>
	public decimal? Denomination { get; }

	/// <summary>
	/// Gets the number of units of the denomination (e.g., number of bills).
	/// </summary>
	/// <value>The number of units of the denomination.</value>
	public int UnitCount { get; }

	/// <summary>
	/// Gets a value indicating whether this represents only bills (denomination &gt;= 1).
	/// </summary>
	/// <value><see langword="true"/> if the denomination represents bills; otherwise, <see langword="false"/>.</value>
	public bool IsBillsOnly => Denomination is >= 1m;

	/// <summary>
	/// Gets a value indicating whether this represents only coins (denomination &lt; 1).
	/// </summary>
	/// <value><see langword="true"/> if the denomination represents coins; otherwise, <see langword="false"/>.</value>
	public bool IsCoinsOnly => Denomination is < 1m;

	// ========================================================================
	// Conversion operators
	// ========================================================================

	/// <summary>
	/// Implicitly converts a <see cref="Money"/> instance to its <see cref="Amount"/> as a <see cref="decimal"/>.
	/// </summary>
	/// <param name="money">The monetary value to convert.</param>
	/// <returns>The <see cref="Amount"/> of the monetary value.</returns>
	/// <remarks>
	/// <para>
	/// This enables natural assignment of <see cref="Money"/> to <see cref="decimal"/> variables:
	/// <code>
	/// Money price = Money.USD(29.99m);
	/// decimal amount = price; // amount == 29.99m
	/// </code>
	/// </para>
	/// <para>
	/// Note that the conversion discards the currency information. If you need to
	/// preserve the currency, use the <see cref="Amount"/> property directly
	/// alongside <see cref="CurrencyCode"/>.
	/// </para>
	/// </remarks>
	public static implicit operator decimal(Money money)
	{
		ArgumentNullException.ThrowIfNull(money);
		return money.Amount;
	}

	/// <summary>
	/// Explicitly converts a <see cref="Money"/> instance to a <see cref="double"/>.
	/// </summary>
	/// <param name="money">The monetary value to convert.</param>
	/// <returns>The <see cref="Amount"/> of the monetary value as a <see cref="double"/>.</returns>
	/// <remarks>
	/// <para>
	/// This conversion is explicit because <see cref="decimal"/> to <see cref="double"/>
	/// introduces floating-point representation errors that are unacceptable in
	/// financial calculations (e.g., <c>0.1m + 0.2m</c> is exactly <c>0.3m</c> in
	/// <see cref="decimal"/> but <c>0.30000000000000004</c> in <see cref="double"/>).
	/// </para>
	/// <para>
	/// The explicit cast ensures developers consciously opt in to precision loss:
	/// <code>
	/// Money price = Money.USD(29.99m);
	/// double d = (double)price; // OK — developer acknowledges precision loss
	/// </code>
	/// </para>
	/// </remarks>
	public static explicit operator double(Money money)
	{
		ArgumentNullException.ThrowIfNull(money);
		return (double)money.Amount;
	}

	/// <summary>
	/// Converts this <see cref="Money"/> instance to a <see cref="double"/>.
	/// </summary>
	/// <returns>The <see cref="Amount"/> of this monetary value as a <see cref="double"/>.</returns>
	/// <remarks>
	/// Named method alternative to the explicit conversion operator (CA2225).
	/// </remarks>
	public double ToDouble() => (double)Amount;

	/// <summary>
	/// Converts this <see cref="Money"/> instance to its <see cref="Amount"/> as a <see cref="decimal"/>.
	/// </summary>
	/// <returns>The <see cref="Amount"/> of this monetary value.</returns>
	/// <remarks>
	/// Named method alternative to the implicit conversion operator (CA2225).
	/// </remarks>
	public decimal ToDecimal() => Amount;

	// ========================================================================
	// Factory methods
	// ========================================================================

	/// <summary>
	/// Creates a new <see cref="Money"/> instance from the specified amount and currency code.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="currencyCode">The ISO 4217 currency code. Defaults to "USD".</param>
	public static Money From(decimal amount, string currencyCode = DefaultCurrencyCode) => new(amount, currencyCode);

	/// <summary>
	/// Creates a new <see cref="Money"/> instance from a <see cref="double"/> value.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="currencyCode">The ISO 4217 currency code. Defaults to "USD".</param>
	public static Money From(double amount, string currencyCode = DefaultCurrencyCode) => new((decimal)amount, currencyCode);

	/// <summary>
	/// Creates a new <see cref="Money"/> instance from a <see cref="float"/> value.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="currencyCode">The ISO 4217 currency code. Defaults to "USD".</param>
	public static Money From(float amount, string currencyCode = DefaultCurrencyCode) => new((decimal)amount, currencyCode);

	/// <summary>
	/// Creates a new <see cref="Money"/> instance from an <see cref="int"/> value.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="currencyCode">The ISO 4217 currency code. Defaults to "USD".</param>
	public static Money From(int amount, string currencyCode = DefaultCurrencyCode) => new(amount, currencyCode);

	/// <summary>
	/// Creates a new <see cref="Money"/> instance from a <see cref="long"/> value.
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	/// <param name="currencyCode">The ISO 4217 currency code. Defaults to "USD".</param>
	public static Money From(long amount, string currencyCode = DefaultCurrencyCode) => new(amount, currencyCode);

	/// <summary>
	/// Creates a new <see cref="Money"/> instance in US Dollars (USD).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money USD(decimal amount) => new(amount, "USD");

	/// <summary>
	/// Creates a new <see cref="Money"/> instance in Euros (EUR).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money EUR(decimal amount) => new(amount, "EUR");

	/// <summary>
	/// Creates a new <see cref="Money"/> instance in British Pounds (GBP).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money GBP(decimal amount) => new(amount, "GBP");

	/// <summary>
	/// Creates a new <see cref="Money"/> instance in Japanese Yen (JPY).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money JPY(decimal amount) => new(amount, "JPY");

	/// <summary>
	/// Creates a new <see cref="Money"/> instance in Swiss Francs (CHF).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money CHF(decimal amount) => new(amount, "CHF");

	/// <summary>
	/// Creates a new <see cref="Money"/> instance in Canadian Dollars (CAD).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money CAD(decimal amount) => new(amount, "CAD");

	/// <summary>
	/// Creates a new <see cref="Money"/> instance in Australian Dollars (AUD).
	/// </summary>
	/// <param name="amount">The monetary amount.</param>
	public static Money AUD(decimal amount) => new(amount, "AUD");

	/// <summary>
	/// Parses a currency string using the specified culture for number formatting.
	/// </summary>
	/// <param name="amount">The monetary amount as a string (e.g., "$1,234.56" or "1.234,56").</param>
	/// <param name="currencyCode">The ISO 4217 currency code. Defaults to "USD".</param>
	/// <param name="culture">
	/// The culture to use for parsing the number format. Defaults to <see cref="CultureInfo.InvariantCulture"/>.
	/// </param>
	/// <exception cref="FormatException">Thrown when the string cannot be parsed as a currency value.</exception>
	public static Money Parse(string amount, string currencyCode = DefaultCurrencyCode, CultureInfo? culture = null)
	{
		culture ??= CultureInfo.InvariantCulture;

		if (!decimal.TryParse(amount, NumberStyles.Currency, culture, out var parsedAmount))
		{
			throw new FormatException(
					string.Format(
							CultureInfo.CurrentCulture,
							InvalidFormatFormat,
							amount,
							culture.Name));
		}

		return new Money(parsedAmount, currencyCode);
	}

	/// <summary>
	/// Creates a new <see cref="Money"/> instance from a string, using the specified culture for parsing.
	/// </summary>
	/// <param name="amount">The monetary amount as a string.</param>
	/// <param name="cultureName">
	/// The culture name for parsing the number format (e.g., "en-US", "de-DE").
	/// The currency code is inferred from the culture's region for backward compatibility.
	/// </param>
	/// <exception cref="FormatException">Thrown when the string cannot be parsed as a currency value.</exception>
	/// <remarks>
	/// This overload exists for backward compatibility. Prefer <see cref="Parse(string, string, CultureInfo?)"/>
	/// which explicitly separates currency code from parsing culture.
	/// </remarks>
	public static Money From(string amount, string cultureName = "en-US")
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

		// Infer currency from culture region for backward compatibility
		var currencyCode = new RegionInfo(culture.Name).ISOCurrencySymbol;
		return new Money(parsedAmount, currencyCode);
	}

	// ========================================================================
	// Arithmetic operators
	// ========================================================================

	/// <summary>
	/// Adds two monetary values.
	/// </summary>
	public static Money operator +(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return new Money(left.Amount + right.Amount, left.CurrencyCode);
	}

	/// <summary>
	/// Subtracts one monetary value from another.
	/// </summary>
	public static Money operator -(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return new Money(left.Amount - right.Amount, left.CurrencyCode);
	}

	/// <summary>
	/// Multiplies a monetary value by a scalar.
	/// </summary>
	public static Money operator *(Money money, decimal multiplier)
	{
		ArgumentNullException.ThrowIfNull(money);
		return new(money.Amount * multiplier, money.CurrencyCode);
	}

	/// <summary>
	/// Multiplies a monetary value by a scalar.
	/// </summary>
	public static Money operator *(decimal multiplier, Money money) => money * multiplier;

	/// <summary>
	/// Divides a monetary value by a scalar.
	/// </summary>
	public static Money operator /(Money money, decimal divisor)
	{
		ArgumentNullException.ThrowIfNull(money);

		if (divisor == 0)
		{
			throw new DivideByZeroException(Resources.Money_DivideByZero);
		}

		return new Money(money.Amount / divisor, money.CurrencyCode);
	}

	// ========================================================================
	// Comparison operators
	// ========================================================================

	/// <summary>
	/// Determines whether one monetary value is greater than another.
	/// </summary>
	public static bool operator >(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return left.Amount > right.Amount;
	}

	/// <summary>
	/// Determines whether one monetary value is less than another.
	/// </summary>
	public static bool operator <(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return left.Amount < right.Amount;
	}

	/// <summary>
	/// Determines whether one monetary value is greater than or equal to another.
	/// </summary>
	public static bool operator >=(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return left.Amount >= right.Amount;
	}

	/// <summary>
	/// Determines whether one monetary value is less than or equal to another.
	/// </summary>
	public static bool operator <=(Money left, Money right)
	{
		EnsureSameCurrency(left, right);
		return left.Amount <= right.Amount;
	}

	// ========================================================================
	// Static method alternatives (CA2225 / non-operator languages)
	// ========================================================================

	/// <summary>
	/// Adds two monetary values.
	/// </summary>
	public static Money Add(Money left, Money right) => left + right;

	/// <summary>
	/// Subtracts one monetary value from another.
	/// </summary>
	public static Money Subtract(Money left, Money right) => left - right;

	/// <summary>
	/// Multiplies a monetary value by a scalar.
	/// </summary>
	public static Money Multiply(Money money, decimal multiplier) => money * multiplier;

	/// <summary>
	/// Divides a monetary value by a scalar.
	/// </summary>
	public static Money Divide(Money money, decimal divisor) => money / divisor;

	/// <summary>
	/// Compares this monetary value to another.
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

	// ========================================================================
	// Equality and formatting
	// ========================================================================

	/// <inheritdoc />
	public override IEnumerable<object?> GetEqualityComponents()
	{
		yield return Amount;
		yield return CurrencyCode;
		yield return Denomination;
	}

	/// <summary>
	/// Returns a culture-neutral string representation: "{Amount} {CurrencyCode}".
	/// </summary>
	/// <returns>A string like "1234.56 USD".</returns>
	/// <remarks>
	/// For culture-specific formatting (e.g., "$1,234.56" or "1.234,56 €"),
	/// use <see cref="ToString(CultureInfo)"/>.
	/// </remarks>
	public override string ToString() => $"{Amount} {CurrencyCode}";

	/// <summary>
	/// Returns a culture-specific currency-formatted string.
	/// </summary>
	/// <param name="culture">The culture to use for number formatting.</param>
	/// <returns>A culture-formatted currency string (e.g., "$1,234.56" for en-US).</returns>
	/// <remarks>
	/// <para>
	/// The same <see cref="Money"/> value can be formatted differently depending on the culture:
	/// <code>
	/// var price = Money.EUR(1234.56m);
	/// price.ToString(new CultureInfo("de-DE")); // "1.234,56 €"
	/// price.ToString(new CultureInfo("fr-FR")); // "1 234,56 €"
	/// price.ToString(new CultureInfo("en-US")); // "€1,234.56"
	/// </code>
	/// </para>
	/// </remarks>
	public string ToString(CultureInfo culture)
	{
		ArgumentNullException.ThrowIfNull(culture);
		return Amount.ToString("C", culture);
	}

	/// <summary>
	/// Returns a string representation with the specified format using the invariant culture.
	/// </summary>
	/// <param name="format">The format string (e.g., "N2", "F4").</param>
	public string ToString(string format) => Amount.ToString(format, CultureInfo.InvariantCulture);

	/// <summary>
	/// Returns a string representation with the specified format and culture.
	/// </summary>
	/// <param name="format">The format string (e.g., "N2", "F4").</param>
	/// <param name="culture">The culture to use for number formatting.</param>
	public string ToString(string format, CultureInfo culture)
	{
		ArgumentNullException.ThrowIfNull(culture);
		return Amount.ToString(format, culture);
	}

	private static void EnsureSameCurrency(Money left, Money right)
	{
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		if (!string.Equals(left.CurrencyCode, right.CurrencyCode, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Cannot perform operations on money with different currencies: '{left.CurrencyCode}' and '{right.CurrencyCode}'.");
		}
	}
}
