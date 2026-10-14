namespace Aplicacion.DTOs;

public class TareaDTO
{
    public int Id { get; set; }
    public int CursoId { get; set; }
    public string CursoNombre { get; set; } = "";
    public string CursoColor { get; set; } = "#304BFF";
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateTime FechaLimite { get; set; }
    public string Prioridad { get; set; } = "Media";
    public bool Completada { get; set; }
    public int MinutosAviso { get; set; }

    public string FechaTexto => FechaLimite.ToString("dd/MM/yyyy hh:mm tt");
    public bool EstaVencida => !Completada && FechaLimite < DateTime.Now;
    public string PrioridadColor => Prioridad switch
    {
        "Alta" => "#EF4444",
        "Media" => "#F59E0B",
        _ => "#22B981"
    };
}