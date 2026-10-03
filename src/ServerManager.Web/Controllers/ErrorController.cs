using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ServerManager.Web.Controllers;

[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController : Controller
{
    [Route("Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        ViewData["TraceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }

    [Route("Error/{statusCode:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int statusCode)
    {
        ViewData["StatusCode"] = statusCode;
        return View();
    }
}
