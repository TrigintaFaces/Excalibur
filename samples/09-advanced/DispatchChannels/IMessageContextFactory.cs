using Excalibur.Dispatch.Abstractions.Pipeline;

namespace examples.Excalibur.Dispatch.Channels;

/// <summary>
/// Simple factory interface for creating message contexts.
/// </summary>
public interface IMessageContextFactory
{
	/// <summary>
	/// Creates a message context for the given message.
	/// </summary>
	public IMessageContext CreateContext(IDispatchMessage message);
}
