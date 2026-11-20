using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using Infraestructura.DBContext;
using Microsoft.Maui.ApplicationModel;

#if ANDROID
using Android.Content;
using Android.Provider;
#endif

namespace StudySpace.Servicios;

// El ID se usa internamente; la interfaz nunca muestra identificadores técnicos.
public sealed record OpcionCalendarioAndroid(
    long Id,
    string Nombre,
    string Cuenta,
    string TipoCuenta,
    bool EsPrimario = false)
{
    public bool EsGoogle => TipoCuenta.Equals("com.google", StringComparison.OrdinalIgnoreCase);

    public string NombreVisible => string.IsNullOrWhiteSpace(Nombre)
        ? "Calendario sin nombre"
        : Nombre.Trim();

    public string Origen => EsGoogle ? "Google" :
        string.IsNullOrWhiteSpace(TipoCuenta) ||
        TipoCuenta.Equals("LOCAL", StringComparison.OrdinalIgnoreCase)
            ? "En el teléfono"
            : "Otra cuenta";

    public string Detalle => string.IsNullOrWhiteSpace(Cuenta) ||
                             NombreVisible.Equals(Cuenta.Trim(), StringComparison.OrdinalIgnoreCase)
        ? Origen
        : $"{Origen} · {Cuenta.Trim()}";

    // Se prioriza el calendario personal de Google sin asumir que es el predeterminado.
    public bool EsSugerido => EsGoogle && EsPrimario;

    public string Sugerencia => EsSugerido ? "CALENDARIO DE TU CUENTA" : Origen.ToUpperInvariant();
}

public sealed record EstadoVinculoCalendario(
    bool Vinculado,
    bool Comprobable,
    bool ExisteEvento,
    bool Actualizado,
    long? CalendarioId)
{
    public static EstadoVinculoCalendario SinVinculo => new(false, true, false, false, null);
}

// HU15: Calendar Provider Android; no usa OAuth ni servidor externo.
// Los eventos se verifican en el calendario real: no hay que modificar la tabla SQLite.
public sealed class ServicioCalendarioAndroid
{
    private readonly ContextoBaseDatos _dbContexto;
    private readonly ServicioAutenticacion _autenticacion;
    private readonly SemaphoreSlim _operacion = new(1, 1);

    public ServicioCalendarioAndroid(
        ContextoBaseDatos dbContexto,
        ServicioAutenticacion autenticacion)
    {
        _dbContexto = dbContexto;
        _autenticacion = autenticacion;
    }

    public IReadOnlyList<OpcionCalendarioAndroid> ObtenerCalendariosEditables()
    {
#if ANDROID
        var uri = CalendarContract.Calendars.ContentUri
            ?? throw new InvalidOperationException("No está disponible el calendario de Android.");
        var resolver = Platform.AppContext.ContentResolver
            ?? throw new InvalidOperationException("No se pudo acceder al proveedor de calendario.");

        string[] columnas =
        {
            "_id", "calendar_displayName", "account_name", "calendar_access_level", "account_type"
        };

        using var cursor = resolver.Query(
            uri, columnas, "calendar_access_level >= ?", new[] { "500" },
            "calendar_displayName COLLATE NOCASE ASC");

        var calendarios = new List<OpcionCalendarioAndroid>();
        if (cursor is null)
            return calendarios;

        while (cursor.MoveToNext())
        {
            long id = cursor.GetLong(0);
            string nombre = cursor.IsNull(1) ? "Calendario" : cursor.GetString(1) ?? "Calendario";
            string cuenta = cursor.IsNull(2) ? "" : cursor.GetString(2) ?? "";
            string tipo = cursor.IsNull(4) ? "" : cursor.GetString(4) ?? "";
            calendarios.Add(new OpcionCalendarioAndroid(id, nombre, cuenta, tipo));
        }

        return calendarios
            .OrderByDescending(x => x.EsSugerido)
            .ThenBy(x => x.Origen)
            .ThenBy(x => x.NombreVisible)
            .ToList();
#else
        return Array.Empty<OpcionCalendarioAndroid>();
#endif
    }

