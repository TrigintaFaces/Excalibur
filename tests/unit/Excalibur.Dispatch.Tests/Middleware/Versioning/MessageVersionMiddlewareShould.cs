// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Versioning;
using Excalibur.Dispatch.Middleware.Versioning;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Middleware.Versioning;

/// <summary>
/// Unit tests for <see cref="MessageVersionMiddleware"/>.
/// </summary>
/// <remarks>
/// Sprint 698 T.2 (m5e9u): Tests for the internal middleware that handles message schema versioning
/// by inspecting transport headers and delegating to registered <see cref="IMessageVersionMapper"/> implementations.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class MessageVersionMiddlewareShould : UnitTestBase
{
	private readonly IDispatchMessage _message;
	private readonly TestMessageContext _context;
	private bool _nextInvoked;

	public MessageVersionMiddlewareShould()
	{
		_message = A.Fake<IDispatchMessage>();
		_context = new TestMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "TestMessage",
		};
		_nextInvoked = false;
	}

	private ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
	{
		_nextInvoked = true;
		return new ValueTask<IMessageResult>(MessageResult.Success());
	}

	private static MessageVersionMiddleware CreateMiddleware(params IMessageVersionMapper[] mappers)
	{
		return new MessageVersionMiddleware(
			mappers,
			NullLogger<MessageVersionMiddleware>.Instance);
	}

	#region Stage Tests

	[Fact]
	public void HaveSerializationStage()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Serialization);
	}

	#endregion

	#region Pass-Through Tests

	[Fact]
	public async Task PassThroughWhenNoVersionHeader()
	{
		// Arrange - no version headers set on context
		var middleware = CreateMiddleware();

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_nextInvoked.ShouldBeTrue();
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task PassThroughWhenVersionHeaderButNoExpectedVersion()
	{
		// Arrange
		_context.Items[MessageVersionMiddleware.VersionHeaderKey] = 1;
		var middleware = CreateMiddleware();

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task PassThroughWhenVersionsMatch()
	{
		// Arrange
		_context.Items[MessageVersionMiddleware.VersionHeaderKey] = 2;
		_context.Items[MessageVersionMiddleware.ExpectedVersionKey] = 2;
		var middleware = CreateMiddleware();

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task PassThroughWhenVersionsMismatchButNoMapper()
	{
		// Arrange
		_context.Items[MessageVersionMiddleware.VersionHeaderKey] = 1;
		_context.Items[MessageVersionMiddleware.ExpectedVersionKey] = 2;
		var middleware = CreateMiddleware(); // no mappers

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_nextInvoked.ShouldBeTrue();
	}

	#endregion

	#region Mapper Invocation Tests

	[Fact]
	public async Task InvokeMapperCanMapWhenVersionsMismatch()
	{
		// Arrange
		_context.Items[MessageVersionMiddleware.VersionHeaderKey] = 1;
		_context.Items[MessageVersionMiddleware.ExpectedVersionKey] = 2;

		var mapper = A.Fake<IMessageVersionMapper>();
		A.CallTo(() => mapper.CanMap(A<string>._, 1, 2)).Returns(true);

		var middleware = CreateMiddleware(mapper);

		// Act
		await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		A.CallTo(() => mapper.CanMap(A<string>._, 1, 2)).MustHaveHappenedOnceExactly();
		_nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task SkipMapperThatCannotMap()
	{
		// Arrange
		_context.Items[MessageVersionMiddleware.VersionHeaderKey] = 1;
		_context.Items[MessageVersionMiddleware.ExpectedVersionKey] = 3;

		var mapper1 = A.Fake<IMessageVersionMapper>();
		A.CallTo(() => mapper1.CanMap(A<string>._, 1, 3)).Returns(false);

		var mapper2 = A.Fake<IMessageVersionMapper>();
		A.CallTo(() => mapper2.CanMap(A<string>._, 1, 3)).Returns(true);

		var middleware = CreateMiddleware(mapper1, mapper2);

		// Act
		await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		A.CallTo(() => mapper1.CanMap(A<string>._, 1, 3)).MustHaveHappenedOnceExactly();
		A.CallTo(() => mapper2.CanMap(A<string>._, 1, 3)).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region String Version Parsing Tests

	[Fact]
	public async Task ParseStringVersionHeaders()
	{
		// Arrange - versions as strings instead of ints
		_context.Items[MessageVersionMiddleware.VersionHeaderKey] = "1";
		_context.Items[MessageVersionMiddleware.ExpectedVersionKey] = "2";

		var mapper = A.Fake<IMessageVersionMapper>();
		A.CallTo(() => mapper.CanMap(A<string>._, 1, 2)).Returns(true);

		var middleware = CreateMiddleware(mapper);

		// Act
		await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		A.CallTo(() => mapper.CanMap(A<string>._, 1, 2)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassThroughWhenVersionHeaderIsNonNumericString()
	{
		// Arrange
		_context.Items[MessageVersionMiddleware.VersionHeaderKey] = "not-a-number";
		_context.Items[MessageVersionMiddleware.ExpectedVersionKey] = 2;
		var middleware = CreateMiddleware();

		// Act
		var result = await middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_nextInvoked.ShouldBeTrue();
	}

	#endregion

	#region Null Guard Tests

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			middleware.InvokeAsync(null!, _context, Next, CancellationToken.None).AsTask())
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			middleware.InvokeAsync(_message, null!, Next, CancellationToken.None).AsTask())
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowOnNullDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			middleware.InvokeAsync(_message, _context, null!, CancellationToken.None).AsTask())
			.ConfigureAwait(false);
	}

	#endregion

	#region Constructor Guard Tests

	[Fact]
	public void ThrowOnNullMappers()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MessageVersionMiddleware(null!, NullLogger<MessageVersionMiddleware>.Instance));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MessageVersionMiddleware(Array.Empty<IMessageVersionMapper>(), null!));
	}

	#endregion

	#region Constants Tests

	[Fact]
	public void HaveCorrectVersionHeaderKey()
	{
		MessageVersionMiddleware.VersionHeaderKey.ShouldBe("x-message-version");
	}

	[Fact]
	public void HaveCorrectExpectedVersionKey()
	{
		MessageVersionMiddleware.ExpectedVersionKey.ShouldBe("x-expected-message-version");
	}

	#endregion
}
