using Excalibur.Dispatch.Abstractions.Pipeline;

namespace examples.CrossLangSample;

/// <summary>
/// Sample event published to RabbitMQ.
/// </summary>
/// <param name="Message">Event text.</param>
///
[MessageSchemaVersion(1)]
[MessageSerializerVersion(1)]
public sealed record PingEvent(string Message) : IDispatchEvent;
