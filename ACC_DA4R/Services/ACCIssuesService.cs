using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace ACC_DA4R.Services
{
    public class ACCIssuesService
    {
        private const string BASE_URL = "https://developer.api.autodesk.com";

        /// <summary>
        /// Gets the Issues container ID for the given ACC project.
        /// </summary>
        public async Task<string> GetContainerIdAsync(string accessToken, string projectId)
        {
            var client = new RestClient(BASE_URL);
            var request = new RestRequest("/construction/issues/v2/containers", Method.Get);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddQueryParameter("project_id", projectId);

            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                throw new Exception($"Failed to get container ID: {response.StatusCode} - {response.Content}");
            }

            var json = JObject.Parse(response.Content);
            var containerId = json["data"]?[0]?["id"]?.ToString();

            return containerId;
        }

        /// <summary>
        /// Creates an ACC Issue in the given container.
        /// </summary>
        public async Task CreateIssueAsync(string accessToken, string containerId, string title, string description)
        {
            var client = new RestClient(BASE_URL);
            var request = new RestRequest($"/issues/v2/containers/{containerId}/issues", Method.Post);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddHeader("Content-Type", "application/vnd.api+json");

            var body = new
            {
                data = new
                {
                    type = "issues",
                    attributes = new
                    {
                        title = title,
                        description = description,
                        status = "open"
                    }
                }
            };

            var jsonBody = JsonConvert.SerializeObject(body);
            request.AddStringBody(jsonBody, DataFormat.Json);

            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                throw new Exception($"Failed to create issue: {response.StatusCode} - {response.Content}");
            }
        }
    }
}
