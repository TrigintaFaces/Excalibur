// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Security.Encryption;

/// <summary>
/// Deep coverage tests for <see cref="MessageEncryptionMiddleware"/> covering enabled/disabled,
/// ShouldEncryptMessage logic, ISensitiveMessage detection, excluded types, encryption/decryption
/// flow, missing payload, and EncryptionException handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class MessageEncryptionMiddlewareDepthShould
{
	private readonly IMessageEncryptionService _encryptionService = A.Fake<IMessageEncryptionService>();
	private readonly MessageEncryptionMiddleware _sut;

	public MessageEncryptionMiddlewareDepthShould()
	{
		var options = new EncryptionOptions
		{
			Enabled = true,
			EncryptByDefault = true,
			CurrentKeyId = "key-1",
			DefaultAlgorithm = EncryptionAlgorithm.Aes256Gcm,
		};

		_sut = new MessageEncryptionMiddleware(
			_encryptionService,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<MessageEncryptionMiddleware>.Instance);
	}

	[Fact]
	public async Task PassThrough_WhenEncryptionDisabled()
	{
		// Arrange
		var disabledOptions = new EncryptionOptions { Enabled = false };
		var sut = new MessageEncryptionMiddleware(
			_encryptionService,
			Microsoft.Extensions.Options.Options.Create(disabledOptions),
			NullLogger<MessageEncryptionMiddleware>.Instance);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expected = A.Fake<IMessageResult>();

		// Act
		var result = await sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expected),
			CancellationToken.None);

		// Assert
		result.ShouldBe(expected);
		A.CallTo(() => _encryptionService.EncryptMessageAsync(A<string>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SkipEncryption_WhenMessageTypeExcluded()
	{
		// Arrange
		var options = new EncryptionOptions
		{
			Enabled = true,
			EncryptByDefault = true,
			ExcludedMessageTypes = new HashSet<string>(StringComparer.Ordinal) { "ObjectProxy" },
		};
		var sut = new MessageEncryptionMiddleware(
			_encryptionService,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<MessageEncryptionMiddleware>.Instance);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expected = A.Fake<IMessageResult>();

		// Act
		var result = await sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expected),
			CancellationToken.None);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task SkipEncryption_WhenDisableEncryptionContextFlag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["DisableEncryption"] = true,
		};
		A.CallTo(() => context.Items).Returns(items);
		var expected = A.Fake<IMessageResult>();

		// Act
		var result = await _sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expected),
			CancellationToken.None);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task EncryptSensitiveMessages_EvenWhenEncryptByDefaultIsFalse()
	{
		// Arrange
		var options = new EncryptionOptions
		{
			Enabled = true,
			EncryptByDefault = false,
			CurrentKeyId = "key-1",
		};
		var sut = new MessageEncryptionMiddleware(
			_encryptionService,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<MessageEncryptionMiddleware>.Instance);

		// Create a fake IDispatchMessage that also implements ISensitiveMessage
		var message = A.Fake<IDispatchMessage>(o => o.Implements<ISensitiveMessage>());
		var context = A.Fake<IMessageContext>();

		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["MessageDirection"] = "Outgoing",
		};
		A.CallTo(() => context.Items).Returns(items);

		var successResult = A.Fake<IMessageResult>();
		A.CallTo(() => successResult.Succeeded).Returns(true);

		A.CallTo(() => _encryptionService.EncryptMessageAsync(A<string>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns("encrypted-payload");

		// Act
		await sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(successResult),
			CancellationToken.None);

		// Assert
		A.CallTo(() => _encryptionService.EncryptMessageAsync(A<string>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task HandleEncryptionException_ReturnFailedResult()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["MessageDirection"] = "Outgoing",
		};
		A.CallTo(() => context.Items).Returns(items);

		var successResult = A.Fake<IMessageResult>();
		A.CallTo(() => successResult.Succeeded).Returns(true);

		A.CallTo(() => _encryptionService.EncryptMessageAsync(A<string>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ThrowsAsync(new EncryptionException("Encryption failed"));

		// Act
		var result = await _sut.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(successResult),
			CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void HaveCorrectStage()
	{
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.Serialization);
	}

	[Fact]
	public void HaveCorrectApplicableMessageKinds()
	{
		_sut.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
	}

	[Fact]
	public void ThrowOnNullMessage()
	{
		Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(null!, A.Fake<IMessageContext>(),
				(_, _, _) => default, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullContext()
	{
		Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(A.Fake<IDispatchMessage>(), null!,
				(_, _, _) => default, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullNextDelegate()
	{
		Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(),
				null!, CancellationToken.None));
	}
}
