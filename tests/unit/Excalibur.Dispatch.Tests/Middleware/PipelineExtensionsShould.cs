// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.Batch;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Middleware.Logging;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Middleware.Timeout;
using Excalibur.Dispatch.Middleware.Transaction;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Middleware.Versioning;
using Excalibur.Dispatch.Performance;
using Excalibur.Dispatch.Threading;
using Excalibur.Dispatch.ZeroAlloc;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Unit tests for pipeline Use*() extension methods:
/// <see cref="ValidationPipelineExtensions"/>,
/// <see cref="AuthenticationPipelineExtensions"/>,
/// <see cref="AuthorizationPipelineExtensions"/>,
/// <see cref="CircuitBreakerPipelineExtensions"/>,
/// <see cref="RetryPipelineExtensions"/>,
/// <see cref="TimeoutPipelineExtensions"/>,
/// <see cref="RateLimitingPipelineExtensions"/>,
/// <see cref="TransactionPipelineExtensions"/>,
/// <see cref="OutboxPipelineExtensions"/>,
/// <see cref="InboxPipelineExtensions"/>,
/// <see cref="CloudEventsPipelineExtensions"/>,
/// <see cref="TenantIdentityPipelineExtensions"/>,
/// <see cref="InputSanitizationPipelineExtensions"/>,
/// <see cref="PerformancePipelineExtensions"/>,
/// <see cref="BackgroundExecutionPipelineExtensions"/>,
/// <see cref="BatchingPipelineExtensions"/>,
/// <see cref="ContractVersioningPipelineExtensions"/>,
/// <see cref="AuditLoggingPipelineExtensions"/>,
/// <see cref="ZeroAllocPipelineExtensions"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PipelineExtensionsShould : IDisposable
{
	private readonly ServiceCollection _services = new();
	private DispatchBuilder? _builder;

	public void Dispose()
	{
		_builder?.Dispose();
	}

	private DispatchBuilder CreateBuilder()
	{
		_builder = new DispatchBuilder(_services);
		return _builder;
	}

	#region UseValidation Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseValidation()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseValidation());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseValidation()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseValidation();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterValidationMiddleware_WhenUseValidationCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseValidation();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(ValidationMiddleware));
	}

	#endregion

	#region UseAuthentication Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseAuthentication()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseAuthentication());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseAuthentication()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseAuthentication();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterAuthenticationMiddleware_WhenUseAuthenticationCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseAuthentication();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(AuthenticationMiddleware));
	}

	#endregion

	#region UseAuthorization Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseAuthorization()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseAuthorization());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseAuthorization()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseAuthorization();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterAuthorizationMiddleware_WhenUseAuthorizationCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseAuthorization();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(AuthorizationMiddleware));
	}

	#endregion

	#region UseCircuitBreaker Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCircuitBreaker()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseCircuitBreaker());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCircuitBreaker()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseCircuitBreaker();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterCircuitBreakerMiddleware_WhenUseCircuitBreakerCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseCircuitBreaker();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(CircuitBreakerMiddleware));
	}

	#endregion

	#region UseRetry Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseRetry()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseRetry());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseRetry()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseRetry();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterRetryMiddleware_WhenUseRetryCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseRetry();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(RetryMiddleware));
	}

	#endregion

	#region UseTimeout Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseTimeout()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseTimeout());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseTimeout()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseTimeout();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterTimeoutMiddleware_WhenUseTimeoutCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseTimeout();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(TimeoutMiddleware));
	}

	#endregion

	#region UseThrottling Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseThrottling()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseThrottling());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseThrottling()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseThrottling();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterThrottlingMiddleware_WhenUseThrottlingCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseThrottling();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(ThrottlingMiddleware));
	}

	#endregion

	#region UseTransaction Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseTransaction()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseTransaction());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseTransaction()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseTransaction();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterTransactionMiddleware_WhenUseTransactionCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseTransaction();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(TransactionMiddleware));
	}

	#endregion

	#region UseOutbox Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseOutbox()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseOutbox());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseOutbox()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseOutbox();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterOutboxMiddleware_WhenUseOutboxCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseOutbox();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(OutboxMiddleware));
	}

	#endregion

	#region UseInbox Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseInbox()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseInbox());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseInbox()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseInbox();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterInboxMiddleware_WhenUseInboxCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseInbox();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(InboxMiddleware));
	}

	#endregion

	#region UseIdempotency Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseIdempotency()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseIdempotency());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseIdempotency()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseIdempotency();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterInboxMiddleware_WhenUseIdempotencyCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseIdempotency();

		// Assert -- UseIdempotency is an alias for UseInbox, both register InboxMiddleware
		_services.ShouldContain(sd => sd.ServiceType == typeof(InboxMiddleware));
	}

	#endregion

	#region UseCloudEvents Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCloudEvents()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseCloudEvents());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCloudEvents()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseCloudEvents();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterCloudEventMiddleware_WhenUseCloudEventsCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseCloudEvents();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(CloudEventMiddleware));
	}

	#endregion

	#region UseTenantIdentity Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseTenantIdentity()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseTenantIdentity());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseTenantIdentity()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseTenantIdentity();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterTenantIdentityMiddleware_WhenUseTenantIdentityCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseTenantIdentity();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(TenantIdentityMiddleware));
	}

	#endregion

	#region UseInputSanitization Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseInputSanitization()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseInputSanitization());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseInputSanitization()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseInputSanitization();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterInputSanitizationMiddleware_WhenUseInputSanitizationCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseInputSanitization();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(InputSanitizationMiddleware));
	}

	#endregion

	#region UsePerformance Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUsePerformance()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UsePerformance());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UsePerformance()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UsePerformance();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterPerformanceMiddleware_WhenUsePerformanceCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UsePerformance();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(PerformanceMiddleware));
	}

	#endregion

	#region UseBackgroundExecution Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseBackgroundExecution()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseBackgroundExecution());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseBackgroundExecution()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseBackgroundExecution();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterBackgroundExecutionMiddleware_WhenUseBackgroundExecutionCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseBackgroundExecution();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(BackgroundExecutionMiddleware));
	}

	#endregion

	#region UseBatching Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseBatching()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseBatching());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseBatching()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseBatching();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterBatchingMiddleware_WhenUseBatchingCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseBatching();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(UnifiedBatchingMiddleware));
	}

	#endregion

	#region UseContractVersioning Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseContractVersioning()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseContractVersioning());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseContractVersioning()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseContractVersioning();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterContractVersionCheckMiddleware_WhenUseContractVersioningCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseContractVersioning();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(ContractVersionCheckMiddleware));
	}

	#endregion

	#region UseAuditLogging Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseAuditLogging()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseAuditLogging());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseAuditLogging()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseAuditLogging();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterAuditLoggingMiddleware_WhenUseAuditLoggingCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseAuditLogging();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(AuditLoggingMiddleware));
	}

	#endregion

	#region UseZeroAllocMiddleware Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseZeroAllocMiddleware()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseZeroAllocMiddleware());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseZeroAllocMiddleware()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseZeroAllocMiddleware();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterZeroAllocationValidationMiddleware_WhenUseZeroAllocMiddlewareCalled()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.UseZeroAllocMiddleware();

		// Assert
		_services.ShouldContain(sd => sd.ServiceType == typeof(ZeroAllocationValidationMiddleware));
	}

	#endregion

	#region UseCloudEventValidation Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCloudEventValidation()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseCloudEventValidation((ce, ct) => Task.FromResult(true)));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenValidatorIsNull_ForUseCloudEventValidation()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCloudEventValidation(null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCloudEventValidation()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseCloudEventValidation((ce, ct) => Task.FromResult(true));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseCloudEventBatching Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCloudEventBatching()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseCloudEventBatching());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCloudEventBatching()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseCloudEventBatching();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseCloudEventTransformation Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCloudEventTransformation()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IDispatchBuilder)null!).UseCloudEventTransformation((ce, evt, ctx, ct) => Task.CompletedTask));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenTransformerIsNull_ForUseCloudEventTransformation()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCloudEventTransformation(null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCloudEventTransformation()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseCloudEventTransformation((ce, evt, ctx, ct) => Task.CompletedTask);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFluentChaining_AcrossAllPipelineExtensions()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act -- verify all 20 extensions chain fluently (Wave 1-4)
		var result = builder
			.UsePerformance()
			.UseCloudEvents()
			.UseAuthentication()
			.UseTenantIdentity()
			.UseAuthorization()
			.UseInputSanitization()
			.UseValidation()
			.UseContractVersioning()
			.UseThrottling()
			.UseInbox()
			.UseCircuitBreaker()
			.UseRetry()
			.UseTimeout()
			.UseAuditLogging()
			.UseBatching()
			.UseBackgroundExecution()
			.UseZeroAllocMiddleware()
			.UseTransaction()
			.UseOutbox()
			.UseIdempotency();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterAllMiddleware_WhenFullPipelineConfigured()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder
			.UsePerformance()
			.UseCloudEvents()
			.UseAuthentication()
			.UseTenantIdentity()
			.UseAuthorization()
			.UseInputSanitization()
			.UseValidation()
			.UseContractVersioning()
			.UseThrottling()
			.UseInbox()
			.UseCircuitBreaker()
			.UseRetry()
			.UseTimeout()
			.UseAuditLogging()
			.UseBatching()
			.UseBackgroundExecution()
			.UseZeroAllocMiddleware()
			.UseTransaction()
			.UseOutbox();

		// Assert -- Wave 1+2 middleware
		_services.ShouldContain(sd => sd.ServiceType == typeof(ValidationMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(AuthenticationMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(AuthorizationMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(ThrottlingMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(CircuitBreakerMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(RetryMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(TimeoutMiddleware));

		// Assert -- Wave 3 middleware
		_services.ShouldContain(sd => sd.ServiceType == typeof(TransactionMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(OutboxMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(InboxMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(CloudEventMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(TenantIdentityMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(InputSanitizationMiddleware));

		// Assert -- Wave 4 middleware
		_services.ShouldContain(sd => sd.ServiceType == typeof(PerformanceMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(BackgroundExecutionMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(UnifiedBatchingMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(ContractVersionCheckMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(AuditLoggingMiddleware));
		_services.ShouldContain(sd => sd.ServiceType == typeof(ZeroAllocationValidationMiddleware));
	}

	#endregion
}
