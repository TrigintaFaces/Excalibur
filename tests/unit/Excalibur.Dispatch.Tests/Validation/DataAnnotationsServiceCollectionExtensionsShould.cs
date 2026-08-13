// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Validation;
using Excalibur.Dispatch.Validation.DataAnnotations;

// This namespace declares its OWN unrelated ValidationOptions (distinct from
// Excalibur.Dispatch.Options.Middleware.ValidationOptions the middleware actually uses) -- exactly the
// footgun that produced the bug these new tests lock. Never reference ValidationOptions unqualified in
// this file; the tests below configure it only indirectly, through WithDataAnnotationsValidation().
using DispatchValidationException = Excalibur.Dispatch.Exceptions.ValidationException;

namespace Excalibur.Dispatch.Tests.Validation;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class DataAnnotationsServiceCollectionExtensionsShould
{
	#region Registration Tests

	[Fact]
	public void RegisterDataAnnotationsValidatorResolverWhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch => dispatch.WithDataAnnotationsValidation());
		var provider = services.BuildServiceProvider();
		var resolver = provider.GetRequiredService<IValidatorResolver>();

		// Assert
		_ = resolver.ShouldBeOfType<DataAnnotationsValidatorResolver>();
	}

	[Fact]
	public void ReplaceNoOpValidatorResolverWithDataAnnotationsResolver()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - AddDispatch registers NoOpValidatorResolver, WithDataAnnotationsValidation should replace it
		_ = services.AddDispatch(dispatch => dispatch.WithDataAnnotationsValidation());
		var provider = services.BuildServiceProvider();
		var resolvers = provider.GetServices<IValidatorResolver>().ToList();

		// Assert - Should only have one resolver (DataAnnotations)
		resolvers.Count.ShouldBe(1);
		_ = resolvers.Single().ShouldBeOfType<DataAnnotationsValidatorResolver>();
	}

	[Fact]
	public void RegisterValidatorResolverAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithDataAnnotationsValidation());

		// Act
		var provider = services.BuildServiceProvider();
		var resolver1 = provider.GetRequiredService<IValidatorResolver>();
		var resolver2 = provider.GetRequiredService<IValidatorResolver>();

		// Assert
		resolver1.ShouldBeSameAs(resolver2);
	}

	[Fact]
	public void ReturnDispatchBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IDispatchBuilder? capturedBuilder = null;

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			var returnedBuilder = dispatch.WithDataAnnotationsValidation();
			capturedBuilder = dispatch;

			// Assert - method should return the same builder for chaining
			returnedBuilder.ShouldBeSameAs(dispatch);
		});

		// Assert
		_ = capturedBuilder.ShouldNotBeNull();
	}

	#endregion Registration Tests

	#region Exception Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.WithDataAnnotationsValidation());
	}

	#endregion Exception Tests

	#region No Double Evaluation (accumulate-don't-short-circuit vs. the resolver evaluating the same attributes)

	[Fact]
	public async Task ReportExactlyOneError_ForOneViolatedRequiredAttribute_ThroughRealRegistrationPath()
	{
		// Arrange -- the real registration path: UseValidation() wires the middleware + the pluggable
		// validation service; WithDataAnnotationsValidation() swaps in the resolver AND (as of this fix)
		// disables the middleware's OWN DataAnnotations pass, so the attribute is evaluated once.
		var middleware = CreateMiddlewareThroughRealRegistrationPath();
		var context = CreateContext();
		var message = new RequiredNameMessage(); // Name is null -> violates [Required]

		// Act
		var exception = await Should.ThrowAsync<DispatchValidationException>(
			middleware.InvokeAsync(message, context, SuccessDelegate, CancellationToken.None).AsTask());

		// Assert -- exactly one error, not two. A regression that re-enables both sources (removes the
		// UseDataAnnotations = false configuration) reports it twice and this goes RED.
		exception.ValidationErrors.ShouldContainKey("Name");
		exception.ValidationErrors["Name"].Length.ShouldBe(1);
	}

	[Fact]
	public async Task StillReportTheViolation_NotSilentlySuppressed()
	{
		// Arrange -- liveness twin: catches the opposite over-correction -- "fixing" duplicates by
		// disabling BOTH validation sources (UseCustomValidation and UseDataAnnotations) instead of just
		// the redundant one, which would silently let an invalid message dispatch as if it were valid.
		var middleware = CreateMiddlewareThroughRealRegistrationPath();
		var context = CreateContext();
		var message = new RequiredNameMessage();

		// Act & Assert -- must still throw at all.
		_ = await Should.ThrowAsync<DispatchValidationException>(
			middleware.InvokeAsync(message, context, SuccessDelegate, CancellationToken.None).AsTask());
	}

	private static readonly DispatchRequestDelegate SuccessDelegate =
		(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

	private static ValidationMiddleware CreateMiddlewareThroughRealRegistrationPath()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.UseValidation().WithDataAnnotationsValidation());
		var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<ValidationMiddleware>();
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		return context;
	}

	private sealed class RequiredNameMessage : IDispatchMessage
	{
		[Required]
		public string Name { get; set; } = null!;
	}

	#endregion
}