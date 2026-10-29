using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySpace.Servicios;

namespace StudySpace.VistaModelos;

public partial class DetalleTareaVM : ObservableObject
{
    private readonly ServicioTareas _tareas;
    private readonly ServicioCursos _cursos;
    private readonly ServicioNotificaciones _notificaciones;

    [ObservableProperty]
    private TareaDTO? tarea;

    [ObservableProperty]
    private bool ocupado;

    [ObservableProperty]
    private bool estaCompletada;

    [ObservableProperty]
    private string textoCompletar = "Marcar como completada";

    // INYECCIÓN DE DEPENDENCIAS
    public DetalleTareaVM(
        ServicioTareas tareas,
        ServicioCursos cursos,
        ServicioNotificaciones notificaciones)
    {
        _tareas = tareas;
        _cursos = cursos;
        _notificaciones = notificaciones;
    }

    // CARGAR DETALLE
    public async Task Cargar(int id)
    {
        Tarea = await _tareas.ObtenerPorId(id);

        if (Tarea == null)
            return;

        ActualizarEstado();
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

    // VOLVER
    [RelayCommand]
    private async Task Volver()
    {
        await Shell.Current.GoToAsync("..");
    }
}
