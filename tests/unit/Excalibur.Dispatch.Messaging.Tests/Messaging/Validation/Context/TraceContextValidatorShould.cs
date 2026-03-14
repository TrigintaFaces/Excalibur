// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Validation.Context;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Validation.Context;

/// <summary>
///     Tests for the <see cref="TraceContextValidator" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TraceContextValidatorShould
{
	private readonly TraceContextValidator _sut = new(NullLogger<TraceContextValidator>.Instance);

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() => new TraceContextValidator(null!));

	[Fact]
	public void CreateSuccessfully()
	{
		_sut.ShouldNotBeNull();
	}

	[Fact]
	public void ImplementIContextValidator()
	{
		_sut.ShouldBeAssignableTo<IContextValidator>();
	}

	[Fact]
	public async Task ThrowForNullMessage()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ValidateAsync(null!, A.Fake<IMessageContext>(), CancellationToken.None).AsTask()).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForNullContext()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ValidateAsync(A.Fake<IDispatchMessage>(), null!, CancellationToken.None).AsTask()).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReturnSuccessWhenNoTraceContext()
	{
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>();
		var features = new Dictionary<Type, object>();
		A.CallTo(() => context.Items).Returns(items);
		A.CallTo(() => context.Features).Returns(features);
		A.CallTo(() => context.MessageId).Returns("msg-1");

		var result = await _sut.ValidateAsync(message, context, CancellationToken.None).ConfigureAwait(false);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task DetectOrphanedTraceContext()
	{
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var items2 = new Dictionary<string, object>();
		var features2 = new Dictionary<Type, object>();
		A.CallTo(() => context.Items).Returns(items2);
		A.CallTo(() => context.Features).Returns(features2);
		context.GetOrCreateIdentityFeature().TraceParent = "00-traceid-spanid-01";
		A.CallTo(() => context.MessageId).Returns(null);

		var result = await _sut.ValidateAsync(message, context, CancellationToken.None).ConfigureAwait(false);

		// Should detect orphaned trace (TraceParent but no MessageId)
		result.IsValid.ShouldBeFalse();
	}
}
