using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using StudySpace.Servicios;
using StudySpace.Vistas;

namespace StudySpace.VistaModelos;

public partial class DetalleTareaVM : ObservableObject
{
    private readonly ServicioTareas _tareas;
    private readonly ServicioCursos _cursos;
    private readonly ServicioNotificaciones _notificaciones;
    private readonly ServicioCalendarioAndroid _calendarioAndroid;

    [ObservableProperty]
    private TareaDTO? tarea;

    [ObservableProperty]
    private bool ocupado;

    [ObservableProperty]
    private bool estaCompletada;

    [ObservableProperty]
    private string textoCompletar = "Marcar como completada";

    // HU15: la interfaz refleja si el evento ya existe y coincide con la tarea.
    [ObservableProperty]
    private string textoBotonCalendario = "Agregar al calendario";

    [ObservableProperty]
    private bool puedeGuardarCalendario = true;

    [ObservableProperty]
    private bool mostrarCambiarCalendario;

    [ObservableProperty]
    private string estadoCalendario = "Elige un calendario para guardar tu entrega.";


    // INYECCIÓN DE DEPENDENCIAS
    public DetalleTareaVM(
        ServicioTareas tareas,
        ServicioCursos cursos,
        ServicioNotificaciones notificaciones,
        ServicioCalendarioAndroid calendarioAndroid)
    {
        _tareas = tareas;
        _cursos = cursos;
        _notificaciones = notificaciones;
        _calendarioAndroid = calendarioAndroid;
    }

    // CARGAR DETALLE
    public async Task Cargar(int id)
    {
        Tarea = await _tareas.ObtenerPorId(id);

        if (Tarea == null)
            return;

        ActualizarEstado();
        await ActualizarEstadoCalendarioAsync();
    }

    private async Task ActualizarEstadoCalendarioAsync()
    {
        if (Tarea is null)
        {
            PuedeGuardarCalendario = false;
            MostrarCambiarCalendario = false;
            return;
        }

        var estado = await _calendarioAndroid.ConsultarEstadoAsync(Tarea);
        MostrarCambiarCalendario = estado.Vinculado;

        if (!estado.Vinculado)
        {
            TextoBotonCalendario = "Agregar al calendario";
            EstadoCalendario = "Todavía no se ha agregado esta tarea.";
            PuedeGuardarCalendario = true;
        }
        else if (!estado.Comprobable)
        {
            TextoBotonCalendario = "Verificar calendario";
            EstadoCalendario = "Se necesita acceso al calendario para revisar este evento.";
            PuedeGuardarCalendario = true;
        }
        else if (!estado.ExisteEvento)
        {
            TextoBotonCalendario = "Volver a agregar al calendario";
            EstadoCalendario = "El evento vinculado ya no se encuentra en el calendario.";
            PuedeGuardarCalendario = true;
        }
        else if (estado.Actualizado)
        {
            TextoBotonCalendario = "✓  Ya agregado al calendario";
            EstadoCalendario = "El evento está actualizado. No necesitas guardarlo otra vez.";
            PuedeGuardarCalendario = false;
        }
        else
        {
            TextoBotonCalendario = "Actualizar evento en calendario";
            EstadoCalendario = "La tarea o el evento cambiaron. Puedes actualizarlo.";
            PuedeGuardarCalendario = true;
        }
    }

    // ACTUALIZAR ESTADO VISUAL
    private void ActualizarEstado()
    {
        if (Tarea == null)
            return;

        EstaCompletada = Tarea.Completada;

        TextoCompletar = EstaCompletada
            ? "Marcar como pendiente"
            : "Marcar como completada";
    }

