using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Models.Dto;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Pharmacie.Services;

/// <summary>
/// Prévisualisation d'import BL (Excel/CSV/PDF texte) : lecture flexible + rapprochement catalogue.
/// N'enregistre rien en base — uniquement pour préremplir CreateDirect.
/// </summary>
public class BlImportService
{
    private static readonly HttpClient OcrHttp = new() { Timeout = TimeSpan.FromMinutes(3) };

    private readonly ApplicationDbContext _db;

    private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CIP"] = ["CIP", "CODE", "EAN", "EAN13", "CIPEAN13", "CIP_EAN13", "CODEBARRE", "CODE_BARRE", "CODEBARRES"],
        ["LIBELLE"] = ["LIBELLE", "PRODUIT", "DESIGNATION", "NOM", "MEDICAMENT", "LIBELLE_PRODUIT", "LIBELLE_DU_PRODUIT"],
        ["QTE"] = ["QTEFACT", "QTE", "QUANTITE", "QTY", "QTE_LIVREE", "QTELIVREE", "NB"],
        ["PRIX_ACHAT"] = ["PX_FAB", "PRIX_ACHAT", "PA", "PRIX_CESSION", "CESSION", "PRIXACHAT", "PRIX_FAB", "PRIX_DE_CESSION"],
        ["PRIX_VENTE"] = ["PPH", "PRIX_VENTE", "PV", "PRIX_PUBLIC", "PRIXPUBLIC", "PUBLIC", "PRIXVENTE"],
        ["LOT"] = ["LOT", "N_LOT", "NUMERO_LOT", "NUMLOT", "NLOT", "N°LOT", "N° LOT"],
        ["PEREMPTION"] = ["PEREMPTION", "EXPIRATION", "DATE_PEREMPTION", "DLU", "EXP", "DATE_EXP", "DATEPEREMPTION"],
        ["UG"] = ["UG", "GRATUIT", "UNITE_GRATUITE", "UNITES_GRATUITES", "QTE_UG", "QTEUG"],
        ["TVA"] = ["TVA", "TAUX_TVA", "TVA%", "TAUXTVA"]
    };

    public BlImportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<BlImportPreviewResult> PreviewAsync(Stream stream, string fileName, CancellationToken ct = default)
        => await PreviewAsync(stream, fileName, configuration: null, includeOcrDebug: false, ct);

    public async Task<BlImportPreviewResult> PreviewAsync(
        Stream stream,
        string fileName,
        IConfiguration? configuration,
        bool includeOcrDebug,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        List<BlImportRawRow> raw;
        string? texteOcr = null;
        string? supplierName = null;
        string? numeroBl = null;
        DateTime? dateBl = null;
        try
        {
            if (ext is ".xlsx" or ".xlsm")
            {
                raw = ReadExcel(buffer);
            }
            else if (ext is ".csv" or ".txt")
            {
                raw = ReadCsv(buffer);
                if (LooksLikeUbiPharmExport(fileName))
                {
                    supplierName = "UbiPharm";
                    numeroBl = ExtrairNumeroBL(fileName + "\nBEL/" + Path.GetFileNameWithoutExtension(fileName), "UbiPharm");
                    if (string.IsNullOrEmpty(numeroBl) && Regex.IsMatch(fileName ?? "", @"(\d{5,8})"))
                        numeroBl = "BEL/" + Regex.Match(fileName!, @"(\d{5,8})").Groups[1].Value;
                }
            }
            else if (ext == ".pdf")
            {
                var pdfBytes = buffer.ToArray();
                buffer.Position = 0;
                var texteNatif = ExtraireTextePdf(buffer);
                var depuisTexte = EssayerParsersDepuisTexte(texteNatif);
                var fournisseurDetecte = DetecterFournisseur(texteNatif ?? "");
                if (depuisTexte != null && depuisTexte.Value.Rows.Count > 0)
                {
                    raw = depuisTexte.Value.Rows;
                    supplierName = depuisTexte.Value.SupplierName;
                    numeroBl = depuisTexte.Value.NumeroBL;
                    dateBl = depuisTexte.Value.DateBL;
                }
                else if (fournisseurDetecte is "Sodipharm" or "UbiPharm")
                {
                    throw new InvalidOperationException(
                        $"PDF {fournisseurDetecte} reconnu mais aucune ligne produit extraite. Vérifiez le fichier ou importez le CSV.");
                }
                else
                {
                    buffer.Position = 0;
                    try
                    {
                        raw = ReadPdf(buffer);
                    }
                    catch (Exception)
                    {
                        var endpoint = configuration?["VisionAI:Endpoint"];
                        var apiKey = configuration?["VisionAI:ApiKey"];
                        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
                            throw new InvalidOperationException(
                                "PDF scanné : OCR Azure Vision non configuré (VisionAI:Endpoint / VisionAI:ApiKey).");

                        texteOcr = await ExtraireTexteOCR(pdfBytes, endpoint, apiKey);
                        if (string.IsNullOrWhiteSpace(texteOcr))
                            throw new InvalidOperationException("OCR : aucun texte extrait du PDF.");

                        var depuisOcr = EssayerParsersDepuisTexte(texteOcr);
                        if (depuisOcr == null || depuisOcr.Value.Rows.Count == 0)
                            throw new InvalidOperationException("OCR : aucune ligne produit détectée.");

                        raw = depuisOcr.Value.Rows;
                        supplierName = depuisOcr.Value.SupplierName;
                        numeroBl = depuisOcr.Value.NumeroBL;
                        dateBl = depuisOcr.Value.DateBL;
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Format non supporté. Utilisez .xlsx, .csv ou .pdf.");
            }
        }
        catch (Exception ex)
        {
            return new BlImportPreviewResult
            {
                Ok = false,
                Message = ex.Message
            };
        }

        if (raw.Count == 0)
        {
            return new BlImportPreviewResult
            {
                Ok = false,
                Message = "Aucune ligne de produit trouvée dans le fichier."
            };
        }

        var result = await MatchAsync(raw, ct);
        result.SupplierName = supplierName;
        result.NumeroBL = string.IsNullOrWhiteSpace(numeroBl) ? null : numeroBl;
        result.DateBL = dateBl;
        result.SupplierId = await ResoudreSupplierIdAsync(supplierName, ct);
        if (includeOcrDebug && !string.IsNullOrEmpty(texteOcr))
            result.TexteOcrBrut = texteOcr;
        return result;
    }

    private static List<BlImportRawRow> ConvertirLignesExtraite(List<BLLigneExtraite> extraits)
    {
        var raw = new List<BlImportRawRow>();
        var n = 0;
        foreach (var l in extraits)
        {
            n++;
            raw.Add(new BlImportRawRow
            {
                RowNumber = n,
                Cip = l.CIP,
                Libelle = l.NomProduit,
                Quantite = l.QuantiteLivree,
                PrixAchat = l.PrixAchat > 0 ? l.PrixAchat : null,
                PrixVente = l.PrixVente > 0 ? l.PrixVente : null,
                NumeroLot = l.NumeroLot,
                DatePeremption = l.DatePeremption,
                TauxTVA = l.TauxTVA,
                Confiance = l.Confiance
            });
        }

        return raw;
    }

    private static (List<BlImportRawRow> Rows, string SupplierName, string NumeroBL, DateTime? DateBL)? EssayerParsersDepuisTexte(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte) || texte.Trim().Length < 20)
            return null;

        var supplierName = DetecterFournisseur(texte);
        var extraits = supplierName == "Sodipharm"
            ? ParserSodipharm(texte)
            : ParserUbiPharm(texte);
        if (extraits.Count == 0 && supplierName != "Sodipharm")
            extraits = ParserSodipharm(texte);
        if (extraits.Count == 0)
            return null;

        return (
            ConvertirLignesExtraite(extraits),
            supplierName,
            ExtrairNumeroBL(texte, supplierName),
            ExtraireDate(texte));
    }

    private static bool LooksLikeUbiPharmExport(string? fileName)
    {
        var fn = fileName ?? "";
        return fn.Contains("BEL", StringComparison.OrdinalIgnoreCase)
            || fn.Contains("UBI", StringComparison.OrdinalIgnoreCase)
            || fn.Contains("UBIPHARM", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int?> ResoudreSupplierIdAsync(string? supplierName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(supplierName) || supplierName == "Inconnu")
            return null;

        var needle = supplierName.Trim().ToLowerInvariant();
        var id = await _db.Suppliers
            .AsNoTracking()
            .Where(s => s.Name.ToLower().Contains(needle))
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);
        return id;
    }

    private async Task<BlImportPreviewResult> MatchAsync(List<BlImportRawRow> raw, CancellationToken ct)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.ParentProductId == null)
            .Select(p => new ProductHit(
                p.Id,
                p.Cip,
                p.CommercialName,
                p.SalePrice,
                p.PurchasePrice,
                p.TauxTVA,
                p.AssujettiTVA,
                p.StockQuantity))
            .ToListAsync(ct);

        var byCip = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Cip))
            .GroupBy(p => p.Cip!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var byName = products
            .GroupBy(p => NormalizeName(p.CommercialName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var lines = new List<BlImportPreviewLine>();
        var matched = 0;
        var unmatched = 0;

        foreach (var row in raw)
        {
            var line = new BlImportPreviewLine
            {
                RowNumber = row.RowNumber,
                Quantite = row.Quantite ?? 1,
                PrixAchat = row.PrixAchat ?? 0,
                PrixVente = row.PrixVente ?? 0,
                NumeroLot = row.NumeroLot,
                DatePeremption = row.DatePeremption?.ToString("yyyy-MM-dd"),
                EstUG = row.EstUG || (row.NbUG ?? 0) > 0,
                NbUG = row.NbUG ?? (row.EstUG ? 1 : 0),
                TauxTVA = row.TauxTVA,
                Cip = row.Cip,
                Libelle = row.Libelle,
                Confiance = row.Confiance
            };

            ProductHit? hit = null;

            if (!string.IsNullOrWhiteSpace(row.Cip) && byCip.TryGetValue(row.Cip.Trim(), out var cipHit))
            {
                hit = cipHit;
            }
            else if (!string.IsNullOrWhiteSpace(row.Libelle))
            {
                var key = NormalizeName(row.Libelle);
                if (byName.TryGetValue(key, out var nameHit))
                {
                    hit = nameHit;
                }
                else
                {
                    hit = products.FirstOrDefault(p =>
                        NormalizeName(p.CommercialName).Contains(key, StringComparison.Ordinal)
                        || key.Contains(NormalizeName(p.CommercialName), StringComparison.Ordinal));
                }
            }

            if (hit != null)
            {
                matched++;
                line.Matched = true;
                line.ProductId = hit.Id;
                line.ProductText = FormatProductText(hit.CommercialName, hit.Cip, hit.StockQuantity);
                line.PurchasePrice = hit.PurchasePrice;
                line.SalePrice = hit.SalePrice;
                line.TauxTVA ??= hit.AssujettiTVA ? hit.TauxTVA : 0m;

                if (line.PrixAchat <= 0 && hit.PurchasePrice > 0)
                    line.PrixAchat = hit.PurchasePrice;
                if (line.PrixVente <= 0 && hit.SalePrice > 0)
                    line.PrixVente = hit.SalePrice;
            }
            else
            {
                unmatched++;
                line.Matched = false;
                var hint = !string.IsNullOrWhiteSpace(row.Cip)
                    ? $"CIP {row.Cip}"
                    : (row.Libelle ?? "ligne");
                line.Warning = $"Produit introuvable ({hint}). Choisissez-le manuellement.";
                if (!string.IsNullOrWhiteSpace(row.Libelle))
                    line.ProductText = row.Libelle.Trim();
            }

            lines.Add(line);
        }

        return new BlImportPreviewResult
        {
            Ok = true,
            Message = unmatched == 0
                ? $"{matched} ligne(s) prêtes. Vérifiez le résumé puis Enregistrer."
                : $"{matched} trouvé(s), {unmatched} à rattacher manuellement.",
            MatchedCount = matched,
            UnmatchedCount = unmatched,
            Lines = lines
        };
    }

    private static string FormatProductText(string name, string? cip, int stock)
    {
        var label = !string.IsNullOrWhiteSpace(cip) ? $"{cip} — {name}" : name;
        return $"{label} (stock: {stock})";
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static List<BlImportRawRow> ReadPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var pageRows = new List<(double Y, List<(string Text, double X)> Words)>();

        foreach (var page in document.GetPages())
        {
            var words = page.GetWords().ToList();
            if (words.Count == 0)
                continue;

            // Grouper les mots par ligne (Y proche)
            var groups = new List<List<Word>>();
            foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
            {
                var y = word.BoundingBox.Bottom;
                var group = groups.FirstOrDefault(g =>
                    Math.Abs(g[0].BoundingBox.Bottom - y) <= 3.0);
                if (group == null)
                {
                    group = new List<Word>();
                    groups.Add(group);
                }
                group.Add(word);
            }

            foreach (var group in groups)
            {
                var ordered = group.OrderBy(w => w.BoundingBox.Left).ToList();
                pageRows.Add((
                    ordered[0].BoundingBox.Bottom,
                    ordered.Select(w => (w.Text.Trim(), w.BoundingBox.Left)).Where(t => t.Item1.Length > 0).ToList()
                ));
            }
        }

        if (pageRows.Count == 0)
        {
            throw new InvalidOperationException(
                "PDF sans texte extractible (probablement scanné). Exportez en Excel/CSV ou utilisez un PDF texte.");
        }

        // 1) Tentative tableau avec en-tête reconnu
        var tableRows = TryParsePdfTable(pageRows);
        if (tableRows.Count > 0)
            return tableRows;

        // 2) Fallback : lignes contenant un CIP (7–13 chiffres) + nombres
        var fallback = TryParsePdfByCipPattern(pageRows);
        if (fallback.Count > 0)
            return fallback;

        throw new InvalidOperationException(
            "PDF lu mais aucune ligne produit détectée. Vérifiez que le BL contient CIP/libellé en texte, ou utilisez Excel/CSV.");
    }

    private static List<BlImportRawRow> TryParsePdfTable(
        List<(double Y, List<(string Text, double X)> Words)> pageRows)
    {
        var aliasToCanonical = BuildAliasMap();
        Dictionary<string, double>? columnXs = null;
        var headerIndex = -1;

        for (var i = 0; i < pageRows.Count; i++)
        {
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var (text, x) in pageRows[i].Words)
            {
                var norm = NormalizeHeader(text);
                if (norm.Length == 0)
                    continue;
                if (aliasToCanonical.TryGetValue(norm, out var canonical) && !map.ContainsKey(canonical))
                    map[canonical] = x;
            }

            if (map.ContainsKey("CIP") || map.ContainsKey("LIBELLE"))
            {
                columnXs = map;
                headerIndex = i;
                break;
            }
        }

        if (columnXs == null || headerIndex < 0)
            return new List<BlImportRawRow>();

        var rows = new List<BlImportRawRow>();
        for (var i = headerIndex + 1; i < pageRows.Count; i++)
        {
            var cells = AssignWordsToColumns(pageRows[i].Words, columnXs);
            var cip = cells.GetValueOrDefault("CIP");
            var libelle = cells.GetValueOrDefault("LIBELLE");
            if (string.IsNullOrWhiteSpace(cip) && string.IsNullOrWhiteSpace(libelle))
                continue;

            // Ignorer les totaux / pieds de page
            var joined = string.Join(' ', pageRows[i].Words.Select(w => w.Text)).ToUpperInvariant();
            if (joined.Contains("TOTAL") || joined.Contains("PAGE ") || joined.Contains("SOUS-TOTAL"))
                continue;

            var (estUg, nbUg) = ParseUgField(cells.GetValueOrDefault("UG"));
            rows.Add(new BlImportRawRow
            {
                RowNumber = i + 1,
                Cip = EmptyToNull(cip),
                Libelle = EmptyToNull(libelle),
                Quantite = ParseInt(cells.GetValueOrDefault("QTE")),
                PrixAchat = ParseDecimal(cells.GetValueOrDefault("PRIX_ACHAT")),
                PrixVente = ParseDecimal(cells.GetValueOrDefault("PRIX_VENTE")),
                NumeroLot = EmptyToNull(cells.GetValueOrDefault("LOT")),
                DatePeremption = ParseDate(cells.GetValueOrDefault("PEREMPTION")),
                EstUG = estUg,
                NbUG = nbUg,
                TauxTVA = ParseDecimal(cells.GetValueOrDefault("TVA"))
            });
        }

        return rows;
    }

    private static Dictionary<string, string> AssignWordsToColumns(
        List<(string Text, double X)> words,
        Dictionary<string, double> columnXs)
    {
        var buckets = columnXs.Keys.ToDictionary(k => k, _ => new StringBuilder(), StringComparer.OrdinalIgnoreCase);
        var centers = columnXs.ToList();

        foreach (var (text, x) in words)
        {
            var best = centers
                .OrderBy(c => Math.Abs(c.Value - x))
                .First();
            // Écarter si trop loin du début de colonne (mot hors tableau)
            if (Math.Abs(best.Value - x) > 120)
                continue;
            var sb = buckets[best.Key];
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(text);
        }

        return buckets.ToDictionary(kv => kv.Key, kv => kv.Value.ToString().Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static List<BlImportRawRow> TryParsePdfByCipPattern(
        List<(double Y, List<(string Text, double X)> Words)> pageRows)
    {
        var cipRegex = new Regex(@"\b(\d{7,13})\b", RegexOptions.Compiled);
        var rows = new List<BlImportRawRow>();
        var rowNum = 0;

        foreach (var (_, words) in pageRows)
        {
            rowNum++;
            var line = string.Join(' ', words.Select(w => w.Text));
            var m = cipRegex.Match(line);
            if (!m.Success)
                continue;

            var cip = m.Groups[1].Value;
            // Éviter d'importer des totaux / dates comme CIP
            if (cip.Length == 8 && line.Contains('/'))
                continue;

            var numbers = Regex.Matches(line, @"\d+(?:[.,]\d+)?")
                .Select(x => x.Value)
                .Where(v => v != cip)
                .Select(ParseDecimal)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            // Heuristique : 1er entier raisonnable = qté, puis prix
            int? qty = null;
            decimal? pa = null;
            decimal? pv = null;
            foreach (var n in numbers)
            {
                if (qty == null && n is >= 1 and <= 10_000 && n == Math.Truncate(n))
                {
                    qty = (int)n;
                    continue;
                }
                if (pa == null && n >= 0)
                {
                    pa = n;
                    continue;
                }
                if (pv == null && n >= 0)
                {
                    pv = n;
                    break;
                }
            }

            var libelle = line;
            libelle = cipRegex.Replace(libelle, " ", 1).Trim();
            libelle = Regex.Replace(libelle, @"\s+", " ");
            if (libelle.Length > 120)
                libelle = libelle[..120];

            rows.Add(new BlImportRawRow
            {
                RowNumber = rowNum,
                Cip = cip,
                Libelle = EmptyToNull(libelle),
                Quantite = qty,
                PrixAchat = pa,
                PrixVente = pv
            });
        }

        return rows;
    }

    private static Dictionary<string, string> BuildAliasMap()
    {
        var aliasToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (canonical, aliases) in HeaderAliases)
        {
            foreach (var a in aliases)
                aliasToCanonical[NormalizeHeader(a)] = canonical;
        }
        return aliasToCanonical;
    }

    private static List<BlImportRawRow> ReadExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.LastRowUsed() != null)
            ?? throw new InvalidOperationException("Le fichier Excel est vide.");

        var map = FindHeaderMap(worksheet, out var headerRow);
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;
        var rows = new List<BlImportRawRow>();

        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var row = worksheet.Row(r);
            var cip = GetCellText(row, map, "CIP");
            var libelle = GetCellText(row, map, "LIBELLE");
            if (string.IsNullOrWhiteSpace(cip) && string.IsNullOrWhiteSpace(libelle))
                continue;

            var ugText = GetCellText(row, map, "UG");
            var (estUg, nbUg) = ParseUgField(ugText);
            if (!estUg && GetCellBool(row, map, "UG"))
            {
                estUg = true;
                nbUg = GetCellInt(row, map, "UG") is > 0 and var n ? n : 1;
            }
            else if (GetCellInt(row, map, "UG") is > 0 and var nUg)
            {
                estUg = true;
                nbUg = nUg;
            }

            rows.Add(new BlImportRawRow
            {
                RowNumber = r,
                Cip = cip,
                Libelle = libelle,
                Quantite = GetCellInt(row, map, "QTE"),
                PrixAchat = GetCellDecimal(row, map, "PRIX_ACHAT"),
                PrixVente = GetCellDecimal(row, map, "PRIX_VENTE"),
                NumeroLot = GetCellText(row, map, "LOT"),
                DatePeremption = GetCellDate(row, map, "PEREMPTION"),
                EstUG = estUg,
                NbUG = nbUg,
                TauxTVA = GetCellDecimal(row, map, "TVA")
            });
        }

        return rows;
    }

    private static List<BlImportRawRow> ReadCsv(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        try
        {
            return ReadCsvWithEncoding(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        }
        catch (Exception)
        {
            if (!stream.CanSeek)
                throw;
            stream.Position = 0;
            return ReadCsvWithEncoding(stream, Encoding.Latin1);
        }
    }

    private static List<BlImportRawRow> ReadCsvWithEncoding(Stream stream, Encoding encoding)
    {
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line != null)
                lines.Add(line);
        }

        if (lines.Count == 0)
            throw new InvalidOperationException("Le fichier CSV est vide.");

        var sep = lines[0].Count(c => c == ';') >= lines[0].Count(c => c == ',') ? ';' : ',';
        var headerCells = SplitCsvLine(lines[0], sep);
        var map = MapHeaderCells(headerCells);
        if (!map.ContainsKey("CIP") && !map.ContainsKey("LIBELLE"))
            throw new InvalidOperationException("Colonnes attendues : au moins CIP ou LIBELLE (PRODUIT).");

        var rows = new List<BlImportRawRow>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;
            var cells = SplitCsvLine(lines[i], sep);
            string? Cell(string key) =>
                map.TryGetValue(key, out var idx) && idx < cells.Length ? cells[idx].Trim() : null;

            var cip = Cell("CIP");
            var libelle = Cell("LIBELLE");
            if (string.IsNullOrWhiteSpace(cip) && string.IsNullOrWhiteSpace(libelle))
                continue;

            var (estUg, nbUg) = ParseUgField(Cell("UG"));
            rows.Add(new BlImportRawRow
            {
                RowNumber = i + 1,
                Cip = EmptyToNull(cip),
                Libelle = EmptyToNull(libelle),
                Quantite = ParseInt(Cell("QTE")),
                PrixAchat = ParseDecimal(Cell("PRIX_ACHAT")),
                PrixVente = ParseDecimal(Cell("PRIX_VENTE")),
                NumeroLot = EmptyToNull(Cell("LOT")),
                DatePeremption = ParseDate(Cell("PEREMPTION")),
                EstUG = estUg,
                NbUG = nbUg,
                TauxTVA = ParseDecimal(Cell("TVA"))
            });
        }

        return rows;
    }

    /// <summary>UG peut être un booléen (oui) ou un nombre d'unités gratuites.</summary>
    private static (bool EstUG, int? NbUG) ParseUgField(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (false, null);

        var n = ParseInt(text);
        if (n is > 0)
            return (true, n);

        if (ParseBool(text))
            return (true, 1);

        return (false, null);
    }

    private static Dictionary<string, int> FindHeaderMap(IXLWorksheet worksheet, out int headerRow)
    {
        var lastSearch = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 1, 50);
        for (var r = 1; r <= lastSearch; r++)
        {
            var map = MapHeaderRow(worksheet.Row(r));
            if (map.ContainsKey("CIP") || map.ContainsKey("LIBELLE"))
            {
                headerRow = r;
                return map;
            }
        }

        throw new InvalidOperationException(
            "En-tête introuvable. Colonnes attendues : CIP et/ou LIBELLE, QTE, PRIX_ACHAT (ou PX_FAB), PPH…");
    }

    private static Dictionary<string, int> MapHeaderRow(IXLRow row)
    {
        var lastCol = row.LastCellUsed()?.Address.ColumnNumber ?? 0;
        var cells = new List<string>();
        for (var c = 1; c <= lastCol; c++)
            cells.Add(NormalizeHeader(row.Cell(c).GetString()));
        return MapHeaderCells(cells);
    }

    private static Dictionary<string, int> MapHeaderCells(IReadOnlyList<string> cells)
    {
        var aliasToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (canonical, aliases) in HeaderAliases)
        {
            foreach (var a in aliases)
                aliasToCanonical[NormalizeHeader(a)] = canonical;
        }

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < cells.Count; i++)
        {
            var h = NormalizeHeader(cells[i]);
            if (h.Length == 0)
                continue;
            if (aliasToCanonical.TryGetValue(h, out var canonical) && !result.ContainsKey(canonical))
                result[canonical] = i;
        }

        return result;
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var folded = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in folded.ToUpperInvariant())
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            if (ch is '%' or '°')
            {
                sb.Append(ch);
                continue;
            }
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else if (ch is '_' or '-' or ' ')
                sb.Append('_');
        }
        return sb.ToString().Trim('_');
    }

    private static string? GetCellText(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var idx0))
            return null;
        var cell = row.Cell(idx0 + 1);
        if (cell.IsEmpty())
            return null;
        if (key == "CIP")
        {
            var raw = cell.DataType == XLDataType.Number
                ? cell.GetFormattedString()
                : cell.GetString();
            raw = raw?.Trim();
            return string.IsNullOrEmpty(raw) ? null : raw;
        }
        var text = cell.GetString().Trim();
        if (text.Length == 0)
            text = cell.GetFormattedString().Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static int? GetCellInt(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var idx0))
            return null;
        var cell = row.Cell(idx0 + 1);
        if (cell.IsEmpty())
            return null;
        if (cell.DataType == XLDataType.Number)
            return (int)Math.Truncate(cell.GetDouble());
        return ParseInt(cell.GetString());
    }

    private static decimal? GetCellDecimal(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var idx0))
            return null;
        var cell = row.Cell(idx0 + 1);
        if (cell.IsEmpty())
            return null;
        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();
        return ParseDecimal(cell.GetString());
    }

    private static DateTime? GetCellDate(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var idx0))
            return null;
        var cell = row.Cell(idx0 + 1);
        if (cell.IsEmpty())
            return null;
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().Date;
        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out DateTime dt))
            return dt.Date;
        return ParseDate(cell.GetString());
    }

    private static bool GetCellBool(IXLRow row, Dictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var idx0))
            return false;
        var cell = row.Cell(idx0 + 1);
        if (cell.IsEmpty())
            return false;
        if (cell.DataType == XLDataType.Boolean)
            return cell.GetBoolean();
        if (cell.DataType == XLDataType.Number)
            return cell.GetDouble() != 0;
        return ParseBool(cell.GetString());
    }

    private static string[] SplitCsvLine(string line, char sep)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (ch == sep && !inQuotes)
            {
                list.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(ch);
        }
        list.Add(sb.ToString());
        return list.ToArray();
    }

    private static string? EmptyToNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static int? ParseInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a))
            return a;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.GetCultureInfo("fr-FR"), out var b))
            return b;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Truncate(d);
        return null;
    }

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Trim().Replace(" ", "").Replace("FCFA", "", StringComparison.OrdinalIgnoreCase);
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var a))
            return a;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("fr-FR"), out var b))
            return b;
        return null;
    }

    private static DateTime? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Trim();
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy", "yyyy/MM/dd"];
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.Date;
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out dt))
            return dt.Date;
        return null;
    }

    private static bool ParseBool(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        text = text.Trim().ToLowerInvariant();
        return text is "1" or "true" or "oui" or "o" or "x" or "ug" or "yes" or "vrai";
    }

    public static string DetecterFournisseur(string texte)
    {
        if (string.IsNullOrEmpty(texte))
            return "Inconnu";

        if (texte.Contains("UbiPharm", StringComparison.OrdinalIgnoreCase)
            || texte.Contains("UBIPHARM", StringComparison.OrdinalIgnoreCase)
            || texte.Contains("DEL/", StringComparison.OrdinalIgnoreCase)
            || texte.Contains("BEL/", StringComparison.OrdinalIgnoreCase))
            return "UbiPharm";

        if (texte.Contains("SODIPHARM", StringComparison.OrdinalIgnoreCase)
            || texte.Contains("BORDEREAU DE LIVRAISON", StringComparison.OrdinalIgnoreCase))
            return "Sodipharm";

        return "Inconnu";
    }

    public static string ExtrairNumeroBL(string texte, string fournisseur)
    {
        if (string.IsNullOrEmpty(texte))
            return "";

        if (fournisseur == "Sodipharm")
        {
            var match = Regex.Match(texte, @"N[°o]\s*(\d{9,12}\s*\d{3})", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value.Replace(" ", "");
        }

        if (fournisseur == "UbiPharm")
        {
            var match = Regex.Match(texte, @"(DEL/\d+|BEL/\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return "";
    }

    public static DateTime? ExtraireDate(string texte)
    {
        if (string.IsNullOrEmpty(texte))
            return null;

        var match = Regex.Match(texte, @"(\d{2}/\d{2}/\d{4})");
        if (match.Success
            && DateTime.TryParseExact(
                match.Groups[1].Value,
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            return date.Date;

        match = Regex.Match(texte, @"(\d{2}/\d{2}/\d{2})(?!\d)");
        if (match.Success
            && DateTime.TryParseExact(
                match.Groups[1].Value,
                "dd/MM/yy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
            return date.Date;

        return null;
    }

    public static string ExtraireTextePdf(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }

    public static async Task<string> ExtraireTexteOCR(
        byte[] pdfBytes,
        string endpoint,
        string apiKey)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configuration Azure Vision manquante.");

        var root = endpoint.Trim().TrimEnd('/');
        Exception? lastError = null;

        try
        {
            var text = await AppelerImageAnalysisAsync(
                pdfBytes, "application/pdf", root, apiKey);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }
        catch (Exception ex)
        {
            lastError = ex;
        }

        try
        {
            var text = await AppelerReadApiPdfAsync(pdfBytes, root, apiKey);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }
        catch (Exception ex)
        {
            lastError = ex;
        }

        var images = ExtraireImagesPdf(pdfBytes);
        if (images.Count == 0)
        {
            throw lastError ?? new InvalidOperationException(
                "OCR impossible : PDF sans texte extractible ni image embarquée.");
        }

        var sb = new StringBuilder();
        foreach (var image in images)
        {
            var pageText = await AppelerImageAnalysisAsync(
                image, "application/octet-stream", root, apiKey);
            if (!string.IsNullOrWhiteSpace(pageText))
                sb.AppendLine(pageText);
        }

        var combined = sb.ToString().Trim();
        if (combined.Length == 0)
            throw lastError ?? new InvalidOperationException("Azure Vision n'a renvoyé aucun texte.");

        return combined;
    }

    private static List<byte[]> ExtraireImagesPdf(byte[] pdfBytes)
    {
        var list = new List<byte[]>();
        using var stream = new MemoryStream(pdfBytes, writable: false);
        using var pdf = PdfDocument.Open(stream);
        foreach (var page in pdf.GetPages())
        {
            foreach (var image in page.GetImages())
            {
                var raw = image.RawBytes;
                if (raw == null || raw.Count == 0)
                    continue;
                list.Add(raw as byte[] ?? raw.ToArray());
            }
        }

        return list;
    }

    private static async Task<string> AppelerImageAnalysisAsync(
        byte[] bytes,
        string contentType,
        string endpointRoot,
        string apiKey)
    {
        var url = endpointRoot
            + "/computervision/imageanalysis:analyze"
            + "?api-version=2024-02-01&features=read";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await OcrHttp.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure Vision error ({(int)response.StatusCode}): {payload}");

        return ParserReponseOcr(payload);
    }

    private static async Task<string> AppelerReadApiPdfAsync(
        byte[] pdfBytes,
        string endpointRoot,
        string apiKey)
    {
        var url = endpointRoot + "/vision/v3.2/read/analyze";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        request.Content = new ByteArrayContent(pdfBytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        using var response = await OcrHttp.SendAsync(request);
        if (response.StatusCode != System.Net.HttpStatusCode.Accepted
            && !response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Azure Read API error ({(int)response.StatusCode}): {error}");
        }

        var operationUrl = response.Headers.TryGetValues("Operation-Location", out var values)
            ? values.FirstOrDefault()
            : null;
        if (string.IsNullOrEmpty(operationUrl))
        {
            var body = await response.Content.ReadAsStringAsync();
            var immediate = ParserReponseOcr(body);
            if (!string.IsNullOrWhiteSpace(immediate))
                return immediate;
            throw new InvalidOperationException("Azure Read API : Operation-Location manquante.");
        }

        for (var i = 0; i < 40; i++)
        {
            await Task.Delay(500);
            using var poll = new HttpRequestMessage(HttpMethod.Get, operationUrl);
            poll.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            using var pollResponse = await OcrHttp.SendAsync(poll);
            var json = await pollResponse.Content.ReadAsStringAsync();
            if (!pollResponse.IsSuccessStatusCode)
                throw new InvalidOperationException($"Azure Read poll error: {json}");

            using var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.TryGetProperty("status", out var st)
                ? st.GetString()
                : "";
            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Azure Read API : analyse échouée.");
            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase)
                || !doc.RootElement.TryGetProperty("status", out _))
                return ParserReponseOcr(json);
        }

        throw new TimeoutException("Azure Read API : délai d'attente dépassé.");
    }

    private static string ParserReponseOcr(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var sb = new StringBuilder();

        if (root.TryGetProperty("readResult", out var readResult)
            && readResult.TryGetProperty("blocks", out var blocks))
        {
            foreach (var block in blocks.EnumerateArray())
            {
                if (!block.TryGetProperty("lines", out var lines))
                    continue;
                foreach (var line in lines.EnumerateArray())
                {
                    if (line.TryGetProperty("text", out var text))
                        sb.AppendLine(text.GetString());
                }
            }
        }

        if (sb.Length == 0
            && root.TryGetProperty("analyzeResult", out var analyzeResult)
            && analyzeResult.TryGetProperty("readResults", out var readResults))
        {
            foreach (var page in readResults.EnumerateArray())
            {
                if (!page.TryGetProperty("lines", out var lines))
                    continue;
                foreach (var line in lines.EnumerateArray())
                {
                    if (line.TryGetProperty("text", out var text))
                        sb.AppendLine(text.GetString());
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>PDF Sodipharm généré : ligne GEO + qté, nom et code sur la même ligne ou les suivantes.</summary>
    public static List<BLLigneExtraite> ParserSodipharmBordereau(string texte)
    {
        var result = new List<BLLigneExtraite>();
        if (string.IsNullOrWhiteSpace(texte))
            return result;

        var lines = texte.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var geoRx = new Regex(
            @"^\s*\d{1,2}\.\s*[A-Z]\s*\.\s*\S+\s+(\d{1,4})\s+(\d{1,4})(?:\s+(.*))?$");
        var cipRx = new Regex(@"^\s*(\d{7})\s*$");
        var cipInText = new Regex(@"\b(\d{7})\b");

        for (var i = 0; i < lines.Length; i++)
        {
            var geo = geoRx.Match(lines[i].TrimEnd());
            if (!geo.Success)
                continue;

            int.TryParse(geo.Groups[1].Value, out var qtCde);
            int.TryParse(geo.Groups[2].Value, out var qtLiv);
            var suite = geo.Groups[3].Success ? geo.Groups[3].Value.Trim() : "";

            string? nom = null;
            string? cip = null;
            var montants = new List<int>();

            if (suite.Length > 0)
            {
                var cipM = cipInText.Match(suite);
                if (cipM.Success)
                {
                    cip = cipM.Groups[1].Value;
                    nom = Regex.Replace(suite[..cipM.Index].Trim(), @"\s+", " ");
                    foreach (Match n in Regex.Matches(suite[cipM.Index..], @"\b(\d{3,6})\b"))
                    {
                        if (int.TryParse(n.Groups[1].Value, out var v) && v is >= 50 and <= 1_000_000 && n.Groups[1].Value != cip)
                            montants.Add(v);
                    }
                }
                else
                {
                    nom = Regex.Replace(suite, @"\s+", " ");
                }
            }

            for (var j = i + 1; j < lines.Length && j <= i + 12; j++)
            {
                if (geoRx.IsMatch(lines[j].TrimEnd()))
                    break;
                var t = lines[j].Trim();
                if (t.Length == 0)
                    continue;
                if (nom == null && Regex.IsMatch(t, @"^[A-ZÀ-Üa-z].{4,}"))
                {
                    nom = Regex.Replace(t, @"\s+", " ");
                    continue;
                }

                var cipSeul = cipRx.Match(t);
                if (cipSeul.Success)
                {
                    cip ??= cipSeul.Groups[1].Value;
                    continue;
                }

                var digits = t.Replace(" ", "").Replace(".", "");
                if (int.TryParse(digits, out var n) && n is >= 50 and <= 1_000_000)
                    montants.Add(n);
            }

            if (string.IsNullOrWhiteSpace(cip) && string.IsNullOrWhiteSpace(nom))
                continue;
            if (string.IsNullOrWhiteSpace(nom) || nom.Equals("T", StringComparison.OrdinalIgnoreCase))
                continue;

            var prixAchat = montants.Count > 0 ? montants.Min() : 0m;
            var prixPub = montants.Count > 0 ? montants.Max() : 0m;
            var rupture = qtLiv == 0
                || nom.Contains("PAS DE SUIVI", StringComparison.OrdinalIgnoreCase);

            result.Add(new BLLigneExtraite
            {
                CIP = cip ?? "",
                NomProduit = nom,
                PrixAchat = prixAchat,
                PrixVente = prixPub == prixAchat ? 0 : prixPub,
                QuantiteLivree = rupture ? 0 : qtLiv,
                Confiance = rupture ? "rupture" : "bonne"
            });
        }

        return result;
    }

    /// <summary>PDF UbiPharm généré (FACTURE BEL/…) : n° ligne + code + libellé + prix au format 2.499 (= 2499).</summary>
    public static List<BLLigneExtraite> ParserUbiPharmFactureGeneree(string texte)
    {
        var result = new List<BLLigneExtraite>();
        if (string.IsNullOrWhiteSpace(texte))
            return result;

        var lines = texte.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var productIdx = new List<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (EstLigneProduitUbiFacture(lines[i]))
                productIdx.Add(i);
        }

        var lotRx = new Regex(@"LOT\s+(\S+)\s+PER\.?\s*(\d{2}/\d{2}/\d{2,4})", RegexOptions.IgnoreCase);
        foreach (var (start, a) in productIdx.Select((idx, n) => (idx, n)))
        {
            if (!TryParseUbiFactureLigne(lines[start], out var parsed))
                continue;

            var next = a + 1 < productIdx.Count ? productIdx[a + 1] : lines.Length;
            var look = new List<string>();
            for (var j = start + 1; j < next; j++)
            {
                var t = lines[j].Trim();
                if (t.Length > 0)
                    look.Add(t);
            }

            var lookText = string.Join(' ', look);
            var ean13 = Regex.Match(lookText, @"\b(\d{13})\b");
            var cip = ean13.Success
                ? ean13.Groups[1].Value
                : (Regex.IsMatch(parsed.Code, @"^\d{7,13}$") ? parsed.Code : parsed.Code);

            var lots = lotRx.Matches(lookText);
            if (lots.Count == 0)
            {
                result.Add(CreerLigneUbiPharm(
                    cip, parsed.Nom, parsed.PrixAchat, parsed.QteLiv, null, null, "bonne"));
                result[^1].PrixVente = parsed.PrixPublic ?? 0;
                continue;
            }

            foreach (Match lot in lots)
            {
                DateTime? peremp = null;
                if (DateTime.TryParseExact(
                        lot.Groups[2].Value,
                        ["dd/MM/yy", "dd/MM/yyyy"],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var d)
                    && d != default)
                    peremp = d.Date;

                result.Add(CreerLigneUbiPharm(
                    cip,
                    parsed.Nom,
                    parsed.PrixAchat,
                    parsed.QteLiv,
                    lot.Groups[1].Value.Trim(),
                    peremp,
                    "bonne"));
                result[^1].PrixVente = parsed.PrixPublic ?? 0;
            }
        }

        return result;
    }

    private static bool EstLigneProduitUbiFacture(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;
        var t = line.Trim();
        if (t.StartsWith("LOT ", StringComparison.OrdinalIgnoreCase))
            return false;
        if (t.Contains("F A C T U R E", StringComparison.OrdinalIgnoreCase))
            return false;
        return Regex.IsMatch(t, @"^\d{1,3}\s+\S{3,}\s+.+\d{1,3}[.,]\d{3}\s*$");
    }

    private readonly record struct UbiFactureParse(
        string Code, string Nom, int QteLiv, decimal PrixAchat, decimal? PrixPublic);

    private static bool TryParseUbiFactureLigne(string line, out UbiFactureParse parsed)
    {
        parsed = default;
        var parts = Regex.Split(line.Trim(), @"\s+")
            .Where(p => p.Length > 0 && p != "A")
            .ToList();
        if (parts.Count < 6)
            return false;
        if (!int.TryParse(parts[0], out var num) || num is < 1 or > 999)
            return false;
        if (!TryParseMontantMilliersUbi(parts[^1], out _))
            return false;
        if (!TryParseMontantMilliersUbi(parts[^2], out var pu))
            return false;
        if (!int.TryParse(parts[^3], out var qteLiv) || qteLiv is < 0 or > 999)
            return false;

        var nameParts = parts.Skip(2).Take(parts.Count - 6).ToList();
        decimal? pub = null;
        if (nameParts.Count > 0 && TryParseMontantMilliersUbi(nameParts[^1], out var pPub))
        {
            pub = pPub;
            nameParts.RemoveAt(nameParts.Count - 1);
        }

        if (nameParts.Count > 0
            && int.TryParse(nameParts[^1], out var maybeTva)
            && maybeTva is 5 or 10 or 18)
            nameParts.RemoveAt(nameParts.Count - 1);

        var nom = string.Join(' ', nameParts).Trim();
        if (nom.Length < 3)
            return false;

        parsed = new UbiFactureParse(parts[1], nom, qteLiv, pu, pub);
        return true;
    }

    /// <summary>2.499 ou 2,499 = 2499 FCFA (milliers), pas 2,499.</summary>
    private static bool TryParseMontantMilliersUbi(string token, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(token))
            return false;
        if (!Regex.IsMatch(token, @"^\d{1,3}(?:[.,]\d{3})+$"))
            return false;
        var digits = token.Replace(".", "").Replace(",", "");
        return decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public static List<BLLigneExtraite> ParserSodipharm(string texte)
    {
        var bordereau = ParserSodipharmBordereau(texte);
        if (bordereau.Count > 0)
            return bordereau;
        var result = new List<BLLigneExtraite>();
        if (string.IsNullOrWhiteSpace(texte))
            return result;

        var lines = texte.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var cipDebut = new Regex(@"^\s*(\d{7})(?:\s+(.*))?$");
        var nomRx = new Regex(@"^[A-Z][A-Z0-9\s+\-./%]{3,}$", RegexOptions.IgnoreCase);
        var prixRx = new Regex(@"[\d]+[,.][\d]{2,3}");
        var perempRx = new Regex(@"\b(\d{2})[-/](\d{2})\b");

        var anchors = new List<(int Index, string Cip, string? Remainder)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var m = cipDebut.Match(lines[i]);
            if (m.Success)
                anchors.Add((i, m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value.Trim() : null));
        }

        for (var a = 0; a < anchors.Count; a++)
        {
            var start = anchors[a].Index;
            var next = a + 1 < anchors.Count ? anchors[a + 1].Index : lines.Length;
            var blockLines = lines.Skip(start).Take(Math.Max(0, next - start)).ToArray();
            var blockText = string.Join('\n', blockLines);
            var cip = anchors[a].Cip;

            var qtes = ExtraireEntiersCourtsApresCip(blockLines, cip);
            var qtCde = qtes.Count > 0 ? qtes[0] : (int?)null;
            var qtLiv = qtes.Count > 1 ? qtes[1] : qtCde;

            var rupture = blockText.Contains("PAS DE SUIVI DES MANQUANTS", StringComparison.OrdinalIgnoreCase)
                || qtLiv == 0;
            if (rupture)
                qtLiv = 0;

            var nom = ExtraireNomProduitBl(blockLines, anchors[a].Remainder, nomRx);
            var montants = ExtraireMontants(blockText, prixRx);
            var horsTva = montants.Where(p => p is not (5.5m or 10m or 18m)).ToList();
            var prixAchat = horsTva.Count > 0 ? horsTva.Min() : 0m;
            var prixPub = horsTva.Count > 0 ? horsTva.Max() : 0m;
            decimal? tva = ExtraireTvaImprimee(blockText);
            if (tva is null && montants.Any(p => p is 5.5m or 10m or 18m))
                tva = montants.First(p => p is 5.5m or 10m or 18m);

            DateTime? peremp = ExtrairePeremptionMmAa(blockText, perempRx);
            string confiance;
            if (rupture)
                confiance = "rupture";
            else if (peremp.HasValue)
                confiance = "partielle";
            else
                confiance = "bonne";

            result.Add(new BLLigneExtraite
            {
                CIP = cip,
                NomProduit = nom,
                PrixAchat = prixAchat,
                PrixVente = prixPub == prixAchat ? 0 : prixPub,
                TauxTVA = tva,
                QuantiteLivree = qtLiv,
                DatePeremption = peremp,
                Confiance = confiance
            });
        }

        return result;
    }

    public static List<BLLigneExtraite> ParserUbiPharm(string texte)
    {
        var facture = ParserUbiPharmFactureGeneree(texte);
        if (facture.Count >= 3)
            return facture;
        var result = new List<BLLigneExtraite>();
        if (string.IsNullOrWhiteSpace(texte))
            return result;

        var lines = texte.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var cipSeul = new Regex(@"^\s*(\d{7})\s*$");
        var cipDebut = new Regex(@"^\s*(\d{7})\s+(.+)$");
        var nomRx = new Regex(@"^[A-Z][A-Z0-9\s+\-./%]{4,}$", RegexOptions.IgnoreCase);
        var lotRx = new Regex(@"LOT\s+(\S+)\s+PER\.?\s*(\d{2}/\d{2}/\d{2,4})", RegexOptions.IgnoreCase);
        var prixRx = new Regex(@"[\d]+[,.][\d]{2,3}");

        var anchors = new List<(int Index, string Cip, string? Remainder)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var seul = cipSeul.Match(lines[i]);
            if (seul.Success)
            {
                anchors.Add((i, seul.Groups[1].Value, null));
                continue;
            }

            var debut = cipDebut.Match(lines[i]);
            if (debut.Success)
                anchors.Add((i, debut.Groups[1].Value, debut.Groups[2].Value.Trim()));
        }

        for (var a = 0; a < anchors.Count; a++)
        {
            var start = anchors[a].Index;
            var next = a + 1 < anchors.Count ? anchors[a + 1].Index : lines.Length;
            var blockLines = lines.Skip(start).Take(Math.Max(0, next - start)).ToArray();
            var blockText = string.Join('\n', blockLines);

            var nom = ExtraireNomProduitBl(blockLines, anchors[a].Remainder, nomRx);
            var prix = ExtrairePrixUnitaireUbiPharm(blockText, prixRx);
            var (qteLivree, depuisMontant) = ExtraireQuantiteUbiPharm(blockLines, blockText, anchors[a].Cip, prix, prixRx);
            var confiance = depuisMontant && qteLivree is > 0
                ? "bonne"
                : "partielle";
            if (!qteLivree.HasValue)
                confiance = "partielle";

            var lots = lotRx.Matches(blockText);
            if (lots.Count == 0)
            {
                result.Add(CreerLigneUbiPharm(anchors[a].Cip, nom, prix, qteLivree, null, null, confiance));
                continue;
            }

            foreach (Match lot in lots)
            {
                DateTime? peremp = null;
                if (DateTime.TryParseExact(
                        lot.Groups[2].Value,
                        ["dd/MM/yy", "dd/MM/yyyy"],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var d)
                    && d != default)
                    peremp = d.Date;

                result.Add(CreerLigneUbiPharm(
                    anchors[a].Cip,
                    nom,
                    prix,
                    qteLivree,
                    lot.Groups[1].Value.Trim(),
                    peremp,
                    confiance));
            }
        }

        return result;
    }

    private static bool EstNomLot(string? s) =>
        !string.IsNullOrWhiteSpace(s)
        && s.TrimStart().StartsWith("LOT ", StringComparison.OrdinalIgnoreCase);

    private static string ExtraireNomProduitBl(string[] blockLines, string? rest, Regex nomRx)
    {
        if (!string.IsNullOrWhiteSpace(rest) && !EstNomLot(rest) && nomRx.IsMatch(rest.Trim()))
            return Regex.Replace(rest.Trim(), @"\s+", " ");

        for (var j = 1; j < blockLines.Length; j++)
        {
            var candidate = blockLines[j].Trim();
            if (EstNomLot(candidate))
                continue;
            if (nomRx.IsMatch(candidate))
                return Regex.Replace(candidate, @"\s+", " ");
        }

        if (!string.IsNullOrWhiteSpace(rest) && !EstNomLot(rest))
            return Regex.Replace(rest, @"\s+", " ");
        return "";
    }

    private static decimal ExtrairePrixUnitaireUbiPharm(string blockText, Regex prixRx)
    {
        var candidats = ExtraireMontants(blockText, prixRx);
        if (candidats.Count == 0)
            return 0;

        var horsTva = candidats.Where(p => p is not (5.5m or 10m or 18m)).ToList();
        var pool = horsTva.Count > 0 ? horsTva : candidats;
        return pool.Min();
    }

    private static decimal? ExtraireTvaImprimee(string blockText)
    {
        var m = Regex.Match(blockText, @"\b(5[.,]5|10|18)\b");
        if (!m.Success)
            return null;
        return m.Value.Replace(",", ".") == "5.5" || m.Value == "5,5" ? 5.5m
            : m.Value == "10" ? 10m
            : 18m;
    }

    private static List<decimal> ExtraireMontants(string blockText, Regex prixRx)
    {
        var candidats = new List<decimal>();
        foreach (Match m in prixRx.Matches(blockText))
        {
            var raw = m.Value.Replace(",", ".");
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var p)
                && p is >= 0.5m and <= 50_000m)
                candidats.Add(p);
        }

        return candidats;
    }

    private static (int? Qty, bool FromMontant) ExtraireQuantiteUbiPharm(
        string[] blockLines,
        string blockText,
        string cip,
        decimal prixUnitaire,
        Regex prixRx)
    {
        var montants = ExtraireMontants(blockText, prixRx)
            .Where(p => p is not (5.5m or 10m or 18m))
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        if (prixUnitaire > 0)
        {
            foreach (var montant in montants.Where(m => m > prixUnitaire))
            {
                if (EssayerQteDepuisMontant(montant, prixUnitaire, out var q))
                    return (q, true);
            }
        }

        for (var i = 0; i < montants.Count; i++)
        {
            for (var j = i + 1; j < montants.Count; j++)
            {
                if (EssayerQteDepuisMontant(montants[j], montants[i], out var q))
                    return (q, true);
            }
        }

        return (ExtraireQuantiteManuscriteUbiPharm(blockLines, cip), false);
    }

    private static bool EssayerQteDepuisMontant(decimal montant, decimal prixUnitaire, out int qte)
    {
        qte = 0;
        if (prixUnitaire <= 0 || montant <= prixUnitaire)
            return false;

        var ratio = (double)(montant / prixUnitaire);
        var rounded = Math.Round(ratio, MidpointRounding.AwayFromZero);
        if (rounded is < 1 or > 999)
            return false;
        if (Math.Abs(ratio - rounded) > 0.05)
            return false;

        qte = (int)rounded;
        return true;
    }

    /// <summary>Ne jamais prendre un n° de ligne OCR (1–2 chiffres en début de ligne) pour une quantité.</summary>
    private static int? ExtraireQuantiteManuscriteUbiPharm(string[] blockLines, string cip)
    {
        var isolés = new List<int>();
        foreach (var line in blockLines)
        {
            var sansNumeroLigne = Regex.Replace(line, @"^\s*\d{1,2}(?=\s|$)", "").Trim();
            if (!Regex.IsMatch(sansNumeroLigne, @"^\d{1,4}$"))
                continue;
            if (sansNumeroLigne == cip)
                continue;
            if (!int.TryParse(sansNumeroLigne, out var n) || n is < 1 or > 999)
                continue;
            isolés.Add(n);
        }

        return isolés.Count == 1 ? isolés[0] : null;
    }

    private static List<int> ExtraireEntiersCourtsApresCip(string[] blockLines, string cip)
    {
        var found = new List<int>();
        var first = true;
        foreach (var line in blockLines)
        {
            var src = line;
            if (first)
            {
                var idx = src.IndexOf(cip, StringComparison.Ordinal);
                src = idx >= 0 ? src[(idx + cip.Length)..] : src;
                first = false;
            }

            foreach (Match m in Regex.Matches(src, @"\b(\d{1,3})\b"))
            {
                if (!int.TryParse(m.Groups[1].Value, out var n) || n > 999)
                    continue;
                found.Add(n);
                if (found.Count >= 2)
                    return found;
            }
        }

        return found;
    }

    private static DateTime? ExtrairePeremptionMmAa(string blockText, Regex perempRx)
    {
        var m = perempRx.Match(blockText);
        if (!m.Success)
            return null;
        if (!int.TryParse(m.Groups[1].Value, out var mm) || mm is < 1 or > 12)
            return null;
        if (!int.TryParse(m.Groups[2].Value, out var aa))
            return null;
        var year = aa < 100 ? 2000 + aa : aa;
        try
        {
            return new DateTime(year, mm, DateTime.DaysInMonth(year, mm));
        }
        catch
        {
            return null;
        }
    }

    private static BLLigneExtraite CreerLigneUbiPharm(
        string cip,
        string nom,
        decimal prix,
        int? qteLivree,
        string? numeroLot,
        DateTime? peremp,
        string confiance) =>
        new()
        {
            CIP = cip,
            NomProduit = nom,
            PrixAchat = prix,
            QuantiteLivree = qteLivree,
            NumeroLot = numeroLot,
            DatePeremption = peremp,
            Confiance = qteLivree.HasValue && confiance == "bonne" ? "bonne" : "partielle"
        };

    private sealed record ProductHit(
        int Id,
        string? Cip,
        string CommercialName,
        decimal SalePrice,
        decimal PurchasePrice,
        decimal TauxTVA,
        bool AssujettiTVA,
        int StockQuantity);
}
