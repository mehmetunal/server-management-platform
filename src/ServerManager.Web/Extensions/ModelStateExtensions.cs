using Microsoft.AspNetCore.Mvc.ModelBinding;
using ServerManager.Application.Common;

namespace ServerManager.Web.Extensions;

public static class ModelStateExtensions
{
    public static void AddServiceErrors(this ModelStateDictionary modelState, ServiceResult result)
    {
        if (result.Errors.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(result.Message))
                modelState.AddModelError(string.Empty, result.Message);
            return;
        }

        foreach (var error in result.Errors)
            modelState.AddModelError(error.PropertyName, error.Message);
    }
}
