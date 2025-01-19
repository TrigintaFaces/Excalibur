using System.Diagnostics;

using FluentValidation;

using MediatR;

namespace Excalibur.Application.Behaviors;

/// <summary>
///     Pipeline behavior that validates incoming requests using a collection of validators.
/// </summary>
/// <typeparam name="TRequest"> The type of the request. </typeparam>
/// <typeparam name="TResponse"> The type of the response. </typeparam>
/// <remarks>
///     This behavior ensures that the request meets the validation criteria defined by the provided validators. If validation fails, a
///     <see cref="ValidationException" /> is thrown.
/// </remarks>
public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	private readonly IEnumerable<IValidator<TRequest>> _validators;

	/// <summary>
	///     Initializes a new instance of the <see cref="ValidationBehaviour{TRequest, TResponse}" /> class.
	/// </summary>
	/// <param name="validators"> The collection of validators for the request. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="validators" /> is null. </exception>
	public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
	{
		ArgumentNullException.ThrowIfNull(validators);

		_validators = validators;
	}

	/// <inheritdoc />
	public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(next);

		if (!_validators.Any())
		{
			return await next().ConfigureAwait(false);
		}

		var context = new ValidationContext<TRequest>(request);

		var validationResults =
			await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))).ConfigureAwait(false);

		var failures = validationResults
			.SelectMany(r => r.Errors)
			.Where(f => f != null)
			.ToArray();

		if (failures.Length > 0)
		{
			throw new ValidationException(
				message: $"Validation failed for {TypeNameHelper.GetTypeDisplayName(typeof(TRequest), true)}",
				errors: failures,
				appendDefaultMessage: true
			);
		}

		return await next().ConfigureAwait(false);
	}
}
