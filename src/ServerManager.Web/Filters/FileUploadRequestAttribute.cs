using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using ServerManager.Application.Files;

namespace ServerManager.Web.Filters;

/// <summary>
/// Dosya yükleme action'ları için: gövde sınırı FileManagerOptions'tan alınır ve form, model binding
/// sırasında okunmaz; action formu sınırlı FormOptions ile kendisi okur.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FileUploadRequestAttribute : Attribute, IResourceFilter
{
    // Çok parçalı gövdedeki sınır ve alan başlıkları için pay.
    private const long MultipartOverheadBytes = 64 * 1024;

    public static long MaxBodyBytes(FileManagerOptions options) =>
        Math.Max(1, options.MaxUploadMegabytes) * 1024L * 1024L + MultipartOverheadBytes;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<FileManagerOptions>>().Value;
        var sizeFeature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
            sizeFeature.MaxRequestBodySize = MaxBodyBytes(options);

        var factories = context.ValueProviderFactories;
        factories.RemoveType<FormValueProviderFactory>();
        factories.RemoveType<FormFileValueProviderFactory>();
        factories.RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
