// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Middleware registered through <c>UseAt</c> or <c>ForMessageKinds().Use</c> is wrapped in a decorator, so
/// its runtime type is the decorator's, not its own. Anything that groups, counts or compares middleware by
/// type must reach the decorated middleware, or two unrelated registrations look like one type and one
/// registration looks like two.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class PipelineChecksUnwrapDecoratorsShould
{
	private const DispatchMiddlewareStage SharedStage = DispatchMiddlewareStage.PreProcessing;

	[Fact]
	public async Task NotReportADuplicateForTwoDistinctMiddlewaresSharingADecoratorAndStage()
	{
		var description = await DescribeHealth(
			Decorate(new FirstMiddleware()),
			Decorate(new SecondMiddleware()));

		description.ShouldNotContain("Duplicate middleware");
	}

	/// <summary>
	/// Liveness arm: unwrapping must not disable duplicate detection. A middleware genuinely registered
	/// twice at one stage is still a duplicate, decorated or not.
	/// </summary>
	[Fact]
	public async Task StillReportADuplicateWhenOneMiddlewareIsRegisteredTwiceAtAStage()
	{
		var description = await DescribeHealth(
			Decorate(new FirstMiddleware()),
			Decorate(new FirstMiddleware()));

		description.ShouldContain("Duplicate middleware");
		description.ShouldContain(nameof(FirstMiddleware));
	}

	/// <summary>
	/// The inverse of the reported defect: while the decorator hid two middlewares behind one type, the
	/// stage-conflict check could not see that the stage held two different middlewares at all.
	/// </summary>
	[Fact]
	public async Task ReportAStageConflictBetweenTwoDistinctMiddlewaresSharingADecorator()
	{
		var description = await DescribeHealth(
			Decorate(new FirstMiddleware()),
			Decorate(new SecondMiddleware()));

		description.ShouldContain("Multiple middleware at stage");
		description.ShouldContain(nameof(FirstMiddleware));
		description.ShouldContain(nameof(SecondMiddleware));
	}

	/// <summary>
	/// The startup guard fails closed when a profile declares authorization and none is resolvable.
	/// A decorated <see cref="AuthorizationMiddleware" /> IS resolvable, so the guard must not fire.
	/// </summary>
	[Fact]
	public async Task TreatADecoratedAuthorizationMiddlewareAsPresent()
	{
		var service = CreateValidationService(
			profileDeclaresAuthorization: true,
			middlewares: [Decorate(CreateAuthorizationMiddleware())]);

		await Should.NotThrowAsync(() => service.StartAsync(CancellationToken.None));
	}

	/// <summary>
	/// Liveness arm for the guard: unwrapping must not make the guard find authorization that is absent.
	/// </summary>
	[Fact]
	public async Task StillFailClosedWhenOnlyANonAuthorizationMiddlewareIsDecorated()
	{
		var service = CreateValidationService(
			profileDeclaresAuthorization: true,
			middlewares: [Decorate(new FirstMiddleware())]);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => service.StartAsync(CancellationToken.None));
	}

	/// <summary>
	/// Nested decorators occur when middleware is both kind-filtered and stage-overridden.
	/// </summary>
	[Fact]
	public void UnwrapThroughNestedDecorators()
	{
		var inner = new FirstMiddleware();
		var nested = new StageOverrideMiddleware(
			new MessageKindFilteringMiddleware(inner, MessageKinds.Action),
			SharedStage);

		nested.Unwrap().ShouldBeSameAs(inner);
		nested.UnwrappedType().ShouldBe(typeof(FirstMiddleware));
	}

	[Fact]
	public void ReturnAnUndecoratedMiddlewareUnchanged()
	{
		var plain = new FirstMiddleware();

		plain.Unwrap().ShouldBeSameAs(plain);
	}

	private static IDispatchMiddleware Decorate(IDispatchMiddleware inner) =>
		new StageOverrideMiddleware(inner, SharedStage);

	private static async Task<string> DescribeHealth(params IDispatchMiddleware[] middlewares)
	{
		var services = new ServiceCollection();
		foreach (var middleware in middlewares)
		{
			_ = services.AddSingleton(middleware);
		}

		var check = new PipelineIntegrityHealthCheck(services.BuildServiceProvider());
		var result = await check
			.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(true);

		return result.Description ?? string.Empty;
	}

	private static PipelineValidationHostedService CreateValidationService(
		bool profileDeclaresAuthorization,
		IReadOnlyList<IDispatchMiddleware> middlewares)
	{
		var profile = A.Fake<IPipelineProfile>();
		IReadOnlyList<MiddlewareEntry> declaredEntries = profileDeclaresAuthorization
			? [new MiddlewareEntry(typeof(AuthorizationMiddleware), MiddlewareCriticality.Required)]
			: [];
		_ = A.CallTo(() => profile.MiddlewareEntries).Returns(declaredEntries);

		var registry = A.Fake<IPipelineProfileRegistry>();
		_ = A.CallTo(() => registry.GetProfileNames()).Returns(["Strict"]);
		_ = A.CallTo(() => registry.GetProfile("Strict")).Returns(profile);
		_ = A.CallTo(() => registry.GetDefaultProfileName()).Returns("Strict");

		var services = new ServiceCollection();
		foreach (var middleware in middlewares)
		{
			_ = services.AddSingleton(middleware);
		}

		_ = services.AddSingleton(registry);

		return new PipelineValidationHostedService(
			services.BuildServiceProvider(), NullLogger<PipelineValidationHostedService>.Instance);
	}

	private static AuthorizationMiddleware CreateAuthorizationMiddleware() =>
		new(
			MsOptions.Create(new AuthorizationOptions()),
			A.Fake<IAuthorizationService>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<AuthorizationMiddleware>.Instance);

	private sealed class FirstMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => SharedStage;

		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) => nextDelegate(message, context, cancellationToken);
	}

	private sealed class SecondMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => SharedStage;

		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) => nextDelegate(message, context, cancellationToken);
	}
}
