using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Pharmacie.Helpers;

/// <summary>
/// Les inputs HTML <c>type="month"</c> envoient <c>yyyy-MM</c>.
/// Ne doit pas transformer les dates calendaires <c>yyyy-MM-dd</c> (filtres ventes, etc.).
/// </summary>
public sealed class YearMonthDateTimeModelBinder : IModelBinder
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "dd/MM/yyyy",
        "dd-MM-yyyy"
    ];

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var isNullable = bindingContext.ModelMetadata.IsReferenceOrNullableType
                         || Nullable.GetUnderlyingType(bindingContext.ModelType) == typeof(DateTime);

        var raw = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (isNullable)
                bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        var text = raw.Trim();

        // Péremption mois/année uniquement
        if (text.Length == 7 && text[4] == '-'
            && int.TryParse(text.AsSpan(0, 4), out var year)
            && int.TryParse(text.AsSpan(5, 2), out var month)
            && year is >= 2000 and <= 2100
            && month is >= 1 and <= 12)
        {
            bindingContext.Result = ModelBindingResult.Success(
                ExpirationMonth.EndOfMonth(new DateTime(year, month, 1)));
            return Task.CompletedTask;
        }

        if (DateTime.TryParseExact(
                text,
                DateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exact))
        {
            bindingContext.Result = ModelBindingResult.Success(DateTime.SpecifyKind(exact, DateTimeKind.Unspecified));
            return Task.CompletedTask;
        }

        if (ExpirationMonth.TryParse(text, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified));
            return Task.CompletedTask;
        }

        if (isNullable)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Date invalide.");
        return Task.CompletedTask;
    }
}

public sealed class YearMonthDateTimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = context.Metadata.ModelType;
        if (type == typeof(DateTime) || type == typeof(DateTime?))
            return new YearMonthDateTimeModelBinder();
        return null;
    }
}
