using System.Globalization;

namespace Excalibur.Core.Domain.Model.ValueObjects;

/// <summary>
///     A culture-aware class that represents money. This is a value object which means it should be immutable. Do not change properties accessibility.
/// </summary>
public class Money : ValueObjectBase, IEquatable<decimal?>, IComparable<Money?>, IComparable<decimal?>
{
	private static readonly CultureInfo DefaultCulture = Cultures.GetCultureInfo(Cultures.DefaultCultureName);
	private readonly CultureInfo _cultureInfo;

	/// <summary>
	///     Initializes a new instance of the <see cref="Money" /> class with a specified amount and default culture.
	/// </summary>
	/// <param name="amount"> The monetary amount. </param>
	public Money(decimal amount)
		: this(amount, DefaultCulture)
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="Money" /> class with a specific amount and culture.
	/// </summary>
	/// <param name="amount"> The monetary amount. </param>
	/// <param name="cultureName"> The culture name for formatting. </param>
	/// <param name="denomination"> The denomination of the money, if applicable. </param>
	[Newtonsoft.Json.JsonConstructor]
	[System.Text.Json.Serialization.JsonConstructor]
	public Money(decimal amount, string cultureName, decimal? denomination = null)
		: this(amount, !string.IsNullOrWhiteSpace(cultureName) ? Cultures.GetCultureInfo(cultureName) : DefaultCulture, denomination)
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="Money" /> class with a specific amount and culture info.
	/// </summary>
	/// <param name="amount"> The monetary amount. </param>
	/// <param name="cultureInfo"> The culture info for formatting. </param>
	/// <param name="denomination"> The denomination of the money, if applicable. </param>
	public Money(decimal amount, CultureInfo cultureInfo, decimal? denomination = null)
	{
		Amount = amount;
		Denomination = denomination;
		_cultureInfo = cultureInfo;

		ValidateConstraints();
	}

	/// <summary>
	///     Gets the monetary amount.
	/// </summary>
	public decimal Amount { get; }

	/// <summary>
	///     Gets the denomination value, if applicable.
	/// </summary>
	public decimal? Denomination { get; }

	/// <summary>
	///     Gets the number of units of the denomination represented by the amount, if applicable.
	/// </summary>
	public int? UnitCount
	{
		get
		{
			int? unitCount = null;

			if (Denomination is > 0)
			{
				unitCount = (int)(Amount / Denomination.Value);
			}

			return unitCount;
		}
	}

	/// <summary>
	///     Gets a value indicating whether the money represents coins only, based on the denomination.
	/// </summary>
	public bool? IsCoinsOnly => Denomination.HasValue ? !IsWholeNumber(Denomination.Value) : null;

	/// <summary>
	///     Gets a value indicating whether the money represents bills only, based on the denomination.
	/// </summary>
	public bool? IsBillsOnly => Denomination.HasValue ? IsWholeNumber(Denomination.Value) : null;

	/// <summary>
	///     Gets the name of the culture used for formatting.
	/// </summary>
	public string CultureName => _cultureInfo.Name;

	/// <summary>
	///     Gets the currency symbol for the culture.
	/// </summary>
	public string CurrencySymbol => _cultureInfo.NumberFormat.CurrencySymbol;

	/// <summary>
	///     Gets the ISO 4217 currency code for the culture associated with this <see cref="Money" /> instance.
	/// </summary>
	/// <exception cref="InvalidOperationException"> Thrown when the culture does not have an associated region. </exception>
	// ReSharper disable once InconsistentNaming
	public string ISOCurrencyCode
	{
		get
		{
			try
			{
				return new RegionInfo(_cultureInfo.Name).ISOCurrencySymbol;
			}
			catch (ArgumentException ex)
			{
				throw new InvalidOperationException($"Culture '{_cultureInfo.Name}' does not have an associated region.", ex);
			}
		}
	}

	/// <summary>
	///     Creates a new <see cref="Money" /> instance with a zero amount and the specified culture name.
	/// </summary>
	/// <param name="cultureName"> The culture name. </param>
	/// <returns> A new <see cref="Money" /> instance. </returns>
	public static Money Zero(string cultureName) => new(0M, cultureName);

