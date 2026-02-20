using Xunit.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Infrastructure;

/// <summary>
///     Extension methods for ILoggingBuilder for XUnit integration
/// </summary>
internal static class XUnitLoggingExtensions
{
	/// <summary>
	///     Adds XUnit logging to the logging builder
	/// </summary>
	public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper output) =>
		builder; // Stub implementation - would typically add an XUnit logger provider
}
