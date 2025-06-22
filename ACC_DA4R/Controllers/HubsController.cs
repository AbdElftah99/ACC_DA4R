using ACC_DA4R.Models;

namespace ACC_DA4R.Controllers
{
    public class HubsController(APS aps) : ApiController
    {
        private readonly APS _aps = aps;

        [HttpGet()]
        public async Task<ActionResult> ListHubs()
        {
            var tokens = await AuthController.PrepareTokens(Request, Response, _aps);
            if (tokens == null)
            {
                return Unauthorized();
            }
            return Ok(
                from hub in await _aps.GetHubsAsync(tokens)
                select new { id = hub.Id, name = hub.Attributes.Name }
            );
        }

        [HttpGet("{hub}/projects")]
        public async Task<ActionResult> ListProjects(string hub)
        {
            var tokens = await AuthController.PrepareTokens(Request, Response, _aps);
            if (tokens == null)
            {
                return Unauthorized();
            }
            return Ok(
                from project in await _aps.GetProjectsAsync(hub, tokens)
                select new { id = project.Id, name = project.Attributes.Name }
            );
        }
    }
}
