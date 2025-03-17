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
    public class AddTask
    {
        private readonly ILogger _logger;

        public AddTask(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AddTask>();
        }

        [Function("AddTask")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                var requestBody = await JsonSerializer.DeserializeAsync<TaskRequest>(req.Body);

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
                    var text = "INSERT INTO Tasks (Title, IsComplete) VALUES (@Title, @IsComplete)";
                    using (SqlCommand cmd = new SqlCommand(text, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", requestBody.Title);
                        cmd.Parameters.AddWithValue("@IsComplete", requestBody.IsComplete);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                response.StatusCode = System.Net.HttpStatusCode.OK;
                await response.WriteStringAsync("Task added");
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

    public class TaskRequest
    {
        public string Title { get; set; }
        public bool IsComplete { get; set; }
    }
}
