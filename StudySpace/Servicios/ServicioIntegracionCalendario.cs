using Aplicacion.CasosDeUso;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace StudySpace.Servicios;

/// <summary>
/// HU15: integración OPTATIVA con el calendario local Phone de Android.
/// El calendario propio de Study Space siempre funciona independientemente.
/// NO garantiza sincronización con los servidores de Google.
/// </summary>
public sealed class ServicioIntegracionCalendario
{
    private readonly ServicioAutenticacion _autenticacion;
    private readonly ServicioTareas _tareas;
    private readonly ServicioCalendarioAndroid _calendario;
    private const string Prefijo = "HU15_SincronizarPhone_Usuario_";

    public ServicioIntegracionCalendario(
        ServicioAutenticacion autenticacion,
        ServicioTareas tareas,
        ServicioCalendarioAndroid calendario)
    {
        _autenticacion = autenticacion;
        _tareas = tareas;
        _calendario = calendario;
    }

    private string? ClaveUsuario()
    {
        int usuarioId = _autenticacion.UsuarioActual?.Id ?? 0;
        return usuarioId > 0 ? Prefijo + usuarioId : null;
    }

    public bool EstaActiva
    {
        get
        {
            string? clave = ClaveUsuario();
            return clave is not null && Preferences.Default.Get(clave, false);
        }
    }

    public async Task<(bool Exito, string Mensaje)> ActivarAsync()
    {
#if ANDROID
        var clave = ClaveUsuario();
        if (clave is null)
            return (false, "Debes iniciar sesión antes de activar el calendario.");

        // La petición de permisos se hace solo al activar la función en Perfil.
        var lectura = await Permissions.CheckStatusAsync<Permissions.CalendarRead>();
        if (lectura != PermissionStatus.Granted)
            lectura = await Permissions.RequestAsync<Permissions.CalendarRead>();

        var escritura = await Permissions.CheckStatusAsync<Permissions.CalendarWrite>();
        if (escritura != PermissionStatus.Granted)
            escritura = await Permissions.RequestAsync<Permissions.CalendarWrite>();

        if (lectura != PermissionStatus.Granted || escritura != PermissionStatus.Granted)
            return (false, "Android no concedió acceso al calendario. Puedes activarlo desde los ajustes del teléfono.");

        try
        {
            var destino = await Task.Run(_calendario.ObtenerCalendarioTelefono);
            if (destino is null)
                return (false, "No se encontró el calendario Phone. Comprueba que exista y permita guardar eventos.");

            // Guardar preferencia solo después de validar permiso y destino.
            Preferences.Default.Set(clave, true);
            return (true, "Integración activada. Las tareas nuevas y las tareas que edites se guardarán también en el calendario Phone del dispositivo. Las anteriores no se importan automáticamente.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15 - Activación: {ex}");
            return (false, "No fue posible acceder al calendario del teléfono. La integración sigue desactivada.");
        }
#else
        return (false, "La integración con el calendario nativo está disponible en Android.");
#endif
    }

    public void Desactivar()
    {
        var clave = ClaveUsuario();
        if (clave is not null)
            Preferences.Default.Set(clave, false);
        // Los eventos externos existentes NO se eliminan al desactivar.
    }

    /// <summary>Se ejecuta DESPUÉS de confirmar que SQLite guardó la tarea.
    /// Si Android falla, no revierte ni altera la tarea.</summary>
    public async Task<(bool Intentado, bool Exito, string Mensaje)> SincronizarGuardadoAsync(int tareaId)
    {
        if (!EstaActiva)
            return (false, true, string.Empty);

#if ANDROID
        try
        {
            var lectura = await Permissions.CheckStatusAsync<Permissions.CalendarRead>();
            var escritura = await Permissions.CheckStatusAsync<Permissions.CalendarWrite>();
            if (lectura != PermissionStatus.Granted || escritura != PermissionStatus.Granted)
                return (true, false, "Faltan permisos de calendario. Revísalos en Ajustes del teléfono.");

            var tarea = await _tareas.ObtenerPorId(tareaId);
            if (tarea is null)
                return (true, false, "No se pudo recuperar la tarea guardada.");

            var (ok, mensaje) = await _calendario.GuardarEnTelefonoAsync(tarea);
            return (true, ok, mensaje);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15 - Guardar: {ex}");
            return (true, false, "Android no pudo actualizar el calendario del teléfono.");
        }
#else
        return (false, true, string.Empty);
#endif
    }

    /// <summary>Solo elimina eventos previamente vinculados y únicamente si
    /// la integración está activada. La tarea debe permanecer intacta en caso
    /// de error; el VM decide si permite eliminar solo en Study Space.</summary>
    public async Task<(bool Intentado, bool Exito, string Mensaje)> PrepararEliminacionAsync(int tareaId)
    {
        if (!EstaActiva)
            return (false, true, string.Empty);

#if ANDROID
        try
        {
            var resultado = await _calendario.EliminarVinculoExistenteAsync(tareaId);
            return (resultado.Vinculada, resultado.Exito, resultado.Mensaje);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15 - Eliminar: {ex}");
            return (true, false, "No se pudo retirar el evento del calendario.");
        }
#else
        return (false, true, string.Empty);
#endif
    }
}
