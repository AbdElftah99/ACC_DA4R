using Autodesk.DataManagement;
using Autodesk.DataManagement.Model;
using Hangfire.Storage;

namespace ACC_DA4R.Models
{
    public partial class APS
    {
        public async Task<IEnumerable<HubData>> GetHubsAsync(Tokens tokens)
        {
            var dataManagementClient = new DataManagementClient();
            var hubs = await dataManagementClient.GetHubsAsync(accessToken: tokens.InternalToken);
            return hubs.Data;
        }

        public async Task<IEnumerable<ProjectData>> GetProjectsAsync(string hubId, Tokens tokens)
        {
            var dataManagementClient = new DataManagementClient();
            var projects = await dataManagementClient.GetHubProjectsAsync(hubId, accessToken: tokens.InternalToken);
            return projects.Data;
        }
    }
}
