using Pharmacie.Models;

namespace Pharmacie.Models.Dto;

/// <summary>Résultat du matching d'une ligne validée (pas une entité EF).</summary>
public class MatchedImportRow
{
    public ValidatedImportRow ValidatedRow { get; set; } = null!;

    public ImportLineAction ResolvedAction { get; set; }

    public int? MatchedProductId { get; set; }

    /// <summary>Première occurrence du même CIP dans l'import, lorsque le produit n'existe pas encore en base.</summary>
    public int? ReferenceFirstOccurrenceRowNumber { get; set; }
}