	/// <summary>
	///     Creates a new <see cref="Money" /> instance with a zero amount and the specified culture info.
	/// </summary>
	/// <param name="cultureInfo"> The culture info. </param>
	/// <returns> A new <see cref="Money" /> instance. </returns>
	public static Money Zero(CultureInfo cultureInfo) => new(0M, cultureInfo);

	/// <summary>
	///     Creates a new <see cref="Money" /> instance with a zero amount and the default culture.
	/// </summary>
	/// <returns> A new <see cref="Money" /> instance. </returns>
	public static Money Zero() => new(0M, DefaultCulture);

	/// <summary>
	///     Determines whether two <see cref="Money" /> instances are equal.
	/// </summary>
	/// <param name="left"> The first instance to compare. </param>
	/// <param name="right"> The second instance to compare. </param>
	/// <returns> True if the instances are equal; otherwise, false. </returns>
	public static bool operator ==(Money? left, Money? right)
	{
		if (ReferenceEquals(left, right))
		{
			return true;
		}

		if (left is null || right is null)
		{
			return false;
		}

		return left.Equals(right);
	}

	/// <summary>
	///     Determines whether two <see cref="Money" /> instances are not equal.
	/// </summary>
	/// <param name="left"> The first instance to compare. </param>
	/// <param name="right"> The second instance to compare. </param>
	/// <returns> True if the instances are not equal; otherwise, false. </returns>
	public static bool operator !=(Money? left, Money? right) => !(left == right);

	/// <summary>
	///     Subtracts one <see cref="Money" /> instance from another, ensuring they share the same culture.
	/// </summary>
	/// <param name="left"> The <see cref="Money" /> instance to subtract from. </param>
	/// <param name="right"> The <see cref="Money" /> instance to subtract. </param>
	/// <returns> A new <see cref="Money" /> instance representing the difference. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	/// <exception cref="ArgumentNullException"> Thrown if the decimal instance is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown if the cultures of the two instances do not match. </exception>
	public static Money operator -(Money left, Money right)
	{
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		EnsureCommonCultureForOperations(left, right);

		var denomination = GetResultDenomination(left, right);

		return new Money(left.Amount - right.Amount, left.CultureName, denomination);
	}

	/// <summary>
	///     Subtracts a decimal value from a <see cref="Money" /> instance.
	/// </summary>
	/// <param name="left"> The <see cref="Money" /> instance. </param>
	/// <param name="right"> The decimal value to subtract. </param>
	/// <returns> A new <see cref="Money" /> instance representing the difference. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	/// <exception cref="ArgumentNullException"> Thrown if the decimal instance is null. </exception>
	public static Money operator -(Money left, decimal right)
	{
		ArgumentNullException.ThrowIfNull(left);

		var denomination = GetResultDenomination(left, right);

		return new Money(left.Amount - right, left.CultureName, denomination);
	}

	/// <summary>
	///     Subtracts a <see cref="Money" /> value from a decimal instance.
	/// </summary>
	/// <param name="left"> The decimal value to subtract. </param>
	/// <param name="right"> The <see cref="Money" /> instance. </param>
	/// <returns> A new <see cref="Money" /> instance representing the difference. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the decimal instance is null. </exception>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	public static Money operator -(decimal left, Money? right)
	{
		ArgumentNullException.ThrowIfNull(right);

		var denomination = GetResultDenomination(left, right);

		return new Money(left - right.Amount, right.CultureName, denomination);
	}

	/// <summary>
	///     Adds two <see cref="Money" /> instances, ensuring they share the same culture.
	/// </summary>
	/// <param name="left"> The first <see cref="Money" /> instance. </param>
	/// <param name="right"> The second <see cref="Money" /> instance. </param>
	/// <returns> A new <see cref="Money" /> instance representing the sum. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the decimal instance is null. </exception>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	/// <exception cref="InvalidOperationException">
	///     Thrown if the cultures of the two <see cref="Money" /> instances are not the same.
	/// </exception>
	public static Money operator +(Money left, Money right)
	{
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		EnsureCommonCultureForOperations(left, right);

		var denomination = GetResultDenomination(left, right);

		return new Money(left.Amount + right.Amount, left.CultureName, denomination);
	}

