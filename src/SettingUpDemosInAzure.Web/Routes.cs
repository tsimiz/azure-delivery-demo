using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SettingUpDemosInAzure.Web
{
    public static class Routes
    {
        private const string CosmosDatabaseId = "SampleCosmosDb";
        private const string CosmosContainerId = "SampleCosmosDbContainer";

        public static void MapApplicationRoutes(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/samples", async ctx =>
            {
                // use the db's interchangeably just to see all the things working
                if (DateTime.UtcNow.Millisecond % 2 == 0)
                {
                    await using var sqlConnection =
                        ctx.RequestServices.GetRequiredService<SqlConnectionFactory>()();
                    var results = await sqlConnection.QueryAsync<SampleModel>(
                        "SELECT SomeId, SomeDescription, SomeDate from dbo.Samples");

                    ctx.Response.Headers.Add("Results-Origin", "SQL");
                    ctx.Response.StatusCode = (int) HttpStatusCode.OK;
                    await ctx.Response.WriteAsJsonAsync(results);
                }
                else
                {
                    var results = new List<SampleModel>();
                    using var cosmosClient = ctx.RequestServices.GetRequiredService<CosmosClientFactory>()();
                    var container = cosmosClient.GetContainer(CosmosDatabaseId, CosmosContainerId);
                    var iterator = container.GetItemQueryIterator<SampleCosmosModel>();
                    while (iterator.HasMoreResults)
                    {
                        var resultSet = await iterator.ReadNextAsync();
                        results.AddRange(resultSet.Resource.Select(m => m.AsModel()));
                    }

                    ctx.Response.Headers.Add("Results-Origin", "CosmosDB");
                    ctx.Response.StatusCode = (int) HttpStatusCode.OK;
                    await ctx.Response.WriteAsJsonAsync(results);
                }
            });

            endpoints.MapPost("/samples", async ctx =>
            {
                var input = await ctx.Request.ReadFromJsonAsync<SampleInputModel>();
                var newItem = new SampleModel(Guid.NewGuid(), input.SomeDescription, input.SomeDate);

                await using var connection = ctx.RequestServices.GetRequiredService<SqlConnectionFactory>()();
                using var cosmosClient = ctx.RequestServices.GetRequiredService<CosmosClientFactory>()();

                var rowsAffected = await connection.ExecuteAsync(
                    "INSERT INTO dbo.Samples (SomeId, SomeDescription, SomeDate) values (@SomeId, @SomeDescription, @SomeDate)",
                    newItem);

                var container = cosmosClient.GetContainer(CosmosDatabaseId, CosmosContainerId);
                var cosmosResult = await container.CreateItemAsync(SampleCosmosModel.FromModel(newItem));

                if (rowsAffected == 1 && (int) cosmosResult.StatusCode >= 200 &&
                    (int) cosmosResult.StatusCode < 300)
                {
                    await using var producer = ctx.RequestServices.GetRequiredService<ProducerFactory>()();
                    await producer.SendAsync(new[] {new EventData(JsonSerializer.SerializeToUtf8Bytes(newItem))});

                    ctx.Response.StatusCode = (int) HttpStatusCode.OK;
                    await ctx.Response.WriteAsJsonAsync(newItem);
                }
                else
                {
                    ctx.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
                    await ctx.Response.WriteAsJsonAsync(new {message = "Something went wrong!"});
                }
            });
        }
    }


    public record SampleInputModel(string SomeDescription, DateTime SomeDate);

    public record SampleModel(Guid SomeId, string SomeDescription, DateTime SomeDate);

    public record SampleCosmosModel
    {
        [JsonProperty("id")] public Guid SomeId { get; init; }

        public string SomeDescription { get; init; }

        public DateTime SomeDate { get; init; }

        public static SampleCosmosModel FromModel(SampleModel model)
            => new()
            {
                SomeId = model.SomeId,
                SomeDescription = model.SomeDescription,
                SomeDate = model.SomeDate
            };

        public SampleModel AsModel() => new(this.SomeId, this.SomeDescription, this.SomeDate);
    }
    
    public delegate SqlConnection SqlConnectionFactory();

    public delegate EventHubProducerClient ProducerFactory();

    public delegate CosmosClient CosmosClientFactory();
}