    // HU15 - Modo sencillo. Guarda directamente en el calendario Phone del dispositivo.
    // No abre formularios ni intenta escribir en una cuenta Google.
    // NO seleccionar automáticamente calendarios de otras aplicaciones.
    public OpcionCalendarioAndroid? ObtenerCalendarioTelefono()
    {
#if ANDROID
        var opciones = ObtenerCalendariosEditables();
        return opciones.FirstOrDefault(c =>
                   !c.EsGoogle && (
                       c.NombreVisible.Equals("Phone", StringComparison.OrdinalIgnoreCase) ||
                       c.Cuenta.Equals("Phone", StringComparison.OrdinalIgnoreCase) ||
                       c.NombreVisible.Equals("Teléfono", StringComparison.OrdinalIgnoreCase) ||
                       c.Cuenta.Equals("Teléfono", StringComparison.OrdinalIgnoreCase)))
            ?? opciones.FirstOrDefault(c => !c.EsGoogle && (
                       c.TipoCuenta.Equals("LOCAL", StringComparison.OrdinalIgnoreCase) ||
                       c.TipoCuenta.Equals("com.android.local", StringComparison.OrdinalIgnoreCase)));
#else
        return null;
#endif
    }

    // Estado solo del evento creado en el calendario Phone.
    // Un vínculo antiguo de Google no bloquea el botón de guardado local.
    public async Task<EstadoVinculoCalendario> ConsultarEstadoTelefonoAsync(TareaDTO tarea)
    {
#if ANDROID
        var estado = await ConsultarEstadoAsync(tarea);
        if (!estado.Vinculado || !estado.Comprobable)
            return estado;
        try
        {
            var telefono = await Task.Run(ObtenerCalendarioTelefono);
            return telefono is not null && estado.CalendarioId == telefono.Id
                ? estado : EstadoVinculoCalendario.SinVinculo;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15: no se pudo consultar Phone: {ex}");
            return new(true, false, false, false, estado.CalendarioId);
        }
#else
        return EstadoVinculoCalendario.SinVinculo;
#endif
    }

    // Se mantiene el mismo ID del evento para evitar duplicados al actualizar.
    public async Task<(bool Exito, string Mensaje)> GuardarEnTelefonoAsync(TareaDTO tarea)
    {
#if ANDROID
        var telefono = await Task.Run(ObtenerCalendarioTelefono);
        if (telefono is null)
            return (false, "No se encontró un calendario local del teléfono que admita eventos. " +
                "Abre el calendario de Android y comprueba que tengas disponible el calendario Phone.");
        return await GuardarOActualizarTarea(tarea, telefono.Id);
#else
        return (false, "Esta función está disponible únicamente en Android.");
#endif
    }