	/// <summary>
	///     Adds a decimal value to a <see cref="Money" /> instance.
	/// </summary>
	/// <param name="left"> The <see cref="Money" /> instance. </param>
	/// <param name="right"> The decimal value to add. </param>
	/// <returns> A new <see cref="Money" /> instance representing the sum. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	public static Money operator +(Money left, decimal right)
	{
		ArgumentNullException.ThrowIfNull(left);

		var denomination = GetResultDenomination(left, right);

		return new Money(left.Amount + right, left.CultureName, denomination);
	}

	/// <summary>
	///     Adds a <see cref="Money" /> value to a decimal instance.
	/// </summary>
	/// <param name="left"> The decimal value to add. </param>
	/// <param name="right"> The <see cref="Money" /> instance. </param>
	/// <returns> A new <see cref="Money" /> instance representing the sum. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	public static Money operator +(decimal left, Money? right)
	{
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		var denomination = GetResultDenomination(left, right);

		return new Money(left + right.Amount, right.CultureName, denomination);
	}

	/// <summary>
	///     Multiplies a <see cref="Money" /> instance by a decimal value.
	/// </summary>
	/// <param name="left"> The <see cref="Money" /> instance. </param>
	/// <param name="right"> The decimal multiplier. </param>
	/// <returns> A new <see cref="Money" /> instance representing the product. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	public static Money operator *(Money? left, decimal right)
	{
		ArgumentNullException.ThrowIfNull(left);

		return new Money(left.Amount * right, left.CultureName);
	}

	/// <summary>
	///     Multiplies a decimal value by a <see cref="Money" /> instance.
	/// </summary>
	/// <param name="left"> The decimal multiplier. </param>
	/// <param name="right"> The <see cref="Money" /> instance. </param>
	/// <returns> A new <see cref="Money" /> instance representing the product. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	public static Money operator *(decimal left, Money? right)
	{
		ArgumentNullException.ThrowIfNull(right);

		return new Money(left * right.Amount, right.CultureName);
	}

	/// <summary>
	///     Divides a <see cref="Money" /> instance by a decimal value.
	/// </summary>
	/// <param name="left"> The <see cref="Money" /> instance. </param>
	/// <param name="right"> The decimal divisor. </param>
	/// <returns> A new <see cref="Money" /> instance representing the quotient. </returns>
	/// <exception cref="DivideByZeroException"> Thrown if the divisor is zero. </exception>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	public static Money operator /(Money? left, decimal right)
	{
		ArgumentNullException.ThrowIfNull(left);

		if (right == decimal.Zero)
		{
			throw new DivideByZeroException("Cannot divide by zero.");
		}

		return new Money(left.Amount / right, left.CultureName);
	}

	/// <summary>
	///     Compares two <see cref="Money" /> instances to determine if one is less than the other.
	/// </summary>
	/// <param name="left"> The first <see cref="Money" /> instance. </param>
	/// <param name="right"> The second <see cref="Money" /> instance. </param>
	/// <returns> True if the first instance is less than the second; otherwise, false. </returns>
	public static bool operator <(Money left, Money right) =>
		ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;

	/// <summary>
	///     Compares two <see cref="Money" /> instances to determine if one is less than or equal to the other.
	/// </summary>
	/// <param name="left"> The first <see cref="Money" /> instance. </param>
	/// <param name="right"> The second <see cref="Money" /> instance. </param>
	/// <returns> True if the first instance is less than or equal to the second; otherwise, false. </returns>
	public static bool operator <=(Money left, Money right) => ReferenceEquals(left, null) || left.CompareTo(right) <= 0;

	/// <summary>
	///     Compares two <see cref="Money" /> instances to determine if one is greater than the other.
	/// </summary>
	/// <param name="left"> The first <see cref="Money" /> instance. </param>
	/// <param name="right"> The second <see cref="Money" /> instance. </param>
	/// <returns> True if the first instance is greater than the second; otherwise, false. </returns>
	public static bool operator >(Money left, Money right) => !ReferenceEquals(left, null) && left.CompareTo(right) > 0;

