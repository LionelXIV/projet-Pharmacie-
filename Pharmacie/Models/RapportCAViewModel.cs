namespace Pharmacie.Models;

public class RapportCAViewModel
{
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
    public int NombreVentes { get; set; }
    public decimal CATotal { get; set; }
    public decimal PATotal { get; set; }
    public decimal MargeBrute { get; set; }
    public decimal TauxMarge { get; set; }
    public decimal PanierMoyen { get; set; }
    public decimal TVACollectee { get; set; }
    public decimal CAEspeces { get; set; }
    public decimal CAWave { get; set; }
    public decimal CAOrangeMoney { get; set; }
    public decimal CAAutres { get; set; }
    public List<CAJourViewModel> CAParJour { get; set; } = new();
    public List<CAVendeurViewModel> CAParVendeur { get; set; } = new();
}

public class CAJourViewModel
{
    public DateTime Date { get; set; }
    public decimal CA { get; set; }
    public int NbVentes { get; set; }
}

public class CAVendeurViewModel
{
    public string NomVendeur { get; set; } = "";
    public decimal CA { get; set; }
    public int NbVentes { get; set; }
    public decimal PanierMoyen => NbVentes > 0 ? CA / NbVentes : 0;
}
