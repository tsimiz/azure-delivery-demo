using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SettingUpDemosInAzure.Web
{
    public class SqlDbInitializer : BackgroundService
    {
        private readonly SqlConnectionFactory _sqlConnectionFactory;
        private readonly ILogger<SqlDbInitializer> _logger;

        public SqlDbInitializer(SqlConnectionFactory sqlConnectionFactory, ILogger<SqlDbInitializer> logger)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var connection = _sqlConnectionFactory();

            var existsDb = await connection.ExecuteScalarAsync<bool>(
                "SELECT 1 FROM sys.Objects WHERE  Object_id = OBJECT_ID(N'dbo.Samples') AND Type = N'U'");

            if (!existsDb)
            {
                _logger.LogInformation("Table doesn't exist yet, creating...");

                await connection.ExecuteAsync(
                    @"CREATE TABLE dbo.Samples (
  SomeId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
  SomeDescription NVARCHAR(MAX) NOT NULL,
  SomeDate DATE NOT NULL
);");

                _logger.LogInformation("Table created.");
            }
            else
            {
                _logger.LogInformation("Table already exists, skipping creation.");
            }
        }
    }
}