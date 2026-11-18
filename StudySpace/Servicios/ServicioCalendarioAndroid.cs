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
    string TipoCuenta)
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
    public bool EsSugerido => EsGoogle &&
        NombreVisible.Equals(Cuenta.Trim(), StringComparison.OrdinalIgnoreCase);

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
            var evento = LeerEvento(vinculo.EventoId);
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
        "Creado desde Study Space. Solo se actualizará al volver a usar el botón de calendario.";

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
                var existente = LeerEvento(vinculo.EventoId);
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
                var eventoUri = ContentUris.WithAppendedId(eventosUri, vinculo.EventoId);
                int modificados = resolver.Update(eventoUri, valores, null, null);
                if (modificados > 0)
                    return (true, "Evento actualizado correctamente.");
            }

            var nuevoUri = resolver.Insert(eventosUri, valores);
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
                resolver.Delete(ContentUris.WithAppendedId(eventosUri, nuevoId), null, null);
                throw;
            }

            if (vinculo is not null && vinculo.EventoId > 0 &&
                (vinculo.EventoId != nuevoId || vinculo.CalendarioId != calendarioId))
            {
                try
                {
                    resolver.Delete(ContentUris.WithAppendedId(eventosUri, vinculo.EventoId), null, null);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"HU15: evento anterior no retirado: {ex.Message}");
                }
            }
            return (true, "La tarea se agregó al calendario seleccionado.");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Se necesita permiso para modificar el calendario.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15: {ex}");
            return (false, "No fue posible guardar la tarea. Revisa los permisos del calendario.");
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
