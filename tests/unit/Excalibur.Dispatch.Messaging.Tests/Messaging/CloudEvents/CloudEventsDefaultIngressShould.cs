// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Tests.Shared.TestFakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.CloudEvents;

/// <summary>
/// Independent lock (author≠impl) for the CloudEvents default-ingress contract: the out-of-the-box
/// pipeline must WORK, not throw on every incoming CloudEvent. Schema validation is opt-in (matching
/// Microsoft's OutputCache/HybridCache, which never validate by default), and enabling it without a
/// registered validator must fail fast at STARTUP, not on the first message.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
public sealed class CloudEventsDefaultIngressShould
{
	/// <summary>
	/// The default <c>AddCloudEvents()</c> wiring must leave schema validation OFF (opt-in). RED on the
	/// pre-fix opt-out default (<c>ValidateSchema = true</c>), which made the default ingress path throw.
	/// </summary>
	[Fact]
	public void LeaveSchemaValidationOff_ByDefault()
	{
		var services = new ServiceCollection();
		services.AddDispatch(builder => builder.AddCloudEvents());
		using var provider = services.BuildServiceProvider();

		var options = provider.GetRequiredService<IOptions<CloudEventOptions>>().Value;

		options.Schema.ValidateSchema.ShouldBeFalse(
			"CloudEvents schema validation must be opt-in — the default ingress must work without a schema registry");
	}

	/// <summary>
	/// The ingress middleware built with the DEFAULT options must process a valid incoming CloudEvent
	/// WITHOUT throwing. RED on the pre-fix default (<c>ValidateSchema = true</c> + no registry →
	/// <see cref="InvalidOperationException"/> on every message).
	/// </summary>
	[Fact]
	public async Task ProcessValidIncomingCloudEvent_WithoutThrowing_OnDefaultOptions()
	{
		// DEFAULT options — deliberately NOT setting ValidateSchema; this is the baarat contract.
		var middleware = new CloudEventMiddleware(
			NullLoggerFactory.Instance.CreateLogger<CloudEventMiddleware>(),
			MsOptions.Create(new CloudEventOptions()),
			A.Fake<IEnvelopeCloudEventBridge>(),
			schemaRegistry: null);

		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext { MessageId = "baarat-1" };
		context.Items["cloudevent"] = new CloudEvent
		{
			Id = "ce-baarat",
			Source = new Uri("urn:test-source"),
			Type = "test.event.v1",
			Time = DateTimeOffset.UtcNow,
		};

		var result = await middleware.InvokeAsync(
			message, context, (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None);

		result.IsSuccess.ShouldBeTrue("the default ingress must not fail-closed when no schema validation is configured");
		context.MessageId.ShouldBe("ce-baarat");
	}

	/// <summary>
	/// Enabling <c>ValidateSchema</c> while no working schema-payload validation path exists must fail fast
	/// at STARTUP (options validation), not throw on the first message. RED on the pre-fix validator, which
	/// had no such clause and only threw at message time.
	/// </summary>
	[Fact]
	public void FailFastAtStartup_WhenSchemaValidationEnabled_ButUnsupported()
	{
		var services = new ServiceCollection();
		services.AddDispatch(builder => builder.AddCloudEvents(o => o.Schema.ValidateSchema = true));
		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<CloudEventOptions>>().Value);
		ex.Message.ShouldContain(nameof(CloudEventSchemaOptions.ValidateSchema));
	}

	/// <summary>
	/// Registering a schema registry must NOT force <c>ValidateSchema=true</c> — that would trip the
	/// startup guard and make merely adding a registry throw at startup. A registry stamps schema-version
	/// only (validation stays opt-in). RED on the half-fix that left <c>ValidateSchema = true</c> in
	/// <c>AddCloudEventSchemaRegistry</c>.
	/// </summary>
	[Fact]
	public void NotForceSchemaValidation_WhenRegisteringASchemaRegistry()
	{
		var services = new ServiceCollection();
		services.AddDispatch(builder => builder.AddCloudEvents().AddInMemorySchemaRegistry());
		using var provider = services.BuildServiceProvider();

		// Must not throw at startup (the guard would reject ValidateSchema=true).
		var options = provider.GetRequiredService<IOptions<CloudEventOptions>>().Value;

		options.Schema.ValidateSchema.ShouldBeFalse(
			"registering a schema registry must not auto-enable schema-payload validation (no working path yet)");
		options.Schema.IncludeSchemaVersion.ShouldBeTrue(
			"a registry still enables schema-version stamping, which works");
	}
}
