

namespace ACC_DA4R.Controllers
{
    public class DesignAutomation4Revit
    {
        private readonly DesignAutomationClient _designAutomation;
        private readonly ILogger<DesignAutomation4Revit> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        private const string AppName = "FindColumnsApp";
        private const string AppBundleName = "FindColumnsIO.zip";
        private const string ActivityName = "FindColumnsActivity";
        private const string EngineName = "Autodesk.Revit+2022";

        public string NickName => _configuration["APS_CLIENT_ID"];
        private string Alias => _configuration["APS_DA_ALIAS"] ?? "dev";
        private string AppBundleFullName => $"{NickName}.{AppName}+{Alias}";
        private string ActivityFullName => $"{NickName}.{ActivityName}+{Alias}";

        public DesignAutomation4Revit()
        {
            ForgeService service =
                new ForgeService(
                    new HttpClient(
                        new ForgeHandler(Microsoft.Extensions.Options.Options.Create(new ForgeConfiguration()
                        {
                            ClientId = Credentials.GetAppSetting("APS_CLIENT_ID"),
                            ClientSecret = Credentials.GetAppSetting("APS_CLIENT_SECRET")
                        }))
                        {
                            InnerHandler = new HttpClientHandler()
                        })
                );

            _designAutomation = new DesignAutomationClient(service);
        }

        public async Task EnsureAppBundleAsync(string contentRootPath)
        {
            var appBundles = await _designAutomation.GetAppBundlesAsync();
            if (!appBundles.Data.Contains(AppBundleFullName))
            {
                string packageZipPath = Path.Combine(contentRootPath, "bundles", AppBundleName);
                if (!File.Exists(packageZipPath))
                {
                    _logger.LogError("AppBundle zip not found at {Path}", packageZipPath);
                    throw new FileNotFoundException("AppBundle zip not found.", packageZipPath);
                }

                var appBundleSpec = new AppBundle
                {
                    Package = AppName,
                    Engine = EngineName,
                    Id = AppName,
                    Description = $"Description for {AppBundleName}"
                };

                var newAppVersion = await _designAutomation.CreateAppBundleAsync(appBundleSpec);
                if (newAppVersion == null)
                {
                    _logger.LogError("Failed to create new AppBundle.");
                    throw new Exception("Cannot create new AppBundle.");
                }

                var aliasSpec = new Alias { Id = Alias, Version = 1 };
                await _designAutomation.CreateAppBundleAliasAsync(AppName, aliasSpec);

                using var uploadClient = new HttpClient();
                using var multipartContent = new MultipartFormDataContent();
                foreach (var kvp in newAppVersion.UploadParameters.FormData)
                {
                    multipartContent.Add(new StringContent(kvp.Value), kvp.Key);
                }
                multipartContent.Add(new ByteArrayContent(await File.ReadAllBytesAsync(packageZipPath)), "file", AppBundleName);

                var response = await uploadClient.PostAsync(newAppVersion.UploadParameters.EndpointURL, multipartContent);
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task EnsureActivityAsync(string script)
        {
            var activities = await _designAutomation.GetActivitiesAsync();
            if (!activities.Data.Contains(ActivityFullName))
            {
                string commandLine = $"$(engine.path)\\revitcoreconsole.exe /i \"$(args[inputFile].path)\" /al \"$(appbundles[{AppName}].path)\"";

                var activitySpec = new Activity
                {
                    Id = ActivityName,
                    Appbundles = new List<string> { AppBundleFullName },
                    CommandLine = new List<string> { commandLine },
                    Engine = EngineName,
                    Parameters = new Dictionary<string, Parameter>
                {
                    { "inputFile", new Parameter { Description = "Input Revit File", LocalName = "$(inputFile)", Ondemand = false, Required = true, Verb = Verb.Get, Zip = false } },
                    { "result", new Parameter { Description = "Resulting JSON File", LocalName = "result.txt", Ondemand = false, Required = true, Verb = Verb.Put, Zip = false } }
                },
                    Settings = new Dictionary<string, ISetting>
                {
                    { "script", new StringSetting { Value = script } }
                }
                };

                var newActivity = await _designAutomation.CreateActivityAsync(activitySpec);
                if (newActivity == null)
                {
                    _logger.LogError("Failed to create new Activity.");
                    throw new Exception("Cannot create new Activity.");
                }

                var aliasSpec = new Alias { Id = Alias, Version = 1 };
                await _designAutomation.CreateActivityAliasAsync(ActivityName, aliasSpec);
            }
        }

        public async Task StartDesignCheckAsync(string userId, string hubId, string projectId, string versionId, string contentRootPath, string script)
        {
            await EnsureAppBundleAsync(contentRootPath);
            await EnsureActivityAsync(script);

            string resultFilename = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(versionId)) + ".txt";
            string callbackUrl = $"{_configuration["APS_WEBHOOK_URL"]}/api/aps/callback/designautomation/{userId}/{hubId}/{projectId}/{versionId}";

            var workItemSpec = new WorkItem
            {
                ActivityId = ActivityFullName,
                Arguments = new Dictionary<string, IArgument>
            {
                { "inputFile", await BuildDownloadURLAsync(projectId, versionId) },
                { "result", await BuildUploadURLAsync(resultFilename) },
                { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
            }
            };

            var workItemStatus = await _designAutomation.CreateWorkItemAsync(workItemSpec);
            _logger.LogInformation("WorkItem started with ID: {Id}", workItemStatus.Id);
        }

        private async Task<XrefTreeArgument> BuildDownloadURLAsync(string projectId, string versionId)
        {
            var versionsApi = new VersionsApi();
            versionsApi.Configuration.AccessToken = await GetInternalTokenAsync();

            dynamic version = await versionsApi.GetVersionAsync(projectId, versionId);
            string storageId = version.data.relationships.storage.data.id;
            string[] storageParams = storageId.Split('/');
            string bucketKey = storageParams[^2].Split(':')[^1];
            string objectName = storageParams[^1];

            string downloadUrl = $"https://developer.api.autodesk.com/oss/v2/buckets/{bucketKey}/objects/{objectName}";

            return new XrefTreeArgument
            {
                Url = downloadUrl,
                Verb = Verb.Get,
                Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + await GetInternalTokenAsync() }
            }
            };
        }

