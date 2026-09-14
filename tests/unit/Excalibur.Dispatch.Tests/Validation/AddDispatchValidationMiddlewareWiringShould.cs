// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

using ValidationException = Excalibur.Dispatch.Exceptions.ValidationException;

namespace Excalibur.Dispatch.Tests.Validation;

/// <summary>
/// Empirical regression lock for 2lxfic, in the same shape as bgu603's
/// <c>AddDispatchConfigureMiddlewareWiringShould</c>: a consumer who writes
/// <c>services.AddDispatchValidation()</c> beside <c>services.AddDispatch(...)</c> -- the bare Add form,
/// no <c>UseValidation()</c> -- must get real validation, not a clean build that silently validates
/// nothing.
/// </summary>
/// <remarks>
/// Deliberately empirical, not a registration assertion: resolving <c>IMessageValidationService</c> or
/// <c>ValidationMiddleware</c> from the container proves they are IN it, not that the composed pipeline
/// invokes them. This dispatches a real message through a real, DI-composed <see cref="IDispatcher"/>
/// and asserts the OBSERVABLE effect -- an invalid message is rejected (safety), a valid one still
/// reaches the handler (liveness) -- so a regression that silently drops the middleware again fails this
/// lock even though every registration is still present.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Validation")]
public sealed class AddDispatchValidationMiddlewareWiringShould
{
	[Fact]
	public async Task RejectAnInvalidMessage_WhenValidationRegisteredViaTheBareAddForm_AndComposedThroughAddDispatchConfigure()
	{
		// Arrange: the exact documented registration shape the bead names -- AddDispatchValidation()
		// (bare Add form) alongside AddDispatch(configure) -- with no UseValidation() call anywhere.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var invalidHandler = new RequiredFieldCommandHandler();
		_ = services.AddSingleton<IActionHandler<RequiredFieldCommand>>(invalidHandler);
		_ = services.AddSingleton(invalidHandler);
		_ = services.AddDispatchValidation();
		_ = services.AddDispatch(configure: null);

		var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Act & Assert (safety): Name is [Required] and left empty. If ValidationMiddleware never
		// reached the pipeline this dispatch would silently succeed -- exactly the pre-fix defect.
		_ = await Should.ThrowAsync<ValidationException>(
			async () => await dispatcher.DispatchAsync(
				new RequiredFieldCommand(Name: string.Empty), CancellationToken.None));
	}

	[Fact]
	public async Task PassAValidMessage_WhenValidationRegisteredViaTheBareAddForm_AndComposedThroughAddDispatchConfigure()
	{
		// Liveness arm: the same wiring must not turn into a blanket rejection.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var handler = new RequiredFieldCommandHandler();
		_ = services.AddSingleton<IActionHandler<RequiredFieldCommand>>(handler);
		_ = services.AddSingleton(handler);
		_ = services.AddDispatchValidation();
		_ = services.AddDispatch(configure: null);

		var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new RequiredFieldCommand(Name: "a real name"), CancellationToken.None);

		_ = result.ShouldNotBeNull();
		handler.HandledCount.ShouldBe(1);
	}

	private sealed record RequiredFieldCommand([property: Required] string Name) : IDispatchAction;

	private sealed class RequiredFieldCommandHandler : IActionHandler<RequiredFieldCommand>
	{
		public int HandledCount { get; private set; }

		public Task HandleAsync(RequiredFieldCommand action, CancellationToken cancellationToken)
		{
			HandledCount++;
			return Task.CompletedTask;
		}
	}
}
