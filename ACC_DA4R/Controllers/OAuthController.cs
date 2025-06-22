using ACC_DA4R.Models;
using Autodesk.Authentication.Model;
using Autodesk.Forge;
using System.Net;

namespace ACC_DA4R.Controllers
{
    public class OAuthController : ControllerBase 
    {
        [HttpGet]
        [Route("api/aps/oauth/token")]
        public async Task<AccessToken> GetPublicTokenAsync()
        {
            Credentials credentials = await Credentials.FromSessionAsync(Request.Cookies, Response.Cookies);

            if (credentials == null)
            {
                base.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return new AccessToken();
            }

            // return the public (viewables:read) access token
            return new AccessToken()
            {
                access_token = credentials.TokenPublic,
                expires_in = (int)credentials.ExpiresAt.Subtract(DateTime.Now).TotalSeconds
            };
        }

    

        [HttpGet]
        [Route("api/aps/oauth/signout")]
        public IActionResult Singout()
        {
            // finish the session
            Credentials.Signout(base.Response.Cookies);

            return Redirect("/");
        }

        [HttpGet]
        [Route("api/aps/oauth/url")]
        public string GetOAuthURL()
        {
            // prepare the sign in URL
            Scope[] scopes = { Scope.DataRead };
            ThreeLeggedApi _threeLeggedApi = new ThreeLeggedApi();
            string oauthUrl = _threeLeggedApi.Authorize(
              Credentials.GetAppSetting("APS_CLIENT_ID"),
              oAuthConstants.CODE,
              Credentials.GetAppSetting("APS_CALLBACK_URL"),
              new Scope[] { Scope.DataRead,  Scope.DataWrite, Scope.AccountRead });

            return oauthUrl;
        }

        [HttpGet]
        [Route("api/aps/callback/oauth")] // see Web.Config APS_CALLBACK_URL variable
        public async Task<IActionResult> OAuthCallbackAsync(string code)
        {
            // create credentials form the oAuth CODE
            Credentials credentials = await Credentials.CreateFromCodeAsync(code, Response.Cookies);

            return Redirect("/");
        }

        [HttpGet]
        [Route("api/aps/clientid")]
        public dynamic GetClientID()
        {
            return new { id = Credentials.GetAppSetting("APS_CLIENT_ID") };
        }
    }

    /// <summary>
    /// Response for GetPublicToken
    /// </summary>
    public struct AccessToken
    {
        public string access_token { get; set; }
        public int expires_in { get; set; }
    }
}
