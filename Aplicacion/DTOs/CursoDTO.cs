namespace Aplicacion.DTOs;

public class CursoDTO
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Color { get; set; } = "#304BFF";
    public int TareasPendientes { get; set; }

    // Solo para visualización: no se almacenan en SQLite ni cambian el curso.
    public int TotalTareas { get; set; }
    public int TareasCompletadas { get; set; }
    public double Progreso => TotalTareas == 0 ? 0.0 : (double)TareasCompletadas / TotalTareas;
    public string TextoAvance => TotalTareas == 0
        ? "Sin tareas"
        : $"{TareasCompletadas}/{TotalTareas}";

    public string TextoPendientes =>
        TareasPendientes == 0 ? "Sin tareas pendientes"
        : TareasPendientes == 1 ? "1 tarea pendiente"
        : $"{TareasPendientes} tareas pendientes";
}