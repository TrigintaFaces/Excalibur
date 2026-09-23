// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

namespace Excalibur.Dispatch.Tests;

/// <summary>
/// A result states how it was produced once, when it is produced, and cannot be made to say otherwise
/// afterwards.
/// </summary>
/// <remarks>
/// The disposition used to be an initializable property derived from a separate constructor flag, so an
/// object initializer could set it to something the constructor arguments contradicted — a result built
/// as a cache hit could be made to claim a handler ran. Taking it from the constructor and leaving no
/// setter is what makes that unsayable rather than merely unlikely, and a setter reappearing is exactly
/// how it would come back.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageResultDispositionIsSetOnceShould
{
	private static readonly IMessageResult[] Results =
	[
		MessageResult.Success(),
		MessageResult.SuccessFromCache(),
		MessageResult.SuccessFromCache(42),
		MessageResult.Success(routingDecision: null, validationResult: "v", authorizationResult: "a",
			disposition: MessageDisposition.SuppressedAsDuplicate),
		MessageResult.Failed("boom"),
		MessageResult.Cancelled(),
	];

	[Fact]
	public void ExposeDispositionWithNoSetter()
	{
		foreach (var result in Results)
		{
			var property = result.GetType().GetProperty(
				nameof(IMessageResult.Disposition),
				BindingFlags.Public | BindingFlags.Instance);

			_ = property.ShouldNotBeNull();
			property.SetMethod.ShouldBeNull(
				$"{result.GetType().Name}.Disposition has a setter (an 'init' accessor counts), so a producer "
				+ "can construct a result one way and then declare it another");
		}
	}

	[Fact]
	public void ReportTheDispositionItWasConstructedWith()
	{
		// Liveness beside the arm above: a get-only property that always answered Handled would satisfy
		// "no setter" and say nothing true.
		MessageResult.Success().Disposition.ShouldBe(MessageDisposition.Handled);
		MessageResult.SuccessFromCache().Disposition.ShouldBe(MessageDisposition.ServedFromCache);
		MessageResult.SuccessFromCache(42).Disposition.ShouldBe(MessageDisposition.ServedFromCache);
		MessageResult.Success(
				routingDecision: null,
				validationResult: null,
				authorizationResult: null,
				disposition: MessageDisposition.SuppressedAsDuplicate)
			.Disposition.ShouldBe(
				MessageDisposition.SuppressedAsDuplicate,
				"the no-metadata path returns cached singletons; a third disposition must not be flattened "
				+ "onto the nearest one");
	}
}
