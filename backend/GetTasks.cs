using System.Data.SqlClient;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Backend
{
    public class GetTasks
    {
        private readonly ILogger _logger;

        public GetTasks(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GetTasks>();
        }

        [Function("GetTasks")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                string keyVaultName = Environment.GetEnvironmentVariable("KEYVAULT_NAME");
                string kvUri = $"https://{keyVaultName}.vault.azure.net";
                var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());

                KeyVaultSecret dbPasswordSecret = await client.GetSecretAsync("DbPassword");
                string dbPassword = dbPasswordSecret.Value;

                string sqlServer = Environment.GetEnvironmentVariable("SQL_SERVER");
                string sqlDb = Environment.GetEnvironmentVariable("SQL_DB");
                string sqlAdmin = Environment.GetEnvironmentVariable("SQL_ADMIN");

                string connectionString = $"Server=tcp:{sqlServer}.database.windows.net,1433;Initial Catalog={sqlDb};Persist Security Info=False;User ID={sqlAdmin};Password={dbPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var text = "SELECT Id, Title, IsComplete FROM Tasks";
                    using (SqlCommand cmd = new SqlCommand(text, conn))
                    {
                        var reader = await cmd.ExecuteReaderAsync();
                        var tasks = new List<object>();

                        while (reader.Read())
                        {
                            tasks.Add(new
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                IsComplete = reader.GetBoolean(2)
                            });
                        }

                        response.Headers.Add("Content-Type", "application/json");
                        await response.WriteStringAsync(JsonSerializer.Serialize(tasks));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                await response.WriteStringAsync($"Error: {ex.Message}");
            }

            return response;
        }
    }
}
