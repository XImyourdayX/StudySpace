
using System.Collections.ObjectModel;
using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StudySpace.VistaModelos;

public partial class CursosVM : ObservableObject
{
    private readonly ServicioCursos _cursos;
    private readonly ServicioTareas _tareas;

    private List<CursoDTO> _todosLosCursos = new();
    private List<TareaDTO> _todasLasTareas = new();

    public ObservableCollection<CursoDTO> Cursos { get; } = new();

    [ObservableProperty]
    private bool estaVacio = true;

    // Cantidad real, independiente de los filtros.
    [ObservableProperty]
    private int totalCursos;

    [ObservableProperty]
    private string filtroSeleccionado = "Actuales";

    [ObservableProperty]
    private bool filtroTodosActivo;

    [ObservableProperty]
    private bool filtroActualesActivo = true;

    [ObservableProperty]
    private bool filtroFinalizadosActivo;

    public CursosVM(
        ServicioCursos cursos,
        ServicioTareas tareas)
    {
        _cursos = cursos;
        _tareas = tareas;
    }

    public async Task Cargar()
    {
        _todosLosCursos = await _cursos.ObtenerCursos();
        _todasLasTareas = await _tareas.ObtenerTareas();

        // Datos de presentación calculados de las tareas ya cargadas.
        // No hay cambios en la base de datos ni consultas adicionales.
        TotalCursos = _todosLosCursos.Count;
        foreach (var curso in _todosLosCursos)
        {
            var tareasDelCurso = _todasLasTareas
                .Where(t => t.CursoId == curso.Id).ToList();
            curso.TotalTareas = tareasDelCurso.Count;
            curso.TareasCompletadas = tareasDelCurso.Count(t => t.Completada);
        }

        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        IEnumerable<CursoDTO> lista = _todosLosCursos;

        if (FiltroSeleccionado == "Actuales")
        {
            lista = lista.Where(c =>
            {
                var tareasCurso = _todasLasTareas
                    .Where(t => t.CursoId == c.Id)
                    .ToList();

                return tareasCurso.Count == 0 ||
                       tareasCurso.Any(t => !t.Completada);
            });
        }
        else if (FiltroSeleccionado == "Finalizados")
        {
            lista = lista.Where(c =>
            {
                var tareasCurso = _todasLasTareas
                    .Where(t => t.CursoId == c.Id)
                    .ToList();

                return tareasCurso.Count > 0 &&
                       tareasCurso.All(t => t.Completada);
            });
        }

        Cursos.Clear();

        foreach (var curso in lista)
            Cursos.Add(curso);

        EstaVacio = Cursos.Count == 0;

        FiltroTodosActivo = FiltroSeleccionado == "Todos";
        FiltroActualesActivo = FiltroSeleccionado == "Actuales";
        FiltroFinalizadosActivo = FiltroSeleccionado == "Finalizados";
    }

    [RelayCommand]
    private void MostrarTodos()
    {
        FiltroSeleccionado = "Todos";
        AplicarFiltro();
    }

    [RelayCommand]
    private void MostrarActuales()
    {
        FiltroSeleccionado = "Actuales";
        AplicarFiltro();
    }

    [RelayCommand]
    private void MostrarFinalizados()
    {
        FiltroSeleccionado = "Finalizados";
        AplicarFiltro();
    }

    [RelayCommand]
    private async Task Agregar()
    {
        var nombre = await Shell.Current.DisplayPromptAsync(
            "Nuevo curso",
            "Nombre del curso:");

        if (string.IsNullOrWhiteSpace(nombre))
            return;

        var (ok, mensaje) = await _cursos.AgregarCurso(nombre);

        if (!ok)
        {
            await Shell.Current.DisplayAlert(
                "Aviso", mensaje, "OK");
            return;
        }

        await Cargar();
    }

    [RelayCommand]
    private async Task NuevaTarea()
    {
        await Shell.Current.GoToAsync("nuevatarea");
    }
}
