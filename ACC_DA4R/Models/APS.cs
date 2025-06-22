using Autodesk.Authentication.Model;
using Autodesk.SDKManager;

namespace ACC_DA4R.Models
{
    public partial class APS(string clientId, string clientSecret, string callbackUri)
    {
        // Credientials
        private readonly string _clientId = clientId;
        private readonly string _clientSecret = clientSecret;

        private readonly string _callbackUri = callbackUri;
        //for working with ACC Issue on server side
        private readonly List<Scopes> InternalTokenScopes = [Scopes.DataRead, Scopes.DataWrite, Scopes.AccountRead];
        //for working with APS Viewer on client side (future tutorial)
        private readonly List<Scopes> PublicTokenScopes = [Scopes.ViewablesRead];
        public SDKManager _SDKManager = SdkManagerBuilder.Create().Build();
    }
}
