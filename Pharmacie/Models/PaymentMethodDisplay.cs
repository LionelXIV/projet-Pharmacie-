using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Pharmacie.Models;

public static class PaymentMethodDisplay
{
    public static string GetName(PaymentMethod method)
    {
        var member = typeof(PaymentMethod).GetMember(method.ToString()).FirstOrDefault();
        var display = member?.GetCustomAttribute<DisplayAttribute>();
        return display?.GetName() ?? method.ToString();
    }

    public static string BadgeCssClass(PaymentMethod method) => method switch
    {
        PaymentMethod.Wave => "badge-wave",
        PaymentMethod.OrangeMoney => "text-bg-warning",
        PaymentMethod.YasMoney => "text-bg-info",
        PaymentMethod.TPE => "text-bg-dark",
        PaymentMethod.Especes => "text-bg-success",
        PaymentMethod.Freemoney => "text-bg-success",
        PaymentMethod.TransfertInternational => "text-bg-secondary",
        _ => "text-bg-secondary"
    };

    public static string Format(PaymentMethod method, string? autreLibelle = null)
    {
        if (method == PaymentMethod.Autre && !string.IsNullOrWhiteSpace(autreLibelle))
            return $"Autre : {autreLibelle.Trim()}";
        return GetName(method);
    }
}
