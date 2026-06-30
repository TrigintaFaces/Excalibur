using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Validates <see cref="MediatRCompatOptions"/> at startup (fail-fast via <c>ValidateOnStart</c>).
/// </summary>
internal sealed class MediatRCompatOptionsValidator : IValidateOptions<MediatRCompatOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, MediatRCompatOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("MediatRCompatOptions instance is null.");
        }

        if (options.Assemblies.Count == 0)
        {
            return ValidateOptionsResult.Fail(
                "No assemblies registered. Call RegisterServicesFromAssembly(...) (or an overload) so handlers can be discovered.");
        }

        return ValidateOptionsResult.Success;
    }
}
