// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Validation.Context;
using Excalibur.Dispatch.Options.Validation;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Validation.Context;

/// <summary>
///     Tests for the <see cref="DefaultContextValidator" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DefaultContextValidatorShould
{
	private const string ValidTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new DefaultContextValidator(
				null!,
				Microsoft.Extensions.Options.Options.Create(new ContextValidationOptions())));

	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new DefaultContextValidator(
				NullLogger<DefaultContextValidator>.Instance,
				null!));

	[Fact]
	public void CreateSuccessfully()
	{
		var sut = new DefaultContextValidator(
			NullLogger<DefaultContextValidator>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextValidationOptions()));

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void ImplementIContextValidator()
	{
		var sut = new DefaultContextValidator(
			NullLogger<DefaultContextValidator>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextValidationOptions()));

		sut.ShouldBeAssignableTo<IContextValidator>();
	}

	[Fact]
	public async Task ThrowForNullMessageOnValidate()
	{
		var sut = new DefaultContextValidator(
			NullLogger<DefaultContextValidator>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextValidationOptions()));

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.ValidateAsync(null!, A.Fake<IMessageContext>(), CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForNullContextOnValidate()
	{
		var sut = new DefaultContextValidator(
			NullLogger<DefaultContextValidator>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextValidationOptions()));

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.ValidateAsync(A.Fake<IDispatchMessage>(), null!, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ValidateWithDisabledChecksReturnsValid()
	{
		var sut = CreateSut();
		var context = CreateValidContext();
		var message = new TestMessage();

		var result = await sut.ValidateAsync(message, context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeTrue();
		result.Severity.ShouldBe(ValidationSeverity.Info);
	}

	[Fact]
	public async Task ValidateWithAllChecksEnabledReturnsValidForHealthyContext()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = true,
			ValidateMultiTenancy = true,
			ValidateAuthentication = true,
			ValidateTracing = true,
			ValidateVersioning = true,
			ValidateCollections = true,
			ValidateCorrelationChain = true,
			MaxMessageAge = TimeSpan.FromHours(4),
		};

		var sut = CreateSut(options);
		var context = CreateValidContext();
		context.MessageVersion("1");
		context.VersionMetadata(new MessageVersionMetadata { Version = 1 });
		context.DesiredVersion("1");
		var message = new TenantAwareMessage("tenant-1");

		var result = await sut.ValidateAsync(message, context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeTrue();
		result.MissingFields.ShouldBeEmpty();
		result.CorruptedFields.ShouldBeEmpty();
	}

	[Fact]
	public async Task ValidateAsyncReturnsCriticalWhenCoreFieldsAreMissing()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = ["MessageId", "MessageType", "RouteKey"],
			ValidateRequiredFields = true,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
			ValidateCollections = false,
			ValidateCorrelationChain = false,
			MaxMessageAge = null,
		};

		var sut = CreateSut(options);
		var context = CreateValidContext();
		context.MessageId = null;
		context.MessageType = null;

		var result = await sut.ValidateAsync(new TestMessage(), context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeFalse();
		result.Severity.ShouldBe(ValidationSeverity.Critical);
		result.MissingFields.Count(x => x == "MessageId").ShouldBe(1);
		result.MissingFields.Count(x => x == "MessageType").ShouldBe(1);
		result.MissingFields.ShouldContain("RouteKey");
		result.FailureReason.ShouldContain("Missing required fields");
	}

	[Fact]
	public async Task ValidateAsyncReturnsCriticalWhenMessageIdIsCorrupted()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = true,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
			ValidateCollections = false,
			ValidateCorrelationChain = false,
			MaxMessageAge = null,
		};

		var sut = CreateSut(options);
		var context = CreateValidContext();
		context.MessageId = "x";

		var result = await sut.ValidateAsync(new TestMessage(), context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeFalse();
		result.Severity.ShouldBe(ValidationSeverity.Critical);
		result.CorruptedFields.ShouldContain("MessageId");
		result.Details["MessageId_Value"].ShouldBe("x");
	}

	[Fact]
	public async Task ValidateAsyncFlagsMissingTenantForTenantAwareMessages()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = false,
			ValidateMultiTenancy = true,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
			ValidateCollections = false,
			ValidateCorrelationChain = false,
			MaxMessageAge = null,
		};

		var sut = CreateSut(options);
		var context = CreateValidContext();
		context.TenantId = null;

		var result = await sut.ValidateAsync(new TenantAwareMessage("tenant-1"), context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeFalse();
		result.Severity.ShouldBe(ValidationSeverity.Error);
		result.MissingFields.ShouldContain("TenantId");
	}

	[Fact]
	public async Task ValidateAsyncFlagsAuthenticationAndTracingIssues()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = true,
			ValidateMultiTenancy = false,
			ValidateAuthentication = true,
			ValidateTracing = true,
			ValidateVersioning = false,
			ValidateCollections = false,
			ValidateCorrelationChain = false,
			MaxMessageAge = null,
		};

		var sut = CreateSut(options);
		var context = CreateValidContext();
		context.UserId = new string('u', 257);
		context.TraceParent = ValidTraceParent;
		context.AuthorizationResult(AuthorizationResult.Failed("denied"));
		context.MessageType = null;

		using var activity = new Activity("trace-mismatch").Start();

		var result = await sut.ValidateAsync(new TestMessage(), context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeFalse();
		result.CorruptedFields.ShouldContain("UserId");
		result.Details.ContainsKey("UserId_TooLong").ShouldBeTrue();
		result.Details["Authorization_Failed"].ShouldBe(true);
		result.Details["TraceContext_Mismatch"].ShouldBe(true);
		result.Details.ContainsKey("Activity_Id").ShouldBeTrue();
		result.Details.ContainsKey("Context_TraceParent").ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateAsyncFlagsVersioningAndCorrelationIssues()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = false,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = true,
			ValidateCollections = false,
			ValidateCorrelationChain = true,
			MaxMessageAge = null,
		};

		var sut = CreateSut(options);
		var context = CreateValidContext();
		context.CorrelationId = "a";
		context.CausationId = "b";
		context.MessageVersion("2");
		context.VersionMetadata(new MessageVersionMetadata { Version = 1 });
		context.DesiredVersion("-1");

		var result = await sut.ValidateAsync(new TestMessage(), context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeFalse();
		result.CorruptedFields.ShouldContain("MessageVersion");
		result.CorruptedFields.ShouldContain("DesiredVersion");
		result.CorruptedFields.ShouldContain("CorrelationId");
		result.CorruptedFields.ShouldContain("CausationId");
		result.Details["Version_Mismatch"].ShouldBe(true);
		result.Details["DesiredVersion_Invalid"].ShouldBe("-1");
		result.Details["CorrelationId_Invalid"].ShouldBe("a");
		result.Details["CausationId_Invalid"].ShouldBe("b");
	}

	[Fact]
	public async Task ValidateAsyncFlagsOldAndFutureTimestamps()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = false,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
			ValidateCollections = false,
			ValidateCorrelationChain = false,
			MaxMessageAge = TimeSpan.FromHours(1),
		};

		var sut = CreateSut(options);
		var context = CreateValidContext();
		context.ReceivedTimestampUtc = DateTimeOffset.UtcNow.AddHours(-2);
		context.SentTimestampUtc = DateTimeOffset.UtcNow.AddMinutes(10);

		var result = await sut.ValidateAsync(new TestMessage(), context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeFalse();
		result.CorruptedFields.ShouldContain("MessageAge");
		result.CorruptedFields.ShouldContain("SentTimestampUtc");
		result.Details.ContainsKey("MessageAge_Hours").ShouldBeTrue();
		result.Details["SentTimestamp_Future"].ShouldBe(context.SentTimestampUtc);
	}

	[Fact]
	public async Task ValidateAsyncAppliesFieldValidationRules()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = false,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
			ValidateCollections = false,
			ValidateCorrelationChain = false,
			MaxMessageAge = null,
			FieldValidationRules = new Dictionary<string, FieldValidationRule>(StringComparer.Ordinal)
			{
				["Region"] = new()
				{
					Required = true,
					ExpectedType = typeof(string),
					MinLength = 2,
					MaxLength = 3,
					Pattern = "^[A-Z]{2,3}$",
					CustomValidator = static value => string.Equals(value as string, "USA", StringComparison.Ordinal),
					ErrorMessage = "Region must be USA",
				},
				["Priority"] = new()
				{
					ExpectedType = typeof(int),
				},
			},
		};

		var sut = CreateSut(options);
		var context = CreateValidContext();
		context.Items["Region"] = "west";
		context.Items["Priority"] = "high";

		var result = await sut.ValidateAsync(new TestMessage(), context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeFalse();
		result.CorruptedFields.ShouldContain("Region");
		result.CorruptedFields.ShouldContain("Priority");
		result.Details.ContainsKey("Region_TooLong").ShouldBeTrue();
		result.Details.ContainsKey("Region_PatternMismatch").ShouldBeTrue();
		result.Details["Region_CustomValidation"].ShouldBe("Region must be USA");
		result.Details["Priority_TypeMismatch"].ShouldBe("String");
	}

	[Fact]
	public async Task ValidateAsyncFlagsMissingRequiredCustomField()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = false,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
			ValidateCollections = false,
			ValidateCorrelationChain = false,
			MaxMessageAge = null,
			FieldValidationRules = new Dictionary<string, FieldValidationRule>(StringComparer.Ordinal)
			{
				["Environment"] = new() { Required = true },
			},
		};

		var sut = CreateSut(options);
		var context = CreateValidContext();

		var result = await sut.ValidateAsync(new TestMessage(), context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeFalse();
		result.MissingFields.ShouldContain("Environment");
	}

	[Fact]
	public async Task ValidateAsyncFlagsNullItemsCollectionWhenCollectionValidationEnabled()
	{
		var options = new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = false,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
			ValidateCollections = true,
			ValidateCorrelationChain = false,
			MaxMessageAge = null,
		};

		var sut = CreateSut(options);
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns((IDictionary<string, object>)null!);
		A.CallTo(() => context.Properties).Returns(new Dictionary<string, object?>());
		var message = new TestMessage();

		var result = await sut.ValidateAsync(message, context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeFalse();
		result.CorruptedFields.ShouldContain("Items");
		result.Details["Items_Null"].ShouldBe(true);
	}

	private static DefaultContextValidator CreateSut(ContextValidationOptions? options = null)
	{
		options ??= new ContextValidationOptions
		{
			RequiredFields = [],
			ValidateRequiredFields = false,
			ValidateMultiTenancy = false,
			ValidateAuthentication = false,
			ValidateTracing = false,
			ValidateVersioning = false,
			ValidateCollections = false,
			ValidateCorrelationChain = false,
			MaxMessageAge = null,
		};

		return new DefaultContextValidator(
			NullLogger<DefaultContextValidator>.Instance,
			Microsoft.Extensions.Options.Options.Create(options));
	}

	private static MessageEnvelope CreateValidContext() =>
		new()
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "OrderCreated",
			TenantId = "tenant-1",
			UserId = "user-1",
			CorrelationId = Guid.NewGuid().ToString(),
			CausationId = Guid.NewGuid().ToString(),
			TraceParent = ValidTraceParent,
			ReceivedTimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
			SentTimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
		};

	private sealed record TestMessage : IDispatchMessage;

	private sealed record TenantAwareMessage(string TenantId) : IDispatchMessage, ITenantAware;
}
