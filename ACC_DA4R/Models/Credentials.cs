using Autodesk.Forge;
using Newtonsoft.Json;

namespace ACC_DA4R.Models
{
    /// <summary>
    /// Store data in session
    /// </summary>
    public class Credentials
    {
        private const string APS_COOKIE = "APSApp";

        private Credentials() { }
        public string TokenInternal { get; set; }
        public string TokenPublic { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string UserId { get; set; }

        /// <summary>
        /// Perform the OAuth authorization via code
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static async Task<Credentials> CreateFromCodeAsync(string code, IResponseCookies cookies)
        {
            ThreeLeggedApi oauth = new ThreeLeggedApi();

            dynamic credentialInternal = await oauth.GettokenAsync(
              GetAppSetting("APS_CLIENT_ID"), GetAppSetting("APS_CLIENT_SECRET"),
              oAuthConstants.AUTHORIZATION_CODE, code, GetAppSetting("APS_CALLBACK_URL"));

            dynamic credentialPublic = await oauth.RefreshtokenAsync(
              GetAppSetting("APS_CLIENT_ID"), GetAppSetting("APS_CLIENT_SECRET"),
              "refresh_token", credentialInternal.refresh_token, new Scope[] { Scope.ViewablesRead });

            Credentials credentials = new Credentials();
            credentials.TokenInternal = credentialInternal.access_token;
            credentials.TokenPublic = credentialPublic.access_token;
            credentials.RefreshToken = credentialPublic.refresh_token;
            credentials.ExpiresAt = DateTime.Now.AddSeconds(credentialInternal.expires_in);
            credentials.UserId = await GetUserId(credentials);

            cookies.Append(APS_COOKIE, JsonConvert.SerializeObject(credentials));

            // add a record on our database for the tokens and refresh token
            await OAuthDB.Register(credentials.UserId, JsonConvert.SerializeObject(credentials));

            return credentials;
        }

        private static async Task<string> GetUserId(Credentials credentials)
        {
            UserProfileApi userApi = new UserProfileApi();
            userApi.Configuration.AccessToken = credentials.TokenInternal;
            dynamic userProfile = await userApi.GetUserProfileAsync();
            return userProfile.userId;
        }

        /// <summary>
        /// Restore the credentials from the session object, refresh if needed
        /// </summary>
        /// <returns></returns>
        public static async Task<Credentials> FromSessionAsync(IRequestCookieCollection requestCookie, IResponseCookies responseCookie)
        {
            if (requestCookie == null || !requestCookie.ContainsKey(APS_COOKIE)) return null;

            Credentials credentials = JsonConvert.DeserializeObject<Credentials>(requestCookie[APS_COOKIE]);
            if (credentials.ExpiresAt < DateTime.Now)
            {
                credentials = await FromDatabaseAsync(credentials.UserId);
                responseCookie.Delete(APS_COOKIE);
                responseCookie.Append(APS_COOKIE, JsonConvert.SerializeObject(credentials));
            }

            return credentials;
        }

        public static async Task<Credentials> FromDatabaseAsync(string userId)
        {
            var doc = await OAuthDB.GetCredentials(userId);

            Credentials credentials = new Credentials();
            credentials.TokenInternal = (string)doc["TokenInternal"];
            credentials.TokenPublic = (string)doc["TokenPublic"];
            credentials.RefreshToken = (string)doc["RefreshToken"];
            credentials.ExpiresAt = DateTime.Parse((string)doc["ExpiresAt"]);
            credentials.UserId = userId;

            if (credentials.ExpiresAt < DateTime.Now)
            {
                await credentials.RefreshAsync();
            }

            return credentials;
        }

        public static void Signout(IResponseCookies cookies)
        {
            cookies.Delete(APS_COOKIE);
        }

        /// <summary>
        /// Refresh the credentials (internal & external)
        /// </summary>
        /// <returns></returns>
        private async Task RefreshAsync()
        {
            ThreeLeggedApi oauth = new ThreeLeggedApi();

            dynamic credentialInternal = await oauth.RefreshtokenAsync(
              GetAppSetting("APS_CLIENT_ID"), GetAppSetting("APS_CLIENT_SECRET"),
              "refresh_token", RefreshToken, new Scope[] { Scope.DataRead, Scope.DataCreate, Scope.DataWrite, Scope.ViewablesRead });

            dynamic credentialPublic = await oauth.RefreshtokenAsync(
              GetAppSetting("APS_CLIENT_ID"), GetAppSetting("APS_CLIENT_SECRET"),
              "refresh_token", credentialInternal.refresh_token, new Scope[] { Scope.ViewablesRead });

            TokenInternal = credentialInternal.access_token;
            TokenPublic = credentialPublic.access_token;
            RefreshToken = credentialPublic.refresh_token;
            ExpiresAt = DateTime.Now.AddSeconds(credentialInternal.expires_in);

            // update the record on our database for the tokens and refresh token
            await OAuthDB.Register(await GetUserId(this), JsonConvert.SerializeObject(this));
        }

        /// <summary>
        /// Reads appsettings from web.config
        /// </summary>
        public static string GetAppSetting(string settingKey)
        {
            return Environment.GetEnvironmentVariable(settingKey);
        }

        public static async Task<dynamic> Get2LeggedTokenAsync(Scope[] scopes)
        {
            TwoLeggedApi oauth = new TwoLeggedApi();
            string grantType = "client_credentials";
            dynamic bearer = await oauth.AuthenticateAsync(
              GetAppSetting("APS_CLIENT_ID"),
              GetAppSetting("APS_CLIENT_SECRET"),
              grantType,
              scopes);
            return bearer;
        }
    }
}
