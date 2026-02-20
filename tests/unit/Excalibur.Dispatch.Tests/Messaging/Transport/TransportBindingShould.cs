using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportBindingShould
{
	private readonly ITransportAdapter _fakeAdapter = A.Fake<ITransportAdapter>();

	[Fact]
	public void CreateWithRequiredParameters()
	{
		var binding = new TransportBinding("test-binding", _fakeAdapter, "orders/*");

		binding.Name.ShouldBe("test-binding");
		binding.TransportAdapter.ShouldBe(_fakeAdapter);
		binding.EndpointPattern.ShouldBe("orders/*");
		binding.PipelineProfile.ShouldBeNull();
		binding.AcceptedMessageKinds.ShouldBe(MessageKinds.All);
		binding.Priority.ShouldBe(0);
	}

	[Fact]
	public void CreateWithAllParameters()
	{
		var profile = A.Fake<IPipelineProfile>();
		var binding = new TransportBinding(
			"test-binding", _fakeAdapter, "orders/*",
			profile, MessageKinds.Action, 10);

		binding.PipelineProfile.ShouldBe(profile);
		binding.AcceptedMessageKinds.ShouldBe(MessageKinds.Action);
		binding.Priority.ShouldBe(10);
	}

	[Fact]
	public void ThrowOnNullName()
	{
		Should.Throw<ArgumentException>(() => new TransportBinding(null!, _fakeAdapter, "test"));
	}

	[Fact]
	public void ThrowOnEmptyName()
	{
		Should.Throw<ArgumentException>(() => new TransportBinding("", _fakeAdapter, "test"));
	}

	[Fact]
	public void ThrowOnNullAdapter()
	{
		Should.Throw<ArgumentNullException>(() => new TransportBinding("test", null!, "test"));
	}

	[Fact]
	public void ThrowOnNullEndpointPattern()
	{
		Should.Throw<ArgumentException>(() => new TransportBinding("test", _fakeAdapter, null!));
	}

	[Fact]
	public void MatchExactEndpoint()
	{
		var binding = new TransportBinding("test", _fakeAdapter, "orders/create");

		binding.Matches("orders/create").ShouldBeTrue();
		binding.Matches("Orders/Create").ShouldBeTrue(); // Case insensitive
		binding.Matches("orders/delete").ShouldBeFalse();
	}

	[Fact]
	public void MatchWildcardEndpoint()
	{
		var binding = new TransportBinding("test", _fakeAdapter, "orders/*");

		binding.Matches("orders/create").ShouldBeTrue();
		binding.Matches("orders/delete").ShouldBeTrue();
		binding.Matches("users/create").ShouldBeFalse();
	}

	[Fact]
	public void MatchQuestionMarkWildcard()
	{
		var binding = new TransportBinding("test", _fakeAdapter, "orders/v?");

		binding.Matches("orders/v1").ShouldBeTrue();
		binding.Matches("orders/v2").ShouldBeTrue();
		binding.Matches("orders/v10").ShouldBeFalse(); // ? matches single char
	}

	[Fact]
	public void MatchComplexPattern()
	{
		var binding = new TransportBinding("test", _fakeAdapter, "*.events.*");

		binding.Matches("orders.events.created").ShouldBeTrue();
		binding.Matches("users.events.updated").ShouldBeTrue();
		binding.Matches("orders.commands.create").ShouldBeFalse();
	}
}
