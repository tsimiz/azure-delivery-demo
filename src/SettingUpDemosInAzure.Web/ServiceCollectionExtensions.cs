using System.Data.SqlClient;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SettingUpDemosInAzure.Web
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddConnectionFactories(this IServiceCollection services)
            => services
                .AddSingleton<SqlConnectionFactory>(s =>
                {
                    var config = s.GetRequiredService<IConfiguration>();
                    var connectionString = config.GetConnectionString("SqlConnectionString");
                    return () => new SqlConnection(connectionString);
                })
                .AddSingleton<ProducerFactory>(s =>
                {
                    var config = s.GetRequiredService<IConfiguration>();
                    var connectionString = config.GetConnectionString("EventHubConnectionString");
                    return () => new EventHubProducerClient(connectionString);
                })
                .AddSingleton<CosmosClientFactory>(s =>
                {
                    var config = s.GetRequiredService<IConfiguration>();
                    var connectionString = config.GetConnectionString("CosmosDbConnectionString");
                    return () => new CosmosClient(connectionString, new CosmosClientOptions
                    {
                        SerializerOptions = new CosmosSerializationOptions
                        {
                            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                        }
                    });
                });
    }
}