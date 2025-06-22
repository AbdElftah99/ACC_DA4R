using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ACC_DA4R.Services
{
    public class DMWebhook
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://developer.api.autodesk.com")
        };

        private string AccessToken { get; set; }
        private string CallbackURL { get; set; }

        public DMWebhook(string accessToken, string callbackUrl)
        {
            AccessToken = accessToken;
            CallbackURL = callbackUrl;
        }

        public async Task<IList<GetHookData.Hook>> Hooks(Event eventType, string folderId)
        {
            var requestUri = $"/webhooks/v1/systems/data/events/{EnumToString(eventType)}/hooks?scopeName=folder&scopeValue={folderId}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Authorization", $"Bearer {AccessToken}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var hookData = await response.Content.ReadFromJsonAsync<GetHookData>();
            return hookData?.data ?? new List<GetHookData.Hook>();
        }

        public async Task<HttpStatusCode> CreateHook(Event eventType, string hubId, string projectId, string folderId)
        {
            var requestUri = $"/webhooks/v1/systems/data/events/{EnumToString(eventType)}/hooks";
            var payload = new
            {
                callbackUrl = CallbackURL,
                scope = new { folder = folderId },
                hookAttribute = new { projectId, hubId }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {AccessToken}");

            var response = await _httpClient.SendAsync(request);
            return response.StatusCode;
        }

        public async Task<IDictionary<string, HttpStatusCode>> DeleteHook(Event eventType, string folderId)
        {
            var hooks = await Hooks(eventType, folderId);
            var statusDict = new Dictionary<string, HttpStatusCode>();

            foreach (var hook in hooks)
            {
                var requestUri = $"/webhooks/v1/systems/data/events/{EnumToString(eventType)}/hooks/{hook.hookId}";
                var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
                request.Headers.Add("Authorization", $"Bearer {AccessToken}");

                var response = await _httpClient.SendAsync(request);
                statusDict[hook.hookId] = response.StatusCode;
            }

            return statusDict;
        }

        private string EnumToString(Event eventType)
        {
            var name = Enum.GetName(typeof(Event), eventType);
            return "dm." + string.Join(".", Regex.Split(name, @"(?<!^)(?=[A-Z])")).ToLower();
        }
    }

    public class GetHookData
    {
        public Links links { get; set; }
        public List<Hook> data { get; set; }

        public class Links
        {
            public object next { get; set; }
        }

        public class Hook
        {
            public string hookId { get; set; }
            public string tenant { get; set; }
            public string callbackUrl { get; set; }
            public string createdBy { get; set; }
            public string @event { get; set; }
            public DateTime createdDate { get; set; }
            public string system { get; set; }
            public string creatorType { get; set; }
            public string status { get; set; }
            public Scope scope { get; set; }
            public string urn { get; set; }
            public string __self__ { get; set; }

            public class Scope
            {
                public string folder { get; set; }
            }
        }
    }

    public enum Event
    {
        VersionAdded,
        VersionModified,
        VersionDeleted,
        VersionMoved,
        VersionCoped // If this is a typo for "Copied", correct it.
    }
}
