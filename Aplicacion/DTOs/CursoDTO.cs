namespace Aplicacion.DTOs;

public class CursoDTO
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Color { get; set; } = "#304BFF";
    public int TareasPendientes { get; set; }

    public string TextoPendientes =>
        TareasPendientes == 0 ? "Sin tareas pendientes"
        : TareasPendientes == 1 ? "1 tarea pendiente"
        : $"{TareasPendientes} tareas pendientes";
}