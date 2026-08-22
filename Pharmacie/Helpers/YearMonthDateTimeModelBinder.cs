using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Pharmacie.Helpers;

/// <summary>
/// Les inputs HTML <c>type="month"</c> envoient <c>yyyy-MM</c>, que le binder DateTime standard refuse.
/// </summary>
public sealed class YearMonthDateTimeModelBinder : IModelBinder
{
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

        if (ExpirationMonth.TryParse(raw, out var date))
        {
            bindingContext.Result = ModelBindingResult.Success(date);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            "Date invalide.");
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
