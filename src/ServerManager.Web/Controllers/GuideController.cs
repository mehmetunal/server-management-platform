using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using ServerManager.Web.Services;

namespace ServerManager.Web.Controllers;

/// <summary>Kullanım kılavuzu. Oturum açmış her kullanıcı okuyabilir; içerik derlemeye gömülü Markdown dosyalarıdır.</summary>
[Authorize]
public class GuideController : Controller
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private readonly UserGuide _guide;

    public GuideController(UserGuide guide)
    {
        _guide = guide;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["GuideImageBase"] = Url.Content("~/Guide/Image/");
        return View(_guide.Document);
    }

    [HttpGet]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public IActionResult Image(string id)
    {
        var stream = UserGuide.OpenImage(id);
        if (stream is null) return NotFound();
        return File(stream, ContentTypes.TryGetContentType(id, out var type) ? type : "application/octet-stream");
    }
}