        private async Task<XrefTreeArgument> BuildUploadURLAsync(string resultFilename)
        {
            string bucketName = "revitdesigncheck" + NickName.ToLower();
            var bucketsApi = new BucketsApi();
            bucketsApi.Configuration.AccessToken = await GetInternalTokenAsync();

            var bucketPayload = new PostBucketsPayload(bucketName, null, PostBucketsPayload.PolicyKeyEnum.Transient);
            try
            {
                await bucketsApi.CreateBucketAsync(bucketPayload, "US");
            }
            catch
            {
                // Bucket may already exist
            }

            var objectsApi = new ObjectsApi();
            objectsApi.Configuration.AccessToken = await GetInternalTokenAsync();

            var signedUrl = await objectsApi.CreateSignedResourceAsyncWithHttpInfo(bucketName, resultFilename, new PostBucketsSigned(5), "readwrite");

            return new XrefTreeArgument
            {
                Url = signedUrl.Data.signedUrl,
                Verb = Verb.Put
            };
        }

        private async Task<string> GetInternalTokenAsync()
        {
            var oauth = new TwoLeggedApi();
            var bearer = await oauth.AuthenticateAsync(
                _configuration["APS_CLIENT_ID"],
                _configuration["APS_CLIENT_SECRET"],
                "client_credentials",
                new Scope[] { Scope.BucketCreate, Scope.DataWrite, Scope.DataRead });

            return bearer.access_token;
        }

        public async Task StartDesignCheck(string userId, string hubId, string projectId, string versionId, string contentRootPath)
        {
            // uncomment these lines to clear all appbundles & activities under your account
            //await _designAutomation.DeleteForgeAppAsync("me");

            Credentials credentials = await Credentials.FromDatabaseAsync(userId);

            await EnsureAppBundleAsync(contentRootPath);
            await EnsureActivityAsync(" ");

            string resultFilename = versionId.Base64Encode() + ".txt";
            string callbackUrl = string.Format("{0}/api/aps/callback/designautomation/{1}/{2}/{3}/{4}", Credentials.GetAppSetting("APS_WEBHOOK_URL"), userId, hubId, projectId, versionId.Base64Encode());

            WorkItem workItemSpec = new WorkItem()
            {
                ActivityId = ActivityFullName,
                Arguments = new Dictionary<string, IArgument>()
                {
                    { "inputFile", await BuildDownloadURLAsync(projectId, versionId) },
                    { "result",  await BuildUploadURLAsync(resultFilename) },
                    { "onComplete", new XrefTreeArgument { Verb = Verb.Post, Url = callbackUrl } }
                }
            };
            WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemAsync(workItemSpec);
        }
    }
}
