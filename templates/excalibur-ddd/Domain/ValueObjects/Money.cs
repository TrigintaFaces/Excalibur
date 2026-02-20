namespace Company.ExcaliburDdd.Domain.ValueObjects;

/// <summary>
/// Represents a monetary value with currency.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency = "USD")
{
    /// <summary>
    /// Creates a zero-value <see cref="Money"/> instance.
    /// </summary>
    public static Money Zero(string currency = "USD") => new(0m, currency);

    /// <summary>
    /// Adds two monetary values. Both must share the same currency.
    /// </summary>
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot add {left.Currency} and {right.Currency}.");

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Amount:F2} {Currency}";
}