	/// <summary>
	///     Compares two <see cref="Money" /> instances to determine if one is greater than or equal to the other.
	/// </summary>
	/// <param name="left"> The first <see cref="Money" /> instance. </param>
	/// <param name="right"> The second <see cref="Money" /> instance. </param>
	/// <returns> True if the first instance is greater than or equal to the second; otherwise, false. </returns>
	public static bool operator >=(Money left, Money right) =>
		ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;

	/// <summary>
	///     Compares two <see cref="Money" /> instances to determine if one is less than the other.
	/// </summary>
	/// <param name="left"> The first <see cref="Money" /> instance. </param>
	/// <param name="right"> The second <see cref="Money" /> instance. </param>
	/// <returns> True if the first instance is less than the second; otherwise, false. </returns>
	public static bool operator <(Money left, decimal right)
	{
		ArgumentNullException.ThrowIfNull(left);

		return left.CompareTo(right) < 0;
	}

	/// <summary>
	///     Compares a <see cref="Money" /> instance to a decimal value to determine if the <see cref="Money" /> instance is less than or
	///     equal to the decimal value.
	/// </summary>
	/// <param name="left"> The <see cref="Money" /> instance. </param>
	/// <param name="right"> The decimal value to compare to. </param>
	/// <returns> True if the <see cref="Money" /> instance is less than or equal to the decimal value; otherwise, false. </returns>
	public static bool operator <=(Money left, decimal right)
	{
		ArgumentNullException.ThrowIfNull(left);

		return left.CompareTo(right) <= 0;
	}

	/// <summary>
	///     Compares a <see cref="Money" /> instance to a decimal value to determine if the <see cref="Money" /> instance is greater than
	///     the decimal value.
	/// </summary>
	/// <param name="left"> The <see cref="Money" /> instance. </param>
	/// <param name="right"> The decimal value to compare to. </param>
	/// <returns> True if the <see cref="Money" /> instance is greater than the decimal value; otherwise, false. </returns>
	public static bool operator >(Money left, decimal right)
	{
		ArgumentNullException.ThrowIfNull(left);

		return left.CompareTo(right) > 0;
	}

	/// <summary>
	///     Compares a <see cref="Money" /> instance to a decimal value to determine if the <see cref="Money" /> instance is greater than or
	///     equal to the decimal value.
	/// </summary>
	/// <param name="left"> The <see cref="Money" /> instance. </param>
	/// <param name="right"> The decimal value to compare to. </param>
	/// <returns> True if the <see cref="Money" /> instance is greater than or equal to the decimal value; otherwise, false. </returns>
	public static bool operator >=(Money left, decimal right)
	{
		ArgumentNullException.ThrowIfNull(left);

		return left.CompareTo(right) >= 0;
	}

	/// <summary>
	///     Creates a new <see cref="Money" /> instance with a specified amount, culture name, and optional denomination.
	/// </summary>
	/// <param name="amount"> The monetary amount. </param>
	/// <param name="cultureName"> The culture name for formatting. </param>
	/// <param name="denomination"> The denomination of the money, if applicable. </param>
	/// <returns> A new <see cref="Money" /> instance. </returns>
	public static Money From(decimal amount, string? cultureName, decimal? denomination = null) =>
		new(amount, cultureName ?? Cultures.DefaultCultureName, denomination);

	/// <summary>
	///     Creates a new <see cref="Money" /> instance with a specified integer amount, culture name, and optional denomination.
	/// </summary>
	/// <param name="amount"> The monetary amount as an integer. </param>
	/// <param name="cultureName"> The culture name for formatting. </param>
	/// <param name="denomination"> The denomination of the money, if applicable. </param>
	/// <returns> A new <see cref="Money" /> instance. </returns>
	public static Money From(int amount, string? cultureName, decimal? denomination = null) =>
		new(amount, cultureName ?? Cultures.DefaultCultureName, denomination);

