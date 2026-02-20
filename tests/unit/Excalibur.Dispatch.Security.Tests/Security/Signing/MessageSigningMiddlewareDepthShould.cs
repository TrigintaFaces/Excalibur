// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Deep coverage tests for <see cref="MessageSigningMiddleware"/> covering incoming/outgoing
/// direction detection, tenant algorithm overrides, key ID extraction, purpose extraction,
/// missing signature handling, and signing exception paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class MessageSigningMiddlewareDepthShould
{
	private readonly IMessageSigningService _signingService;
	private readonly IDispatchMessage _message;
	private readonly IMessageContext _context;
	private readonly IMessageResult _successResult;
	private readonly IMessageResult _failedResult;
	private readonly Dictionary<string, object> _contextItems;
	private readonly Dictionary<string, object?> _contextProperties;

	public MessageSigningMiddlewareDepthShould()
	{
		_signingService = A.Fake<IMessageSigningService>();
		_message = A.Fake<IDispatchMessage>();
		_context = A.Fake<IMessageContext>();
		_successResult = A.Fake<IMessageResult>();
		_failedResult = A.Fake<IMessageResult>();
		_contextItems = new Dictionary<string, object>(StringComparer.Ordinal);
		_contextProperties = new Dictionary<string, object?>(StringComparer.Ordinal);

		A.CallTo(() => _successResult.Succeeded).Returns(true);
		A.CallTo(() => _failedResult.Succeeded).Returns(false);
		A.CallTo(() => _context.Items).Returns(_contextItems);
		A.CallTo(() => _context.Properties).Returns(_contextProperties);
	}

	[Fact]
	public async Task AllowUnsignedIncomingMessage_WhenSignatureNotRequired()
	{
		// Arrange — incoming + no signature + RequireValidSignature=false
		var options = new SigningOptions { Enabled = true, RequireValidSignature = false };
		var sut = CreateMiddleware(options);

		_contextItems["MessageDirection"] = "Incoming";
		// No "MessageSignature" in items

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — unsigned messages are allowed
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => _signingService.VerifySignatureAsync(
			A<string>._, A<string>._, A<SigningContext>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task RejectUnsignedIncomingMessage_WhenSignatureRequired()
	{
		// Arrange — incoming + no signature + RequireValidSignature=true
		var options = new SigningOptions { Enabled = true, RequireValidSignature = true };
		var sut = CreateMiddleware(options);

		_contextItems["MessageDirection"] = "Incoming";
		// No "MessageSignature" in items

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — should fail because no signature was found
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task UseDefaultAlgorithm_ForOutgoingMessage()
	{
		// Arrange
		var options = new SigningOptions
		{
			Enabled = true,
			DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
		};
		var sut = CreateMiddleware(options);

		SigningContext? capturedContext = null;
		A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
			.Invokes((string _, SigningContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(Task.FromResult("sig"));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		capturedContext.ShouldNotBeNull();
		capturedContext!.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
	}

	[Fact]
	public async Task ApplyTenantAlgorithm_WhenConfigured()
	{
		// Arrange
		var options = new SigningOptions
		{
			Enabled = true,
			DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
			TenantAlgorithms = { ["tenant-abc"] = SigningAlgorithm.HMACSHA512 },
		};
		var sut = CreateMiddleware(options);

		_contextItems["TenantId"] = "tenant-abc";

		SigningContext? capturedContext = null;
		A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
			.Invokes((string _, SigningContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(Task.FromResult("sig"));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — should override to tenant algorithm
		capturedContext.ShouldNotBeNull();
		capturedContext!.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA512);
		capturedContext.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public async Task ExtractKeyId_FromContext()
	{
		// Arrange
		var options = new SigningOptions
		{
			Enabled = true,
			DefaultKeyId = "default-key",
		};
		var sut = CreateMiddleware(options);

		_contextItems["SigningKeyId"] = "custom-key-123";

		SigningContext? capturedContext = null;
		A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
			.Invokes((string _, SigningContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(Task.FromResult("sig"));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — should use context key ID over default
		capturedContext.ShouldNotBeNull();
		capturedContext!.KeyId.ShouldBe("custom-key-123");
	}

	[Fact]
	public async Task UseDefaultKeyId_WhenNotInContext()
	{
		// Arrange
		var options = new SigningOptions
		{
			Enabled = true,
			DefaultKeyId = "default-key",
		};
		var sut = CreateMiddleware(options);

		SigningContext? capturedContext = null;
		A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
			.Invokes((string _, SigningContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(Task.FromResult("sig"));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		capturedContext.ShouldNotBeNull();
		capturedContext!.KeyId.ShouldBe("default-key");
	}

	[Fact]
	public async Task ExtractPurpose_FromContext()
	{
		// Arrange
		var sut = CreateMiddleware(new SigningOptions { Enabled = true });

		_contextItems["SigningPurpose"] = "data-integrity";

		SigningContext? capturedContext = null;
		A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
			.Invokes((string _, SigningContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(Task.FromResult("sig"));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		capturedContext.ShouldNotBeNull();
		capturedContext!.Purpose.ShouldBe("data-integrity");
	}

	[Fact]
	public async Task SkipSigning_WhenOutgoingResultFailed()
	{
		// Arrange — outgoing message, but next delegate returns failed result
		var sut = CreateMiddleware(new SigningOptions { Enabled = true });

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_failedResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — signing not called because result was not successful
		result.Succeeded.ShouldBeFalse();
		A.CallTo(() => _signingService.SignMessageAsync(
			A<string>._, A<SigningContext>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task VerifyIncoming_WithAlgorithmFromContext()
	{
		// Arrange — incoming + signature + algorithm stored in context
		var options = new SigningOptions
		{
			Enabled = true,
			RequireValidSignature = true,
			DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
		};
		var sut = CreateMiddleware(options);

		_contextItems["MessageDirection"] = "Incoming";
		_contextItems["MessageSignature"] = "sig-value";
		_contextItems["SignatureAlgorithm"] = "HMACSHA512";

		SigningContext? capturedContext = null;
		A.CallTo(() => _signingService.VerifySignatureAsync(A<string>._, A<string>._, A<SigningContext>._, A<CancellationToken>._))
			.Invokes((string _, string _, SigningContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(Task.FromResult(true));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — algorithm from context overrides default
		capturedContext.ShouldNotBeNull();
		capturedContext!.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA512);
	}

	[Fact]
	public async Task HandleSigningException_ReturnFailedResult()
	{
		// Arrange — outgoing, signing throws
		var sut = CreateMiddleware(new SigningOptions { Enabled = true });

		A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
			.Throws(new SigningException("key not found"));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — graceful failure, not exception propagation
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task HandleVerificationException_ReturnFailedResult()
	{
		// Arrange — incoming, verification throws SigningException
		var options = new SigningOptions { Enabled = true, RequireValidSignature = true };
		var sut = CreateMiddleware(options);

		_contextItems["MessageDirection"] = "Incoming";
		_contextItems["MessageSignature"] = "bad-sig";

		A.CallTo(() => _signingService.VerifySignatureAsync(A<string>._, A<string>._, A<SigningContext>._, A<CancellationToken>._))
			.Throws(new SigningException("invalid key"));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task SetIncludeTimestamp_FromOptions()
	{
		// Arrange
		var options = new SigningOptions
		{
			Enabled = true,
			IncludeTimestampByDefault = true,
		};
		var sut = CreateMiddleware(options);

		SigningContext? capturedContext = null;
		A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
			.Invokes((string _, SigningContext ctx, CancellationToken _) => capturedContext = ctx)
			.Returns(Task.FromResult("sig"));

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		capturedContext.ShouldNotBeNull();
		capturedContext!.IncludeTimestamp.ShouldBeTrue();
	}

	private MessageSigningMiddleware CreateMiddleware(SigningOptions? options = null)
	{
		return new MessageSigningMiddleware(
			_signingService,
			MsOptions.Create(options ?? new SigningOptions { Enabled = true }),
			NullLogger<MessageSigningMiddleware>.Instance);
	}
}

#pragma warning restore IL2026, IL3050
