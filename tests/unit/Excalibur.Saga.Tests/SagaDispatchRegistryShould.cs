// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga;

namespace Excalibur.Saga.Tests;

/// <summary>
/// Tests for <see cref="SagaDispatchRegistry"/> covering registration, retrieval,
/// overwrite behavior, and null/edge cases.
/// Sprint 738 B.2: AOT Wave 4 -- ISagaDispatchRegistry.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Feature", "AOT")]
public sealed class SagaDispatchRegistryShould
{
	private readonly SagaDispatchRegistry _sut = new();

	private static readonly Func<object, IMessageContext, ISagaEvent, SagaInfo, CancellationToken, Task> TestDispatcher =
		(_, _, _, _, _) => Task.CompletedTask;

	[Fact]
	public void ReturnNullForUnregisteredType()
	{
		var result = _sut.GetDispatcher(typeof(string), typeof(int));

		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnRegisteredDispatcher()
	{
		_sut.Register(typeof(string), typeof(int), TestDispatcher);

		var result = _sut.GetDispatcher(typeof(string), typeof(int));

		result.ShouldBeSameAs(TestDispatcher);
	}

	[Fact]
	public void OverwriteExistingRegistration()
	{
		Func<object, IMessageContext, ISagaEvent, SagaInfo, CancellationToken, Task> replacement =
			(_, _, _, _, _) => Task.CompletedTask;

		_sut.Register(typeof(string), typeof(int), TestDispatcher);
		_sut.Register(typeof(string), typeof(int), replacement);

		var result = _sut.GetDispatcher(typeof(string), typeof(int));

		result.ShouldBeSameAs(replacement);
	}

	[Fact]
	public void DistinguishDifferentTypePairs()
	{
		Func<object, IMessageContext, ISagaEvent, SagaInfo, CancellationToken, Task> other =
			(_, _, _, _, _) => Task.CompletedTask;

		_sut.Register(typeof(string), typeof(int), TestDispatcher);
		_sut.Register(typeof(int), typeof(string), other);

		_sut.GetDispatcher(typeof(string), typeof(int)).ShouldBeSameAs(TestDispatcher);
		_sut.GetDispatcher(typeof(int), typeof(string)).ShouldBeSameAs(other);
	}

	[Fact]
	public void ThrowOnNullDispatcher()
	{
		Should.Throw<ArgumentNullException>(
			() => _sut.Register(typeof(string), typeof(int), null!));
	}

	[Fact]
	public async Task InvokeRegisteredDispatcher()
	{
		var invoked = false;
		Func<object, IMessageContext, ISagaEvent, SagaInfo, CancellationToken, Task> tracker =
			(_, _, _, _, _) =>
			{
				invoked = true;
				return Task.CompletedTask;
			};

		_sut.Register(typeof(string), typeof(int), tracker);

		var dispatcher = _sut.GetDispatcher(typeof(string), typeof(int))!;
		await dispatcher(new object(), A.Fake<IMessageContext>(), A.Fake<ISagaEvent>(), default, CancellationToken.None);

		invoked.ShouldBeTrue();
	}

	[Fact]
	public void ImplementISagaDispatchRegistry()
	{
		_sut.ShouldBeAssignableTo<ISagaDispatchRegistry>();
	}

	[Fact]
	public void ThrowWhenRegisteringAfterFreeze()
	{
		_sut.Freeze();

		Should.Throw<InvalidOperationException>(
			() => _sut.Register(typeof(string), typeof(int), TestDispatcher));
	}

	[Fact]
	public void AllowGetDispatcherAfterFreeze()
	{
		_sut.Register(typeof(string), typeof(int), TestDispatcher);
		_sut.Freeze();

		_sut.GetDispatcher(typeof(string), typeof(int)).ShouldBeSameAs(TestDispatcher);
	}

	[Fact]
	public void AllowDoubleFreezeWithoutThrow()
	{
		_sut.Freeze();
		_sut.Freeze(); // Should not throw
	}
}
