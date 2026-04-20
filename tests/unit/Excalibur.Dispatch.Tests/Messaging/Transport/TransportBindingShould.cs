using System.Reflection;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
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

	/// <summary>
	/// Defense-in-depth against ReDoS: the endpoint regex constructed from a
	/// wildcard pattern MUST carry an explicit 1-second MatchTimeout so a
	/// pathological pattern configured by a consumer cannot hang a dispatcher
	/// thread with catastrophic backtracking.
	/// </summary>
	/// <remarks>[S795 bd-ilwc63].</remarks>
	[Fact]
	public void RegexHasExplicitMatchTimeoutForReDoSDefense()
	{
		var binding = new TransportBinding("test", _fakeAdapter, "orders/*");

		var field = typeof(TransportBinding).GetField(
			"_endpointRegex",
			BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull("_endpointRegex field must exist on TransportBinding.");

		var regex = field!.GetValue(binding) as Regex;
		regex.ShouldNotBeNull("Wildcard patterns must compile to a Regex instance.");

		regex!.MatchTimeout.ShouldBe(
			TimeSpan.FromSeconds(1),
			"TransportBinding must construct its endpoint regex with an explicit " +
			"1-second MatchTimeout (ReDoS defense-in-depth per bd-ilwc63).");
	}

	/// <summary>
	/// The MatchTimeout is enforced at match time — a very long candidate
	/// endpoint must return a bounded result (either a match decision or a
	/// <see cref="RegexMatchTimeoutException"/>) rather than hanging the
	/// calling thread.
	/// </summary>
	/// <remarks>
	/// Generous 2-second wall-clock assertion absorbs Windows/Linux timer
	/// variance around the 1-second internal timeout. [S795 bd-ilwc63 / plan §Risk row 4].
	/// </remarks>
	[Fact]
	public void MatchesCompletesWithinTimeoutForLongInput()
	{
		var binding = new TransportBinding("test", _fakeAdapter, "orders/*/events/*");

		// 200K-character input — catastrophic-backtracking risk is low for this
		// particular escaped-wildcard shape, but the bounded-evaluation contract
		// must hold regardless. The test asserts the wall-clock budget, not the
		// match outcome.
		var longInput = new string('a', 200_000);

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		try
		{
			_ = binding.Matches(longInput);
		}
		catch (RegexMatchTimeoutException)
		{
			// Acceptable — timeout fired before input was fully evaluated.
		}
		stopwatch.Stop();

		stopwatch.Elapsed.ShouldBeLessThan(
			TimeSpan.FromSeconds(2),
			"Matches(long-input) must return or throw RegexMatchTimeoutException " +
			"within 2 seconds (1-second MatchTimeout + platform timer variance).");
	}
}
