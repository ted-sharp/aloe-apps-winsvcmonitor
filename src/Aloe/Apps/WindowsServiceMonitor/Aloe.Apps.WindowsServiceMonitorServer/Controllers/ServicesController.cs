using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;

namespace Aloe.Apps.WindowsServiceMonitorServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServicesController : ControllerBase
{
    private readonly IServiceManager _serviceManager;

    public ServicesController(IServiceManager serviceManager)
    {
        this._serviceManager = serviceManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var services = await this._serviceManager.GetAllServicesAsync();
        return this.Ok(services);
    }

    [HttpGet("{serviceName}")]
    public async Task<IActionResult> Get(string serviceName)
    {
        var service = await this._serviceManager.GetServiceAsync(serviceName);
        if (service == null)
            return this.NotFound();
        return this.Ok(service);
    }
}
