namespace Pharmacie.Models;

public class RecapitulatifViewModel
{
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
    public int NombreVentes { get; set; }
    public decimal CATotal { get; set; }
    public decimal PATotal { get; set; }
    public decimal MargeBrute { get; set; }
    public decimal TauxMarge { get; set; }
    public decimal TVACollectee { get; set; }
    public decimal PanierMoyen { get; set; }
    public decimal TotalBons { get; set; }
    public decimal TotalBonsRegle { get; set; }
    public decimal TotalAvoirs { get; set; }
    public List<RecapCategorieVm> CAParCategorie { get; set; } = new();

    public decimal TotalBonsEnAttente => TotalBons - TotalBonsRegle;
}

public class RecapCategorieVm
{
    public string Categorie { get; set; } = "";
    public decimal CA { get; set; }
    public decimal PA { get; set; }
    public decimal Marge { get; set; }
    public int NbArticles { get; set; }
    public decimal TauxMarge => CA > 0 ? Marge / CA * 100 : 0;
}