    public async Task<EstadoVinculoCalendario> ConsultarEstadoAsync(TareaDTO tarea)
    {
#if ANDROID
        int usuarioId = _autenticacion.UsuarioActual?.Id ?? 0;
        if (usuarioId <= 0 || tarea.Id <= 0)
            return EstadoVinculoCalendario.SinVinculo;

        var db = await _dbContexto.ObtenerDb();
        var vinculo = await db.FindAsync<VinculoEventoCalendario>($"{usuarioId}:{tarea.Id}");
        if (vinculo is null || vinculo.EventoId <= 0)
            return EstadoVinculoCalendario.SinVinculo;

        var permiso = await Permissions.CheckStatusAsync<Permissions.CalendarRead>();
        if (permiso != PermissionStatus.Granted)
            return new(true, false, false, false, vinculo.CalendarioId);

        try
        {
            // Google puede tardar en responder: nunca leer el proveedor en el hilo UI.
            var evento = await Task.Run(() => LeerEvento(vinculo.EventoId));
            if (evento is null)
                return new(true, true, false, false, vinculo.CalendarioId);

            bool coincide = evento.CalendarioId == vinculo.CalendarioId &&
                            CoincideConTarea(evento, tarea);
            return new(true, true, true, coincide, vinculo.CalendarioId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15: error leyendo calendario: {ex}");
            return new(true, false, false, false, vinculo.CalendarioId);
        }
#else
        return EstadoVinculoCalendario.SinVinculo;
#endif
    }

#if ANDROID
    private sealed record EventoExistente(
        long CalendarioId, string Titulo, string Descripcion, long Inicio, long Fin);

    private static DateTime FechaLocal(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => fecha.ToLocalTime(),
        DateTimeKind.Local => fecha,
        _ => DateTime.SpecifyKind(fecha, DateTimeKind.Local)
    };

    private static string TituloEvento(TareaDTO tarea) => $"Entrega: {tarea.Titulo}";

    private static string DescripcionEvento(TareaDTO tarea, DateTime fechaLocal) =>
        $"Curso: {tarea.CursoNombre}\n" +
        $"Fecha límite: {fechaLocal:dd/MM/yyyy HH:mm}\n" +
        "Creado desde Study Space. Los cambios realizados en la app se reflejan en este evento.";

    private static EventoExistente? LeerEvento(long eventoId)
    {
        var resolver = Platform.AppContext.ContentResolver
            ?? throw new InvalidOperationException("Calendario Android no disponible.");
        var eventosUri = CalendarContract.Events.ContentUri
            ?? throw new InvalidOperationException("Calendario Android no disponible.");
        var uri = ContentUris.WithAppendedId(eventosUri, eventoId);
        string[] columnas = { "calendar_id", "title", "description", "dtstart", "dtend" };
        using var cursor = resolver.Query(uri, columnas, null, null, null);
        if (cursor is null || !cursor.MoveToFirst())
            return null;
        return new EventoExistente(
            cursor.GetLong(0),
            cursor.IsNull(1) ? "" : cursor.GetString(1) ?? "",
            cursor.IsNull(2) ? "" : cursor.GetString(2) ?? "",
            cursor.GetLong(3),
            cursor.GetLong(4));
    }

    private static bool CoincideConTarea(EventoExistente evento, TareaDTO tarea)
    {
        var fecha = FechaLocal(tarea.FechaLimite);
        long fin = new DateTimeOffset(fecha).ToUnixTimeMilliseconds();
        long inicio = new DateTimeOffset(fecha.AddMinutes(-15)).ToUnixTimeMilliseconds();
        return evento.Titulo == TituloEvento(tarea) &&
               evento.Descripcion == DescripcionEvento(tarea, fecha) &&
               evento.Inicio == inicio && evento.Fin == fin;
    }
#endif

    // HU15 - PASO 5. Actualiza UNICAMENTE eventos previamente vinculados.
    // Nunca crea eventos nuevos ni solicita permisos de forma inesperada.
    public async Task<(bool Exito, bool Vinculada, string Mensaje)> ActualizarVinculoExistenteAsync(
        TareaDTO tarea)
    {
#if ANDROID
        await _operacion.WaitAsync();
        try
        {
            int usuarioId = _autenticacion.UsuarioActual?.Id ?? 0;
            if (usuarioId <= 0 || tarea.Id <= 0)
                return (false, false, "La sesión no está disponible.");

            var db = await _dbContexto.ObtenerDb();
            var vinculo = await db.FindAsync<VinculoEventoCalendario>($"{usuarioId}:{tarea.Id}");
            if (vinculo is null || vinculo.EventoId <= 0)
                return (true, false, ""); // El usuario nunca vinculó esta tarea.

            // Solo autoactualizar eventos del calendario local Phone.
            // Los vínculos antiguos de Google quedan intactos.
            var telefono = await Task.Run(ObtenerCalendarioTelefono);
            if (telefono is null || vinculo.CalendarioId != telefono.Id)
                return (true, false, "");

            if (!await TienePermisosCalendarioAsync())
                return (false, true, "Falta el permiso del calendario. Actívalo en Ajustes de Android y actualiza el evento desde el detalle de la tarea.");

            var actual = await Task.Run(() => LeerEvento(vinculo.EventoId));
            if (actual is null)
                return (false, true, "El evento ya no existe en el calendario. Puedes volver a agregarlo desde el detalle de la tarea.");

            // Si alguien movió el evento a otro calendario, no lo modificamos a ciegas.
            if (actual.CalendarioId != vinculo.CalendarioId)
                return (false, true, "El evento cambió de calendario fuera de Study Space. Revísalo antes de sincronizar.");

            if (CoincideConTarea(actual, tarea))
                return (true, true, "El evento ya está actualizado.");

            var eventosUri = CalendarContract.Events.ContentUri;
            var resolver = Platform.AppContext.ContentResolver;
            if (eventosUri is null || resolver is null)
                return (false, true, "Android no ofrece acceso al calendario en este momento.");

            var fecha = FechaLocal(tarea.FechaLimite);
            long fin = new DateTimeOffset(fecha).ToUnixTimeMilliseconds();
            long inicio = new DateTimeOffset(fecha.AddMinutes(-15)).ToUnixTimeMilliseconds();

            using var valores = new ContentValues();
            valores.Put("title", TituloEvento(tarea));
            valores.Put("description", DescripcionEvento(tarea, fecha));
            valores.Put("dtstart", inicio);
            valores.Put("dtend", fin);
            valores.Put("eventTimezone", TimeZoneInfo.Local.Id);

            var uri = ContentUris.WithAppendedId(eventosUri, vinculo.EventoId);
            int cambiados = await Task.Run(() => resolver.Update(uri, valores, null, null));
            return cambiados > 0
                ? (true, true, "El calendario se actualizó automáticamente.")
                : (false, true, "Android no pudo actualizar el evento. Prueba desde el detalle de la tarea.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15 - Sincronizar edición: {ex}");
            return (false, true, "No fue posible actualizar el calendario. Puedes reintentarlo desde el detalle de la tarea.");
        }
        finally
        {
            _operacion.Release();
        }
#else
        return (true, false, "");
#endif
    }

    // HU15 - PASO 5. Borra SOLO el evento que Study Space vinculó a esta tarea.
    // Para evitar eventos huérfanos, si un vínculo existe pero no podemos acceder
    // al calendario, NO autorizamos borrar la tarea local silenciosamente.
    public async Task<(bool Exito, bool Vinculada, string Mensaje)> EliminarVinculoExistenteAsync(
        int tareaId)
    {
#if ANDROID
        await _operacion.WaitAsync();
        try
        {
            int usuarioId = _autenticacion.UsuarioActual?.Id ?? 0;
            if (usuarioId <= 0 || tareaId <= 0)
                return (false, false, "La sesión no está disponible.");

            var db = await _dbContexto.ObtenerDb();
            var vinculo = await db.FindAsync<VinculoEventoCalendario>($"{usuarioId}:{tareaId}");
            if (vinculo is null)
                return (true, false, "");

            // No borrar eventos de cuentas Google u otras apps.
            var telefono = await Task.Run(ObtenerCalendarioTelefono);
            if (telefono is null || vinculo.CalendarioId != telefono.Id)
            {
                await db.DeleteAsync(vinculo);
                return (true, true, "El vínculo externo se desvinculó; no se borró ningún evento de otro calendario.");
            }

            if (vinculo.EventoId <= 0)
            {
                await db.DeleteAsync(vinculo);
                return (true, true, "Se limpió un vínculo antiguo sin evento.");
            }

            if (!await TienePermisosCalendarioAsync())
                return (false, true, "No se borró la tarea porque falta acceso al calendario. Activa los permisos de calendario en Ajustes de Android y vuelve a intentarlo.");

            var eventosUri = CalendarContract.Events.ContentUri;
            var resolver = Platform.AppContext.ContentResolver;
            if (eventosUri is null || resolver is null)
                return (false, true, "No se pudo acceder al calendario. La tarea permanece guardada.");

            // Google puede tardar en responder: nunca leer el proveedor en el hilo UI.
            var evento = await Task.Run(() => LeerEvento(vinculo.EventoId));
            if (evento is not null)
            {
                if (evento.CalendarioId != vinculo.CalendarioId)
                    return (false, true, "El evento fue movido a otro calendario. Revisa el vínculo antes de eliminar la tarea.");

                var uri = ContentUris.WithAppendedId(eventosUri, vinculo.EventoId);
                int borrados = await Task.Run(() => resolver.Delete(uri, null, null));
                // Puede desaparecer por sincronización externa entre consulta y borrado.
                if (borrados == 0 &&
                    await Task.Run(() => LeerEvento(vinculo.EventoId)) is not null)
                    return (false, true, "Android no pudo quitar el evento. La tarea permanece guardada.");
            }

            await db.DeleteAsync(vinculo);
            return (true, true, "Se retiró también el evento del calendario.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15 - Sincronizar eliminación: {ex}");
            return (false, true, "No se pudo quitar el evento del calendario. No se ha eliminado la tarea. Inténtalo de nuevo.");
        }
        finally
        {
            _operacion.Release();
        }
#else
        return (true, false, "");
#endif
    }

#if ANDROID
    private static async Task<bool> TienePermisosCalendarioAsync()
    {
        var lectura = await Permissions.CheckStatusAsync<Permissions.CalendarRead>();
        var escritura = await Permissions.CheckStatusAsync<Permissions.CalendarWrite>();
        return lectura == PermissionStatus.Granted && escritura == PermissionStatus.Granted;
    }
#endif

    public async Task<(bool Exito, string Mensaje)> GuardarOActualizarTarea(
        TareaDTO tarea, long calendarioId)
    {
#if ANDROID
        await _operacion.WaitAsync();
        try
        {
            int usuarioId = _autenticacion.UsuarioActual?.Id ?? 0;
            if (usuarioId <= 0 || tarea.Id <= 0)
                return (false, "Inicia sesión para vincular una tarea.");

            var resolver = Platform.AppContext.ContentResolver;
            if (resolver is null)
                return (false, "Android no dispone del servicio de calendario.");
            var eventosUri = CalendarContract.Events.ContentUri;
            if (eventosUri is null)
                return (false, "No se encontró el calendario de eventos.");

            string clave = $"{usuarioId}:{tarea.Id}";
            var db = await _dbContexto.ObtenerDb();
            var vinculo = await db.FindAsync<VinculoEventoCalendario>(clave);

            var fecha = FechaLocal(tarea.FechaLimite);
            long fin = new DateTimeOffset(fecha).ToUnixTimeMilliseconds();
            long inicio = new DateTimeOffset(fecha.AddMinutes(-15)).ToUnixTimeMilliseconds();

            // No se repite el guardado cuando el evento ya coincide con la tarea.
            if (vinculo is not null && vinculo.CalendarioId == calendarioId &&
                vinculo.EventoId > 0)
            {
                var existente = await Task.Run(() => LeerEvento(vinculo.EventoId));
                if (existente is not null && CoincideConTarea(existente, tarea) &&
                    existente.CalendarioId == calendarioId)
                {
                    return (true, "La tarea ya está guardada en este calendario y no tiene cambios.");
                }
            }

            using var valores = new ContentValues();
            valores.Put("calendar_id", calendarioId);
            valores.Put("title", TituloEvento(tarea));
            valores.Put("description", DescripcionEvento(tarea, fecha));
            valores.Put("dtstart", inicio);
            valores.Put("dtend", fin);
            valores.Put("eventTimezone", TimeZoneInfo.Local.Id);

            if (vinculo is not null && vinculo.CalendarioId == calendarioId &&
                vinculo.EventoId > 0)
            {
                var eventoActual = await Task.Run(() => LeerEvento(vinculo.EventoId));
                if (eventoActual is not null)
                {
                    if (eventoActual.CalendarioId != calendarioId)
                        return (false, "El evento cambió de calendario fuera de Study Space. No se creó otro evento.");

                    var eventoUri = ContentUris.WithAppendedId(eventosUri, vinculo.EventoId);
                    int modificados = await Task.Run(() =>
                        resolver.Update(eventoUri, valores, null, null));
                    // Un Update=0 sobre un evento existente NO autoriza crear otro:
                    // evitar duplicados y dejar la tarea guardada en Study Space.
                    return modificados > 0
                        ? (true, "Evento actualizado correctamente.")
                        : (false, "Android no pudo actualizar el evento existente. No se creó un duplicado.");
                }
                // Si el evento vinculado YA NO EXISTE, se puede crear otro
                // para restaurar el vínculo.
            }

            // La inserción puede bloquear el hilo principal en calendarios de Google.
            // En segundo plano, la pantalla seguirá mostrando el estado de guardado.
            var nuevoUri = await Task.Run(() => resolver.Insert(eventosUri, valores));
            if (nuevoUri is null)
                return (false, "Android no permitió crear el evento.");
            long nuevoId = ContentUris.ParseId(nuevoUri);
            if (nuevoId <= 0)
                return (false, "No se pudo identificar el evento creado.");

            try
            {
                await db.InsertOrReplaceAsync(new VinculoEventoCalendario
                {
                    Clave = clave,
                    UsuarioId = usuarioId,
                    TareaId = tarea.Id,
                    CalendarioId = calendarioId,
                    EventoId = nuevoId
                });
            }
            catch
            {
                await Task.Run(() => resolver.Delete(
                    ContentUris.WithAppendedId(eventosUri, nuevoId), null, null));
                throw;
            }

            // No se borran eventos previos de otros calendarios.
            // Si existía un vínculo anterior con Google, se conservará allí.
            bool habiaOtroCalendario = vinculo is not null && vinculo.CalendarioId != calendarioId;
            return (true, habiaOtroCalendario
                ? "Guardado directamente en el calendario del teléfono. El evento del calendario anterior no se modificó."
                : "Guardado directamente en el calendario del teléfono.");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Se necesita permiso para modificar el calendario.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15: error guardando en calendario: {ex}");
            // No se muestra nombre de cuenta ni datos privados en el aviso.
            return (false, $"Android no pudo guardar en el calendario del teléfono ({ex.GetType().Name}). " +
                "Revisa los permisos y que el calendario Phone esté disponible. " +
                "La tarea de Study Space permanece sin cambios.");
        }
        finally
        {
            _operacion.Release();
        }
#else
        return (false, "Esta función está disponible únicamente en Android.");
#endif
    }
}
