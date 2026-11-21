using Aplicacion.CasosDeUso;
using Dominio.Entidades;
using Infraestructura.DBContext;

namespace StudySpace.Servicios;

/// <summary>
/// HU17: elimina solamente cursos y tareas del perfil local activo.
/// NO elimina la cuenta de MySQL, los respaldos exportados ni los eventos
/// que ya se encuentran en el proveedor de calendario de Android.
/// </summary>
public sealed class ServicioGestionDatosLocales
{
    private readonly ContextoBaseDatos _contexto;
    private readonly ServicioAutenticacion _autenticacion;
    private readonly ServicioNotificaciones _notificaciones;
    private readonly ServicioRestauracionCifrada _restauracion;
    private readonly SemaphoreSlim _semaforo = new(1, 1);

    public ServicioGestionDatosLocales(
        ContextoBaseDatos contexto,
        ServicioAutenticacion autenticacion,
        ServicioNotificaciones notificaciones,
        ServicioRestauracionCifrada restauracion)
    {
        _contexto = contexto;
        _autenticacion = autenticacion;
        _notificaciones = notificaciones;
        _restauracion = restauracion;
    }

    public sealed record Resumen(int Cursos, int Tareas);
    public sealed record Resultado(int CursosEliminados, int TareasEliminadas, int AvisosConError);

    public async Task<Resumen> ConsultarAsync()
    {
        int usuarioId = _autenticacion.UsuarioActual?.Id ?? 0;
        if (usuarioId <= 0)
            throw new InvalidOperationException("Debes iniciar sesión para consultar tus datos.");

        var db = await _contexto.ObtenerDb();
        int cursos = await db.Table<Curso>()
            .Where(c => c.UsuarioId == usuarioId).CountAsync();
        int tareas = await db.Table<Tarea>()
            .Where(t => t.UsuarioId == usuarioId).CountAsync();
        return new Resumen(cursos, tareas);
    }

    public async Task<Resultado> EliminarDatosAcademicosAsync()
    {
        await _semaforo.WaitAsync();
        try
        {
            int usuarioId = _autenticacion.UsuarioActual?.Id ?? 0;
            if (usuarioId <= 0)
                throw new InvalidOperationException("Debes iniciar sesión para eliminar tus datos.");

            var db = await _contexto.ObtenerDb();
            var idsNotificaciones = new List<int>();
            int cursosEliminados = 0;
            int tareasEliminadas = 0;

            // Una sola transacción: nunca dejar tareas eliminadas con cursos intactos
            // por un fallo parcial de SQLite. Todos los borrados se limitan al perfil.
            await db.RunInTransactionAsync(tx =>
            {
                if (_autenticacion.UsuarioActual?.Id != usuarioId)
                    throw new InvalidOperationException("La cuenta activa cambió. No se borró nada.");

                idsNotificaciones = tx.Query<Tarea>(
                    "SELECT * FROM Tarea WHERE UsuarioId = ?", usuarioId)
                    .Select(t => t.Id).ToList();

                // Se elimina el vínculo local, no el evento externo de Android.
                tx.Execute(
                    "DELETE FROM VinculoEventoCalendario WHERE UsuarioId = ?", usuarioId);
                tareasEliminadas = tx.Execute(
                    "DELETE FROM Tarea WHERE UsuarioId = ?", usuarioId);
                cursosEliminados = tx.Execute(
                    "DELETE FROM Curso WHERE UsuarioId = ?", usuarioId);
            });

            // Invalidar una vista previa anterior para evitar restauraciones accidentales.
            _restauracion.DescartarVistaPrevia();

            // Los avisos no forman parte de la transacción SQLite. Cancelarlos tras
            // confirmar el borrado; un error se comunica, pero no se oculta el éxito.
            int fallosAvisos = 0;
            foreach (int tareaId in idsNotificaciones)
            {
                try { _notificaciones.CancelarRecordatorio(tareaId); }
                catch (Exception ex)
                {
                    fallosAvisos++;
                    System.Diagnostics.Debug.WriteLine($"HU17 - Cancelar aviso: {ex.GetType().Name}");
                }
            }

            return new Resultado(cursosEliminados, tareasEliminadas, fallosAvisos);
        }
        finally
        {
            _semaforo.Release();
        }
    }
}
