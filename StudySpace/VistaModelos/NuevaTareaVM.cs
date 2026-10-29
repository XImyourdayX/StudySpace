
using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySpace.Servicios;
using System.Collections.ObjectModel;

namespace StudySpace.VistaModelos;

public partial class NuevaTareaVM : ObservableObject
{
    private readonly ServicioTareas _tareas;
    private readonly ServicioCursos _cursos;
    private readonly ServicioNotificaciones _notificaciones;

    // HU11 - CLAVE DE LA PREFERENCIA GUARDADA
    private const string ClaveAnticipacion =
        "HU11_AnticipacionMinutos";

    private readonly int[] _minutosAviso =
    {
        15, 60, 1440, 2880
    };

    private int _tareaId;

    public ObservableCollection<CursoDTO> Cursos { get; } = new();

    public List<string> Prioridades { get; } = new()
    {
        "Baja", "Media", "Alta"
    };

    public List<string> Avisos { get; } = new()
    {
        "15 minutos antes",
        "1 hora antes",
        "1 día antes",
        "2 días antes"
    };

    [ObservableProperty]
    private string titulo = "";

    [ObservableProperty]
    private string descripcion = "";

    [ObservableProperty]
    private CursoDTO? cursoSeleccionado;

    [ObservableProperty]
    private DateTime fecha = DateTime.Today;

    [ObservableProperty]
    private TimeSpan hora = new(23, 59, 0);

    [ObservableProperty]
    private string prioridad = "Media";

    [ObservableProperty]
    private int indiceAviso = 2;

    [ObservableProperty]
    private bool ocupado;

    [ObservableProperty]
    private bool esEdicion;

    [ObservableProperty]
    private string tituloPagina = "Nueva tarea";

    [ObservableProperty]
    private string textoBoton = "Guardar tarea";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string mensajeError = "";

    public bool TieneError =>
        !string.IsNullOrEmpty(MensajeError);

    public NuevaTareaVM(
        ServicioTareas tareas,
        ServicioCursos cursos,
        ServicioNotificaciones notificaciones)
    {
        _tareas = tareas;
        _cursos = cursos;
        _notificaciones = notificaciones;
    }

    // HU11 - OBTENER ANTICIPACIÓN PREDETERMINADA
    private int ObtenerIndiceAvisoPredeterminado()
    {
        int minutosGuardados = Preferences.Default.Get(
            ClaveAnticipacion,
            1440);

        int indice = Array.IndexOf(
            _minutosAviso,
            minutosGuardados);

        return indice >= 0 ? indice : 2;
    }

    // CARGAR FORMULARIO
    public async Task Cargar(int id = 0)
    {
        _tareaId = 0;
        EsEdicion = false;
        TituloPagina = "Nueva tarea";
        TextoBoton = "Guardar tarea";
        MensajeError = "";
        Ocupado = false;

        var lista = await _cursos.ObtenerCursos();

        Cursos.Clear();

        foreach (var curso in lista)
            Cursos.Add(curso);

        // VALORES PARA UNA TAREA NUEVA
        Titulo = "";
        Descripcion = "";
        CursoSeleccionado = Cursos.FirstOrDefault();
        Fecha = DateTime.Today;
        Hora = new TimeSpan(23, 59, 0);
        Prioridad = "Media";

        // HU11 - APLICAR PREFERENCIA DE PERFIL
        IndiceAviso = ObtenerIndiceAvisoPredeterminado();

        if (id <= 0)
            return;

        // BUSCAR TAREA EXISTENTE
        var tarea = await _tareas.ObtenerPorId(id);

        if (tarea == null)
        {
            MensajeError =
                "No se encontró la tarea que deseas editar.";
            return;
        }

        // ACTIVAR MODO EDICIÓN
        _tareaId = tarea.Id;
        EsEdicion = true;
        TituloPagina = "Editar tarea";
        TextoBoton = "Guardar cambios";

        // CARGAR DATOS DE LA TAREA
        Titulo = tarea.Titulo;
        Descripcion = tarea.Descripcion;

        CursoSeleccionado = Cursos.FirstOrDefault(
            c => c.Id == tarea.CursoId);

        Fecha = tarea.FechaLimite.Date;
        Hora = tarea.FechaLimite.TimeOfDay;
        Prioridad = tarea.Prioridad;

        // AL EDITAR, CONSERVAR EL AVISO PROPIO
        // DE LA TAREA EXISTENTE
        int indice = Array.IndexOf(
            _minutosAviso,
            tarea.MinutosAviso);

        IndiceAviso = indice >= 0 ? indice : 2;
    }

