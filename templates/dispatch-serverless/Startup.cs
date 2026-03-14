#if (UseAws)
using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Company.DispatchServerless;

/// <summary>
/// Configures services for AWS Lambda functions.
/// </summary>
public static class Startup
{
    private static readonly Lazy<IServiceProvider> ServiceProviderLazy = new(BuildServiceProvider);

    /// <summary>
    /// Gets the configured service provider.
    /// </summary>
    public static IServiceProvider ServiceProvider => ServiceProviderLazy.Value;

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(Startup).Assembly);
#if (UseKafka)
            dispatch.UseKafka(kafka =>
            {
                kafka.BootstrapServers(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092");
            });
#elif (UseRabbitMQ)
            dispatch.UseRabbitMQ(rmq =>
            {
                rmq.ConnectionString(Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING") ?? "amqp://guest:guest@localhost:5672/");
            });
#elif (UseAzureServiceBus)
            dispatch.UseAzureServiceBus(asb =>
            {
                asb.ConnectionString(Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING")
                    ?? throw new InvalidOperationException("AZURE_SERVICEBUS_CONNECTION_STRING is required."));
            });
#elif (UseAwsSqs)
            dispatch.UseAwsSqs(sqs =>
            {
                sqs.UseRegion(Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1");
            });
#elif (UseGooglePubSub)
            dispatch.UseGooglePubSub(pubsub =>
            {
                pubsub.ProjectId(Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "my-project");
            });
#endif
        });

        services.AddAwsLambdaServerless();

        return services.BuildServiceProvider();
    }
}
#elif (UseGcp)
using Excalibur.Dispatch.Configuration;

using Google.Cloud.Functions.Hosting;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: FunctionsStartup(typeof(Company.DispatchServerless.Startup))]

namespace Company.DispatchServerless;

/// <summary>
/// Configures services for Google Cloud Functions.
/// </summary>
public class Startup : FunctionsStartup
{
    /// <inheritdoc/>
    public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(Startup).Assembly);
#if (UseKafka)
            dispatch.UseKafka(kafka =>
            {
                kafka.BootstrapServers(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092");
            });
#elif (UseRabbitMQ)
            dispatch.UseRabbitMQ(rmq =>
            {
                rmq.ConnectionString(Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING") ?? "amqp://guest:guest@localhost:5672/");
            });
#elif (UseAzureServiceBus)
            dispatch.UseAzureServiceBus(asb =>
            {
                asb.ConnectionString(Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING")
                    ?? throw new InvalidOperationException("AZURE_SERVICEBUS_CONNECTION_STRING is required."));
            });
#elif (UseAwsSqs)
            dispatch.UseAwsSqs(sqs =>
            {
                sqs.UseRegion(Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1");
            });
#elif (UseGooglePubSub)
            dispatch.UseGooglePubSub(pubsub =>
            {
                pubsub.ProjectId(Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "my-project");
            });
#endif
        });

        services.AddGoogleCloudFunctionsServerless();
    }
}
#endif
