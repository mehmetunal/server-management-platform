using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace ServerManager.Web.Extensions;

public static class TurkishModelBindingMessages
{
    public static void Apply(DefaultModelBindingMessageProvider provider)
    {
        provider.SetAttemptedValueIsInvalidAccessor((value, field) => $"'{value}' değeri {field} alanı için geçerli değil.");
        provider.SetMissingBindRequiredValueAccessor(field => $"{field} alanı zorunludur.");
        provider.SetMissingKeyOrValueAccessor(() => "Değer zorunludur.");
        provider.SetMissingRequestBodyRequiredValueAccessor(() => "İstek gövdesi boş olamaz.");
        provider.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"'{value}' değeri geçerli değil.");
        provider.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Girilen değer geçerli değil.");
        provider.SetNonPropertyValueMustBeANumberAccessor(() => "Değer sayı olmalıdır.");
        provider.SetUnknownValueIsInvalidAccessor(field => $"{field} alanı için girilen değer geçerli değil.");
        provider.SetValueIsInvalidAccessor(value => $"'{value}' değeri geçerli değil.");
        provider.SetValueMustBeANumberAccessor(field => $"{field} alanı sayı olmalıdır.");
        provider.SetValueMustNotBeNullAccessor(value => $"'{value}' değeri boş olamaz.");
    }
}
