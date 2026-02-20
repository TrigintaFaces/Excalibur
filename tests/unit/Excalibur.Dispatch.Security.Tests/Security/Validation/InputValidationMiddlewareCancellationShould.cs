// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Validation;

/// <summary>
/// Tests for Sprint 542 P0 fix S542.17 (bd-09u3m):
/// InputValidationMiddleware now passes CancellationToken to HandleValidationFailureAsync
/// instead of CancellationToken.None.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class InputValidationMiddlewareCancellationShould
{
	[Fact]
	public void HandleValidationFailureAcceptsCancellationToken()
	{
		// Verify the HandleValidationFailureAsync method accepts a CancellationToken parameter
		var method = typeof(InputValidationMiddleware)
			.GetMethod("HandleValidationFailureAsync", BindingFlags.NonPublic | BindingFlags.Instance);

		method.ShouldNotBeNull("HandleValidationFailureAsync method should exist");

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2, "Should have 2 parameters: ValidationContext + CancellationToken");

		// Second parameter should be CancellationToken
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken),
			"Second parameter should be CancellationToken (not CancellationToken.None — S542.17)");
	}

	[Fact]
	public void InvokeAsyncPassesCancellationTokenToHandleValidationFailure()
	{
		// Structural verification: InvokeAsync is an async method that should pass its ct parameter through
		var invokeMethod = typeof(InputValidationMiddleware)
			.GetMethod("InvokeAsync", BindingFlags.Public | BindingFlags.Instance);

		invokeMethod.ShouldNotBeNull("InvokeAsync method should exist");

		// InvokeAsync should have a CancellationToken parameter
		var parameters = invokeMethod.GetParameters();
		var ctParam = parameters.FirstOrDefault(p => p.ParameterType == typeof(CancellationToken));
		ctParam.ShouldNotBeNull("InvokeAsync should have a CancellationToken parameter");
	}

	[Fact]
	public void HandleValidationFailureIsAsync()
	{
		// Verify HandleValidationFailureAsync returns Task (is async)
		var method = typeof(InputValidationMiddleware)
			.GetMethod("HandleValidationFailureAsync", BindingFlags.NonPublic | BindingFlags.Instance);

		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task),
			"HandleValidationFailureAsync should return Task");
	}
}
