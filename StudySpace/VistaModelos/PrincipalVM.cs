
using System.Collections.ObjectModel;
using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySpace.Servicios;

namespace StudySpace.VistaModelos;

public partial class PrincipalVM : ObservableObject
{
    private readonly ServicioTareas _tareas;
    private readonly ServicioAutenticacion _auth;
    private readonly ServicioCursos _cursos;
    private readonly ServicioNotificaciones _notificaciones;

    public ObservableCollection<TareaDTO> Tareas { get; } = new();

    [ObservableProperty]
    private string saludo = "";

    [ObservableProperty]
    private int pendientes;

    [ObservableProperty]
    private int completadas;

    [ObservableProperty]
    private int vencidas;

    [ObservableProperty]
    private int cursosActivos;

    [ObservableProperty]
    private bool estaVacio = true;

    // HU08 - PROGRESO ACADÉMICO
    [ObservableProperty]
    private int porcentajeProgreso;

    [ObservableProperty]
    private double progreso;

    public PrincipalVM(
        ServicioTareas tareas,
        ServicioAutenticacion auth,
        ServicioCursos cursos,
        ServicioNotificaciones notificaciones)
    {
        _tareas = tareas;
        _auth = auth;
        _cursos = cursos;
        _notificaciones = notificaciones;
    }

    // HU06 - OBTENER EL ORDEN DE PRIORIDAD
    private static int OrdenPrioridad(string? prioridad)
    {
        return prioridad?.Trim().ToLowerInvariant() switch
        {
            "alta" => 0,
            "media" => 1,
            "baja" => 2,
            _ => 3
        };
    }

    // CARGAR INFORMACIÓN DE INICIO
    public async Task Cargar()
    {
        var nombre = _auth.UsuarioActual?.Nombre ?? "";

        var primero = nombre.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "";

        Saludo = primero == ""
            ? "Hola"
            : $"Hola, {primero}";

        // OBTENER TAREAS DEL USUARIO
        var todas = await _tareas.ObtenerTareas();

        Pendientes = todas.Count(t => !t.Completada);
        Completadas = todas.Count(t => t.Completada);
        Vencidas = todas.Count(t => t.EstaVencida);

        // HU08 - CALCULAR PROGRESO ACADÉMICO
        int totalTareas = todas.Count;

        if (totalTareas == 0)
        {
            PorcentajeProgreso = 0;
            Progreso = 0;
        }
        else
        {
            Progreso = (double)Completadas / totalTareas;

            PorcentajeProgreso = (int)Math.Round(
                Progreso * 100,
                MidpointRounding.AwayFromZero);
        }

        // CONTAR CURSOS REGISTRADOS
        var cursos = await _cursos.ObtenerCursos();

        CursosActivos = cursos.Count;

        // HU06 - ORDENAR POR ESTADO Y PRIORIDAD
        var tareasOrdenadas = todas
            .OrderBy(t => t.Completada)
            .ThenBy(t => OrdenPrioridad(t.Prioridad))
            .ThenBy(t => t.FechaLimite)
            .ToList();

        Tareas.Clear();

        foreach (var tarea in tareasOrdenadas)
        {
            Tareas.Add(tarea);
        }

        EstaVacio = Tareas.Count == 0;
    }

    // COMPLETAR O REABRIR TAREA DESDE INICIO
    public async Task Completar(TareaDTO tarea)
    {
        if (tarea == null)
            return;

        try
        {
            int tareaId = tarea.Id;

            // CAMBIAR ESTADO EN SQLITE
            await _tareas.AlternarCompletada(tareaId);

            // OBTENER ESTADO ACTUALIZADO
            var tareaActualizada =
                await _tareas.ObtenerPorId(tareaId);

            if (tareaActualizada != null)
            {
                try
                {
                    if (tareaActualizada.Completada)
                    {
                        // CANCELAR RECORDATORIO
                        _notificaciones.CancelarRecordatorio(
                            tareaId);
                    }
                    else
                    {
                        // REPROGRAMAR AL VOLVER A PENDIENTE
                        var fechaAviso =
                            tareaActualizada.FechaLimite
                                .AddMinutes(
                                    -tareaActualizada.MinutosAviso);

                        if (fechaAviso > DateTime.Now)
                        {
                            var listaCursos =
                                await _cursos.ObtenerCursos();

                            var curso = listaCursos.FirstOrDefault(
                                c => c.Id == tareaActualizada.CursoId);

                            string nombreCurso =
                                curso?.Nombre ?? "";

                            await _notificaciones
                                .ProgramarRecordatorio(
                                    tareaId,
                                    tareaActualizada.Titulo,
                                    tareaActualizada.FechaLimite,
                                    tareaActualizada.MinutosAviso,
                                    nombreCurso);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"HU05 - Error en recordatorio: {ex.Message}");
                }
            }

            // ACTUALIZAR CONTADORES, PROGRESO Y LISTA
            await Cargar();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Aviso",
                $"No se pudo actualizar la tarea: {ex.Message}",
                "OK");
        }
    }

    // NAVEGAR A NUEVA TAREA
    [RelayCommand]
    private async Task NuevaTarea()
    {
        await Shell.Current.GoToAsync("nuevatarea");
    }
}

