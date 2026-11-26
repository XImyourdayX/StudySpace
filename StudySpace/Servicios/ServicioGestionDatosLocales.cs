using Aplicacion.CasosDeUso;
using Dominio.Entidades;
using Infraestructura.DBContext;
using Microsoft.Maui.Storage;

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

    private const string ClaveLimpiezaPendiente = "HU17_UsuarioLocalBorradoRemotoPendiente";

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

    /// <summary>
    /// Usar SOLO cuando cuenta-eliminar.php respondió cuenta_eliminada.
    /// Permite continuar la limpieza si Android cierra la app a mitad de camino.
    /// </summary>
    public void MarcarLimpiezaPendiente(int usuarioLocalId)
    {
        if (usuarioLocalId <= 0)
            throw new ArgumentOutOfRangeException(nameof(usuarioLocalId));
        Preferences.Default.Set(ClaveLimpiezaPendiente, usuarioLocalId);
    }

    /// <summary>
    /// La eliminación local es idempotente y está limitada al ID registrado
    /// después de confirmar la eliminación remota. No requiere sesión activa:
    /// el servidor ya revocó esa cuenta y el token puede estar vencido.
    /// </summary>
    public async Task<bool> CompletarLimpiezaPendienteAsync()
    {
        await _semaforo.WaitAsync();
        try
        {
            int id = Preferences.Default.Get(ClaveLimpiezaPendiente, 0);
            if (id <= 0) return false;

            var db = await _contexto.ObtenerDb();
            var idsNotificaciones = new List<int>();

            await db.RunInTransactionAsync(tx =>
            {
                idsNotificaciones = tx.Query<Tarea>(
                    "SELECT * FROM Tarea WHERE UsuarioId = ?", id)
                    .Select(t => t.Id).ToList();

                tx.Execute("DELETE FROM VinculoEventoCalendario WHERE UsuarioId = ?", id);
                tx.Execute("DELETE FROM Tarea WHERE UsuarioId = ?", id);
                tx.Execute("DELETE FROM Curso WHERE UsuarioId = ?", id);
                tx.Execute("DELETE FROM VinculoUsuarioRemoto WHERE UsuarioLocalId = ?", id);
                tx.Execute("DELETE FROM Usuario WHERE Id = ?", id);
            });

            _restauracion.DescartarVistaPrevia();
            Preferences.Default.Remove(ClaveLimpiezaPendiente);

            // Las notificaciones no son parte de la transacción SQLite.
            foreach (int tareaId in idsNotificaciones)
            {
                try { _notificaciones.CancelarRecordatorio(tareaId); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"HU17 - Aviso antiguo: {ex.GetType().Name}");
                }
            }
            return true;
        }
        finally
        {
            _semaforo.Release();
        }
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
