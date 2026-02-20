using AutoFixture;

namespace Tests.Shared.Builders;

/// <summary>
/// Builder for creating test messages with AutoFixture.
/// </summary>
public static class MessageBuilder
{
	private static readonly Fixture Fixture = new();

	public static TMessage Create<TMessage>() where TMessage : class
	{
		return Fixture.Create<TMessage>();
	}

	public static TMessage Build<TMessage>(Action<TMessage> configure) where TMessage : class
	{
		var message = Fixture.Create<TMessage>();
		configure(message);
		return message;
	}

	public static IEnumerable<TMessage> CreateMany<TMessage>(int count = 3) where TMessage : class
	{
		return Fixture.CreateMany<TMessage>(count);
	}
}
