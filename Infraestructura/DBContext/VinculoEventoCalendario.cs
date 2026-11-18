using SQLite;

namespace Infraestructura.DBContext;

// HU15: relaciona una tarea concreta y un perfil local con
// el evento creado por Study Space en el calendario del teléfono.
// No contiene credenciales de Google ni información de la cuenta.
public sealed class VinculoEventoCalendario
{
    // La clave compuesta evita mezclar tareas de perfiles distintos.
    [PrimaryKey]
    public string Clave { get; set; } = string.Empty;

    public int UsuarioId { get; set; }
    public int TareaId { get; set; }
    public long CalendarioId { get; set; }
    public long EventoId { get; set; }
}