	/// <summary>
	///     Creates a new <see cref="Money" /> instance with a specified long amount, culture name, and optional denomination.
	/// </summary>
	/// <param name="amount"> The monetary amount as a long. </param>
	/// <param name="cultureName"> The culture name for formatting. </param>
	/// <param name="denomination"> The denomination of the money, if applicable. </param>
	/// <returns> A new <see cref="Money" /> instance. </returns>
	public static Money From(long amount, string? cultureName, decimal? denomination = null) =>
		new(amount, cultureName ?? Cultures.DefaultCultureName, denomination);

	/// <summary>
	///     Creates a new <see cref="Money" /> instance with a specified double amount, culture name, and optional denomination.
	/// </summary>
	/// <param name="amount"> The monetary amount as a double. </param>
	/// <param name="cultureName"> The culture name for formatting. </param>
	/// <param name="denomination"> The denomination of the money, if applicable. </param>
	/// <returns> A new <see cref="Money" /> instance. </returns>
	public static Money From(double amount, string? cultureName, decimal? denomination = null) =>
		new(Convert.ToDecimal(amount), cultureName ?? Cultures.DefaultCultureName, denomination);

	/// <summary>
	///     Creates a new <see cref="Money" /> instance with a specified float amount, culture name, and optional denomination.
	/// </summary>
	/// <param name="amount"> The monetary amount as a float. </param>
	/// <param name="cultureName"> The culture name for formatting. </param>
	/// <param name="denomination"> The denomination of the money, if applicable. </param>
	/// <returns> A new <see cref="Money" /> instance. </returns>
	public static Money From(float amount, string? cultureName, decimal? denomination = null) =>
		new(Convert.ToDecimal(amount), cultureName ?? Cultures.DefaultCultureName, denomination);

	/// <summary>
	///     Creates a new <see cref="Money" /> instance with a specified string amount, culture name, and optional denomination.
	/// </summary>
	/// <param name="amount"> The monetary amount as a string. </param>
	/// <param name="cultureName"> The culture name for formatting. </param>
	/// <param name="denomination"> The denomination of the money, if applicable. </param>
	/// <returns> A new <see cref="Money" /> instance. </returns>
	/// <exception cref="FormatException"> Thrown if the string amount cannot be parsed into a decimal. </exception>
	public static Money From(string amount, string? cultureName, decimal? denomination = null) =>
		new(decimal.Parse(amount, CultureInfo.InvariantCulture), cultureName ?? Cultures.DefaultCultureName, denomination);

	/// <summary>
	///     Adds metadata to an existing <see cref="Money" /> instance, such as culture or denomination.
	/// </summary>
	/// <param name="money"> The <see cref="Money" /> instance to modify. </param>
	/// <param name="cultureName"> The culture name to associate with the instance. </param>
	/// <param name="denomination"> The denomination to associate with the instance. </param>
	/// <returns> A new <see cref="Money" /> instance with updated metadata. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <see cref="Money" /> instance is null. </exception>
	public static Money AddMetadata(Money money, string cultureName, decimal? denomination = null)
	{
		ArgumentNullException.ThrowIfNull(money);

		var newCultureInfo = !string.IsNullOrWhiteSpace(cultureName)
			? Cultures.GetCultureInfo(cultureName)
			: money._cultureInfo;

		var newDenomination = denomination ?? money.Denomination;

		return new Money(money.Amount, newCultureInfo, newDenomination);
	}

	/// <summary>
	///     Adds metadata to the current <see cref="Money" /> instance, such as culture or denomination.
	/// </summary>
	/// <param name="cultureName"> The culture name to associate with the instance. </param>
	/// <param name="denomination"> The denomination to associate with the instance. </param>
	/// <returns> A new <see cref="Money" /> instance with updated metadata. </returns>
	public Money WithMetadata(string cultureName, decimal? denomination = null) => AddMetadata(this, cultureName, denomination);

	/// <summary>
	///     Compares the current <see cref="Money" /> instance to another <see cref="Money" /> instance.
	/// </summary>
	/// <param name="other"> The other <see cref="Money" /> instance to compare to. </param>
	/// <returns> An integer that indicates the relative order of the objects being compared. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the other <see cref="Money" /> instance is null. </exception>
	public int CompareTo(Money? other)
	{
		if (other is null)
		{
			return 1;
		}

		EnsureCommonCultureForOperations(this, other);
		return Amount.CompareTo(other.Amount);
	}

