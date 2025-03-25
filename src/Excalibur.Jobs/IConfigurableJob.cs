namespace Excalibur.Jobs;

/// <summary>
///     Represents a job that is configured using a strongly-typed configuration object.
/// </summary>
/// <typeparam name="TConfig"> The type of the configuration object implementing <see cref="IJobConfig" />. </typeparam>
public interface IConfigurableJob<out TConfig> where TConfig : class, IJobConfig
{
}
