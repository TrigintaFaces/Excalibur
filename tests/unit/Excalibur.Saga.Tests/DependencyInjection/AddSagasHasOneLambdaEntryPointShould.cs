// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// rpgp4z — one lambda entry point for <c>AddSagas</c>. Two overloads that both accept a
/// single-parameter lambda are ambiguous by construction: C# does not use a statement lambda's BODY for
/// overload resolution, so the natural consumer call binds to whichever the compiler prefers and then
/// fails against a type the consumer never wrote.
/// </summary>
/// <remarks>
/// <para>
/// The concrete cost was measurable: <c>AddSagas(saga =&gt; { saga.UseSqlServer(...); saga.WithCoordination(); })</c>
/// bound to an <c>Action&lt;SagaOptions&gt;</c> overload and failed with CS1061 naming <c>SagaOptions</c>.
/// The saga template could not compile until the lambda parameter was explicitly typed — a workaround no
/// consumer should have to infer from that error.
/// </para>
/// <para>
/// <b>Why a reflection assertion rather than a compile-time one:</b> the failure this guards is a
/// COMPILE error in consumer code, and a test cannot assert that something fails to compile. The
/// property that prevents it — at most one lambda-taking overload — is checkable here and goes RED the
/// moment a second one is added.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class AddSagasHasOneLambdaEntryPointShould
{
	[Fact]
	public void ExposeExactlyOneOverloadTakingASingleParameterLambda()
	{
		var overloads = typeof(Microsoft.Extensions.DependencyInjection.SagaExcaliburBuilderExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(static m => m.Name == "AddSagas")
			.Where(static m =>
			{
				var parameters = m.GetParameters();
				if (parameters.Length != 2)
				{
					return false;
				}

				var second = parameters[1].ParameterType;
				return second.IsGenericType && second.GetGenericTypeDefinition() == typeof(Action<>);
			})
			.ToList();

		overloads.Count.ShouldBe(
			1,
			"two AddSagas overloads both taking a one-parameter lambda cannot be told apart by the "
			+ "compiler from the lambda's body, so the natural call binds to the wrong one and fails "
			+ "naming a type the consumer never mentioned. Configure options through the builder "
			+ "(saga => saga.WithOptions(...)) rather than adding a second lambda entry point. "
			+ $"Found: {string.Join(", ", overloads.Select(static o => o.GetParameters()[1].ParameterType.Name))}");
	}
}
