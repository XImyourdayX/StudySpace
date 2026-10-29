
using System.Collections.ObjectModel;
using System.Linq;
using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StudySpace.VistaModelos;

public partial class DetalleCursoVM : ObservableObject
{
    private readonly ServicioCursos _cursos;
    private readonly ServicioTareas _tareas;

    public ObservableCollection<TareaDTO> Tareas { get; } = new();

    [ObservableProperty]
    private string nombreCurso = "";

    [ObservableProperty]
    private string colorCurso = "#304BFF";

    [ObservableProperty]
    private int totalTareas;

    [ObservableProperty]
    private int tareasPendientes;

    [ObservableProperty]
    private int tareasCompletadas;

    [ObservableProperty]
    private bool estaVacio = true;

    [ObservableProperty]
    private bool ocupado;

    private int _cursoId;

    public DetalleCursoVM(
        ServicioCursos cursos,
        ServicioTareas tareas)
    {
        _cursos = cursos;
        _tareas = tareas;
    }

    // CARGAR CURSO Y SUS TAREAS
    public async Task Cargar(int cursoId)
    {
        _cursoId = cursoId;

        Tareas.Clear();
        NombreCurso = "";
        ColorCurso = "#304BFF";
        TotalTareas = 0;
        TareasPendientes = 0;
        TareasCompletadas = 0;
        EstaVacio = true;

        if (_cursoId <= 0)
            return;

        Ocupado = true;

        try
        {
            // BUSCAR EL CURSO DEL USUARIO ACTUAL
            var cursos = await _cursos.ObtenerCursos();

            var curso = cursos.FirstOrDefault(
                c => c.Id == _cursoId);

            if (curso == null)
                return;

            NombreCurso = curso.Nombre;
            ColorCurso = curso.Color;

            // OBTENER SOLO LAS TAREAS DEL CURSO
            var todas = await _tareas.ObtenerTareas();

            var tareasCurso = todas
                .Where(t => t.CursoId == _cursoId)
                .OrderBy(t => t.Completada)
                .ThenBy(t => t.FechaLimite)
                .ToList();

            foreach (var tarea in tareasCurso)
                Tareas.Add(tarea);

            // CALCULAR RESUMEN
            TotalTareas = tareasCurso.Count;

            TareasPendientes = tareasCurso.Count(
                t => !t.Completada);

            TareasCompletadas = tareasCurso.Count(
                t => t.Completada);

            EstaVacio = Tareas.Count == 0;
        }
        finally
        {
            Ocupado = false;
        }
    }

    // ABRIR EL DETALLE DE UNA TAREA
    [RelayCommand]
    private async Task AbrirTarea(TareaDTO? tarea)
    {
        if (tarea == null || Ocupado)
            return;

        await Shell.Current.GoToAsync(
            "detalletarea",
            new Dictionary<string, object>
            {
                { "id", tarea.Id }
            });
    }

    // CREAR UNA NUEVA TAREA
    [RelayCommand]
    private async Task NuevaTarea()
    {
        await Shell.Current.GoToAsync("nuevatarea");
    }

    // VOLVER A MIS CURSOS
    [RelayCommand]
    private async Task Volver()
    {
        await Shell.Current.GoToAsync("..");
    }
}
