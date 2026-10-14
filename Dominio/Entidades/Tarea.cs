namespace Dominio.Entidades;

public class Tarea
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public int CursoId { get; set; }
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateTime FechaLimite { get; set; } = DateTime.Today.AddDays(1);
    public string Prioridad { get; set; } = "Media";   // Alta, Media, Baja
    public bool Completada { get; set; }
    public int MinutosAviso { get; set; } = 1440;      // 1 día antes
}