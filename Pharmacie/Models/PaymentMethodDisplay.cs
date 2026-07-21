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
        _ => "text-bg-secondary"
    };
}
