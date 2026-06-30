namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Represents a void/no-meaningful-value response, providing the <c>Unit</c> shape expected by code
/// written against the MediatR API so that request handlers without a response can participate in the
/// generic request/response pipeline.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    /// <summary>
    /// Gets the single, default value of <see cref="Unit"/>.
    /// </summary>
    /// <value>The shared <see cref="Unit"/> instance.</value>
    public static readonly Unit Value;

    /// <summary>
    /// Gets a completed task whose result is the <see cref="Unit"/> value, for returning from
    /// asynchronous handlers that produce no meaningful response.
    /// </summary>
    /// <value>A completed <see cref="Task{TResult}"/> of <see cref="Unit"/>.</value>
    public static Task<Unit> Task { get; } = System.Threading.Tasks.Task.FromResult(Value);

    /// <inheritdoc/>
    public bool Equals(Unit other) => true;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc/>
    public override int GetHashCode() => 0;

    /// <inheritdoc/>
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc/>
    public int CompareTo(object? obj) => 0;

    /// <inheritdoc/>
    public override string ToString() => "()";

    /// <summary>Determines whether two <see cref="Unit"/> values are equal (always <see langword="true"/>).</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/>.</returns>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>Determines whether two <see cref="Unit"/> values are unequal (always <see langword="false"/>).</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="false"/>.</returns>
    public static bool operator !=(Unit left, Unit right) => false;

    /// <summary>Compares two <see cref="Unit"/> values (always <see langword="false"/>).</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="false"/>.</returns>
    public static bool operator <(Unit left, Unit right) => false;

    /// <summary>Compares two <see cref="Unit"/> values (always <see langword="false"/>).</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="false"/>.</returns>
    public static bool operator >(Unit left, Unit right) => false;

    /// <summary>Compares two <see cref="Unit"/> values (always <see langword="true"/>).</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/>.</returns>
    public static bool operator <=(Unit left, Unit right) => true;

    /// <summary>Compares two <see cref="Unit"/> values (always <see langword="true"/>).</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/>.</returns>
    public static bool operator >=(Unit left, Unit right) => true;
}
