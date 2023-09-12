using System.Net;
using Microsoft.AspNetCore.Mvc;
using Orleans.Infra;

namespace Silo.Controllers;

[ApiController]
public class DefaultController : ControllerBase
{

    private readonly ILogger<DefaultController> logger;

    public DefaultController(ILogger<DefaultController> logger)
    {
        this.logger = logger;
    }

    [Route("/reset")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> Reset()
    {
        logger.LogWarning("Cleanup requested at {0}", DateTime.UtcNow);
        await Helper.ResetPostgres();
        return Ok();
    }

    [Route("/cleanup")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> Cleanup()
    {
        logger.LogWarning("Cleanup requested at {0}", DateTime.UtcNow);
        await Helper.CleanUpPostgres();
        return Ok();
    }
}