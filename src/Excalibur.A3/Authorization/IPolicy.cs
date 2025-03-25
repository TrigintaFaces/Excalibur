namespace Excalibur.A3.Authorization;

/// <summary>
///     Represents the base interface for all policy types.
/// </summary>
/// <remarks>
///     This interface acts as a marker for policies, allowing type constraints and polymorphism for policy-related operations. Derived
///     interfaces or classes should define specific properties and methods for the policy they represent.
/// </remarks>
public interface IPolicy
{
}