	/// <summary>
	///     Compares the current <see cref="Money" /> instance to a decimal value.
	/// </summary>
	/// <param name="other"> The decimal value to compare to. </param>
	/// <returns> An integer that indicates the relative order of the objects being compared. </returns>
	public int CompareTo(decimal? other) => other is null ? 1 : Amount.CompareTo(other);

	/// <summary>
	///     Determines whether the specified object is equal to the current <see cref="Money" /> instance.
	/// </summary>
	/// <param name="obj"> The object to compare with the current instance. </param>
	/// <returns>
	///     True if the specified object is a <see cref="Money" /> instance or a decimal value that equals the current instance; otherwise, false.
	/// </returns>
	public override bool Equals(object? obj) => obj switch
	{
		Money money => base.Equals(money),
		decimal amount => Equals(amount),
		_ => false
	};

	/// <summary>
	///     Checks whether the current <see cref="Money" /> instance is equal to a decimal value.
	/// </summary>
	/// <param name="other"> The decimal value to compare to. </param>
	/// <returns> True if the instances are equal; otherwise, false. </returns>
	public bool Equals(decimal? other) => Amount == other;

	/// <summary>
	///     Converts the monetary value to a formatted string using the culture-specific currency format.
	/// </summary>
	/// <returns> A formatted string representing the monetary value, including the currency symbol. </returns>
	public override string ToString() => string.Format(_cultureInfo, "{0:C}", Amount);

	/// <summary>
	///     Converts the monetary value to a formatted string, optionally excluding the currency symbol.
	/// </summary>
	/// <param name="excludeSymbol"> If true, excludes the currency symbol in the formatted output. </param>
	/// <returns> A formatted string representing the monetary value. </returns>
	public string ToString(bool excludeSymbol)
	{
		if (excludeSymbol)
		{
			var numberFormatInfo = (NumberFormatInfo)_cultureInfo.NumberFormat.Clone();
			numberFormatInfo.CurrencySymbol = string.Empty;

			return string.Format(numberFormatInfo, "{0:c}", Amount).Trim(); // Trim() because some currency symbols are at the end
		}

		return ToString();
	}

	/// <inheritdoc />
	public override int GetHashCode() => base.GetHashCode();

	/// <inheritdoc />
	protected override bool EqualsInternal(IValueObject? other)
	{
		if (other is not Money otherMoney)
		{
			return false;
		}

		return Amount == otherMoney.Amount && CultureName == otherMoney.CultureName;
	}

	/// <inheritdoc />
	protected override int GetHashCodeInternal() => HashCode.Combine(Amount, CultureName);

	private static bool IsWholeNumber(decimal value) => decimal.Truncate(value) == value;

	private static decimal? GetResultDenomination(Money left, Money right) =>
		left.Denomination == right.Denomination ? left.Denomination : null;

	private static decimal? GetResultDenomination(Money left, decimal right)
	{
		decimal? denomination = null;
		if (left.Denomination.HasValue && right % left.Denomination.Value == 0)
		{
			denomination = left.Denomination;
		}

		return denomination;
	}

	private static decimal? GetResultDenomination(decimal left, Money right)
	{
		decimal? denomination = null;
		if (right.Denomination.HasValue && left % right.Denomination.Value == 0)
		{
			denomination = right.Denomination;
		}

		return denomination;
	}

	private static void EnsureCommonCultureForOperations(Money left, Money right)
	{
		if (!left.CultureName.Equals(right.CultureName, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("In order to perform operations on Money object, they must use the same culture.");
		}
	}

	private void ValidateConstraints()
	{
		switch (Denomination)
		{
			case null:
				return;

			case <= 0:
				throw new InvalidOperationException("Denomination must be greater than zero if specified.");
			default:
				break;
		}

		if (Amount % Denomination.Value != 0)
		{
			throw new InvalidOperationException("Amount must be a multiple of the denomination.");
		}

		if (IsBillsOnly == true && !IsWholeNumber(Amount))
		{
			throw new InvalidOperationException("Amount must be a whole number if the denomination is a bill.");
		}
	}
}