    // GUARDAR TAREA
    [RelayCommand]
    private async Task Guardar()
    {
        if (Ocupado)
            return;

        MensajeError = "";
        Ocupado = true;

        try
        {
            var fechaLimite = Fecha.Date + Hora;

            var indice = Math.Clamp(
                IndiceAviso,
                0,
                _minutosAviso.Length - 1);

            var minutos = _minutosAviso[indice];

            string nombreCurso =
                CursoSeleccionado?.Nombre ?? "";

            bool ok;
            string mensaje;
            int tareaId;
            bool completada = false;

            if (EsEdicion)
            {
                // ACTUALIZAR TAREA
                (ok, mensaje) = await _tareas.EditarTarea(
                    _tareaId,
                    Titulo,
                    Descripcion,
                    CursoSeleccionado?.Id ?? 0,
                    fechaLimite,
                    Prioridad,
                    minutos);

                tareaId = _tareaId;

                if (ok)
                {
                    var tareaActualizada =
                        await _tareas.ObtenerPorId(tareaId);

                    completada =
                        tareaActualizada?.Completada ?? true;
                }
            }
            else
            {
                // CREAR TAREA Y OBTENER SU ID
                var resultado =
                    await _tareas.AgregarTareaConId(
                        Titulo,
                        Descripcion,
                        CursoSeleccionado?.Id ?? 0,
                        fechaLimite,
                        Prioridad,
                        minutos);

                ok = resultado.Exito;
                mensaje = resultado.Mensaje;
                tareaId = resultado.TareaId;
            }

            if (!ok)
            {
                MensajeError = mensaje;
                return;
            }

            // HU05 Y HU11 - PROGRAMAR RECORDATORIO
            bool notificacionProgramada = false;

            bool fechaAvisoValida =
                fechaLimite.AddMinutes(-minutos) > DateTime.Now;

            try
            {
                if (tareaId > 0)
                {
                    // CANCELAR AVISO ANTERIOR
                    _notificaciones.CancelarRecordatorio(
                        tareaId);

                    if (!completada && fechaAvisoValida)
                    {
                        notificacionProgramada =
                            await _notificaciones
                                .ProgramarRecordatorio(
                                    tareaId,
                                    Titulo.Trim(),
                                    fechaLimite,
                                    minutos,
                                    nombreCurso);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"HU05 - Error de notificación: {ex.Message}");
            }

            string mensajeFinal = EsEdicion
                ? "Tarea actualizada correctamente."
                : "Tarea guardada correctamente.";

#if ANDROID
            if (!completada &&
                fechaAvisoValida &&
                notificacionProgramada)
            {
                mensajeFinal +=
                    "\nRecordatorio programado.";
            }
            else if (!completada && fechaAvisoValida)
            {
                mensajeFinal +=
                    "\nNo se programó el recordatorio. " +
                    "Comprueba las preferencias, el horario " +
                    "permitido y los permisos de Android.";
            }
            else if (!completada && !fechaAvisoValida)
            {
                mensajeFinal +=
                    "\nLa hora del recordatorio ya pasó.";
            }
#endif

            await Shell.Current.DisplayAlert(
                "Listo",
                mensajeFinal,
                "OK");

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            MensajeError =
                $"No se pudo guardar la tarea: {ex.Message}";
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