    // COMPLETAR O REABRIR TAREA
    [RelayCommand]
    private async Task Completar()
    {
        if (Tarea == null || Ocupado)
            return;

        Ocupado = true;

        try
        {
            int id = Tarea.Id;

            // ACTUALIZAR ESTADO EN SQLITE
            await _tareas.AlternarCompletada(id);

            // RECARGAR TAREA
            await Cargar(id);

            if (Tarea == null)
                return;

            try
            {
                if (Tarea.Completada)
                {
                    // CANCELAR RECORDATORIO
                    _notificaciones.CancelarRecordatorio(id);
                }
                else
                {
                    // REPROGRAMAR AL VOLVER A PENDIENTE
                    var fechaAviso = Tarea.FechaLimite
                        .AddMinutes(-Tarea.MinutosAviso);

                    if (fechaAviso > DateTime.Now)
                    {
                        // HU05 - OBTENER NOMBRE DEL CURSO
                        var cursos =
                            await _cursos.ObtenerCursos();

                        var curso = cursos.FirstOrDefault(
                            c => c.Id == Tarea.CursoId);

                        string nombreCurso =
                            curso?.Nombre ?? "";

                        await _notificaciones
                            .ProgramarRecordatorio(
                                id,
                                Tarea.Titulo,
                                Tarea.FechaLimite,
                                Tarea.MinutosAviso,
                                nombreCurso);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"HU05 - Error al actualizar recordatorio: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Aviso",
                $"No se pudo actualizar la tarea: {ex.Message}",
                "OK");
        }
        finally
        {
            Ocupado = false;
        }
    }

    // ELIMINAR TAREA
    [RelayCommand]
    private async Task Eliminar()
    {
        if (Tarea == null || Ocupado)
            return;

        bool confirmar = await Shell.Current.DisplayAlert(
            "Eliminar tarea",
            $"¿Seguro que deseas eliminar '{Tarea.Titulo}'?",
            "Eliminar",
            "Cancelar");

        if (!confirmar)
            return;

        Ocupado = true;

        try
        {
            int id = Tarea.Id;

            // ELIMINAR DE SQLITE
            var (ok, mensaje) =
                await _tareas.EliminarTarea(id);

            if (!ok)
            {
                await Shell.Current.DisplayAlert(
                    "Aviso",
                    mensaje,
                    "OK");

                return;
            }

            // HU05 - CANCELAR NOTIFICACIÓN
            try
            {
                _notificaciones.CancelarRecordatorio(id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"HU05 - Error al cancelar recordatorio: {ex.Message}");
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Aviso",
                $"No se pudo eliminar la tarea: {ex.Message}",
                "OK");
        }
        finally
        {
            Ocupado = false;
        }
    }

    // HU15 - Primera asociación: elegir un calendario claro y sin identificadores.
    // Si ya está vinculado, solo se puede actualizar cuando cambie el contenido.
    [RelayCommand]
    private Task GuardarCalendarioAndroid() => EnviarAlCalendarioAsync(cambiarDestino: false);

    // Permite cambiar explícitamente el calendario de destino, sin crear duplicados.
    [RelayCommand]
    private Task CambiarCalendario() => EnviarAlCalendarioAsync(cambiarDestino: true);

    private async Task EnviarAlCalendarioAsync(bool cambiarDestino)
    {
        if (Tarea is null || Ocupado)
            return;

        Ocupado = true;
        try
        {
#if ANDROID
            var lectura = await Permissions.CheckStatusAsync<Permissions.CalendarRead>();
            if (lectura != PermissionStatus.Granted)
                lectura = await Permissions.RequestAsync<Permissions.CalendarRead>();

            var escritura = await Permissions.CheckStatusAsync<Permissions.CalendarWrite>();
            if (escritura != PermissionStatus.Granted)
                escritura = await Permissions.RequestAsync<Permissions.CalendarWrite>();

            if (lectura != PermissionStatus.Granted || escritura != PermissionStatus.Granted)
            {
                await Shell.Current.DisplayAlert(
                    "Permisos del calendario",
                    "Para guardar tus entregas, permite el acceso al calendario desde Ajustes de Android.",
                    "Entendido");
                return;
            }

            // Leer siempre la tarea y el vínculo vigentes antes de modificar eventos.
            var actual = await _tareas.ObtenerPorId(Tarea.Id);
            if (actual is null)
            {
                await Shell.Current.DisplayAlert(
                    "Tarea no disponible", "La tarea ya no está disponible para esta sesión.", "OK");
                return;
            }

            var estado = await _calendarioAndroid.ConsultarEstadoAsync(actual);
            long calendarioId;

            if (estado.Vinculado && estado.Actualizado && !cambiarDestino)
            {
                // Seguridad: la doble pulsación nunca vuelve a escribir lo mismo.
                await ActualizarEstadoCalendarioAsync();
                return;
            }

            if (estado.Vinculado && estado.CalendarioId.HasValue && !cambiarDestino)
            {
                // Actualiza el evento original; no vuelve a preguntar por la cuenta.
                calendarioId = estado.CalendarioId.Value;
            }
            else
            {
                var opciones = _calendarioAndroid.ObtenerCalendariosEditables();
                if (opciones.Count == 0)
                {
                    await Shell.Current.DisplayAlert(
                        "Sin calendarios disponibles",
                        "No hay calendarios editables configurados en el teléfono. " +
                        "Revisa las cuentas y la sincronización del calendario en Ajustes de Android.",
                        "OK");
                    return;
                }

                var pagina = new PaginaSeleccionCalendario(opciones);
                await Shell.Current.Navigation.PushModalAsync(pagina);
                var seleccionado = await pagina.EsperarSeleccionAsync();
                if (seleccionado is null)
                    return;

                calendarioId = seleccionado.Id;

                // No hacer nada cuando el usuario elige el mismo calendario
                // y ya existe el evento actualizado.
                if (estado.Actualizado && estado.CalendarioId == calendarioId)
                    return;
            }

            var resultado = await _calendarioAndroid.GuardarOActualizarTarea(actual, calendarioId);
            await ActualizarEstadoCalendarioAsync();

            await Shell.Current.DisplayAlert(
                resultado.Exito ? "Calendario listo" : "No se pudo guardar",
                resultado.Mensaje,
                "Entendido");
#else
            await Shell.Current.DisplayAlert(
                "Calendario", "Esta función está disponible en Android.", "OK");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15 - Calendario: {ex}");
            await Shell.Current.DisplayAlert(
                "Calendario", "No se pudo completar la operación con el calendario.", "OK");
        }
        finally
        {
            Ocupado = false;
        }
    }

    // VOLVER
    [RelayCommand]
    private async Task Volver()
    {
        await Shell.Current.GoToAsync("..");
    }
}
