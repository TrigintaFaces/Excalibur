// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using FluentValidation;

namespace Excalibur.Application;

/// <summary>
/// Provides extension methods for FluentValidation rules to validate tenant IDs and correlation IDs.
/// </summary>
public static class ValidationExtensions
{
	/// <summary>
	/// Defines a validation rule for tenant IDs to ensure they are not empty.
	/// </summary>
	/// <typeparam name="T"> The type of the object being validated. </typeparam>
	/// <param name="ruleBuilder"> The rule builder to extend. </param>
	/// <returns> An <see cref="IRuleBuilderOptions{T,String}" /> that can be used to configure additional options for the rule. </returns>
	public static IRuleBuilderOptions<T, string> IsValidTenantId<T>(this IRuleBuilder<T, string> ruleBuilder) => ruleBuilder.NotEmpty();

	/// <summary>
	/// Defines a validation rule for correlation IDs to ensure they are not empty GUIDs.
	/// </summary>
	/// <typeparam name="T"> The type of the object being validated. </typeparam>
	/// <param name="ruleBuilder"> The rule builder to extend. </param>
	/// <returns> An <see cref="IRuleBuilderOptions{T,Guid}" /> that can be used to configure additional options for the rule. </returns>
	public static IRuleBuilderOptions<T, Guid> IsValidCorrelationId<T>(this IRuleBuilder<T, Guid> ruleBuilder) =>
		ruleBuilder.NotEmpty().WithMessage("{PropertyName} must not be an empty guid.");
}
