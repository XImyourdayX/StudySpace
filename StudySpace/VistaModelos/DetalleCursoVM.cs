using System.Collections.ObjectModel;
using System.Linq;
using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StudySpace.VistaModelos;

// Agrupación solo de presentación: no cambia ni guarda información en SQLite.
public sealed class GrupoPrioridadTareas : ObservableCollection<TareaDTO>
{
    public string Nombre { get; }
    public string ColorPrioridad { get; }
    public string ConteoTexto => Count == 1 ? "1 tarea" : $"{Count} tareas";

    public GrupoPrioridadTareas(string nombre, string color, IEnumerable<TareaDTO> tareas)
        : base(tareas)
    {
        Nombre = nombre;
        ColorPrioridad = color;
    }
}

public partial class DetalleCursoVM : ObservableObject
{
    private readonly ServicioCursos _cursos;
    private readonly ServicioTareas _tareas;
    private int _cursoId;

    private enum FiltroTareas { Todas, Pendientes, Completadas }
    private FiltroTareas _filtroActual = FiltroTareas.Todas;

    // Se conservan las tareas de origen; las agrupaciones son solo para la pantalla.
    public ObservableCollection<TareaDTO> Tareas { get; } = new();
    public ObservableCollection<GrupoPrioridadTareas> GruposTareas { get; } = new();

    [ObservableProperty] private string nombreCurso = "";
    [ObservableProperty] private string colorCurso = "#304BFF";
    [ObservableProperty] private int totalTareas;
    [ObservableProperty] private int tareasPendientes;
    [ObservableProperty] private int tareasCompletadas;
    [ObservableProperty] private bool estaVacio = true;
    [ObservableProperty] private bool ocupado;

    [ObservableProperty] private double progresoCurso;
    [ObservableProperty] private string progresoTexto = "0%";
    [ObservableProperty] private string resumenCurso = "Sin tareas registradas";

    [ObservableProperty] private bool pestanaTareasActiva = true;
    [ObservableProperty] private bool pestanaInformacionActiva;
    [ObservableProperty] private bool filtroTodasActivo = true;
    [ObservableProperty] private bool filtroPendientesActivo;
    [ObservableProperty] private bool filtroCompletadasActivo;

    public DetalleCursoVM(ServicioCursos cursos, ServicioTareas tareas)
    {
        _cursos = cursos;
        _tareas = tareas;
    }

    // Cargar solo datos de la cuenta activa, como en la versión validada.
    public async Task Cargar(int cursoId)
    {
        _cursoId = cursoId;
        Tareas.Clear();
        GruposTareas.Clear();
        NombreCurso = "";
        ColorCurso = "#304BFF";
        TotalTareas = 0;
        TareasPendientes = 0;
        TareasCompletadas = 0;
        ProgresoCurso = 0;
        ProgresoTexto = "0%";
        ResumenCurso = "Sin tareas registradas";
        EstaVacio = true;

        if (_cursoId <= 0)
            return;

        Ocupado = true;
        try
        {
            var cursos = await _cursos.ObtenerCursos();
            var curso = cursos.FirstOrDefault(c => c.Id == _cursoId);
            if (curso == null)
                return;

            NombreCurso = curso.Nombre;
            ColorCurso = curso.Color;

            var todas = await _tareas.ObtenerTareas();
            var tareasCurso = todas
                .Where(t => t.CursoId == _cursoId)
                .OrderBy(t => t.Completada)
                .ThenBy(t => t.FechaLimite)
                .ToList();

            foreach (var tarea in tareasCurso)
                Tareas.Add(tarea);

            TotalTareas = tareasCurso.Count;
            TareasPendientes = tareasCurso.Count(t => !t.Completada);
            TareasCompletadas = tareasCurso.Count(t => t.Completada);
            EstaVacio = TotalTareas == 0;

            ProgresoCurso = TotalTareas == 0 ? 0 : (double)TareasCompletadas / TotalTareas;
            ProgresoTexto = $"{System.Math.Round(ProgresoCurso * 100):0}%";
            ResumenCurso = TotalTareas == 0
                ? "Aún no hay tareas en este curso"
                : $"{TotalTareas} {(TotalTareas == 1 ? "tarea" : "tareas")} · {TareasCompletadas} {(TareasCompletadas == 1 ? "completada" : "completadas")}";

            ActualizarGrupos();
        }
        finally
        {
            Ocupado = false;
        }
    }

    private void ActualizarGrupos()
    {
        GruposTareas.Clear();
        IEnumerable<TareaDTO> filtradas = _filtroActual switch
        {
            FiltroTareas.Pendientes => Tareas.Where(t => !t.Completada),
            FiltroTareas.Completadas => Tareas.Where(t => t.Completada),
            _ => Tareas
        };

        var lista = filtradas.ToList();
        AgregarGrupo(lista, "Alta", "#EF4444", t => string.Equals(t.Prioridad, "Alta", StringComparison.OrdinalIgnoreCase));
        AgregarGrupo(lista, "Media", "#F59E0B", t => string.Equals(t.Prioridad, "Media", StringComparison.OrdinalIgnoreCase));
        // El DTO interpreta cualquier otra prioridad como Baja; mantenemos la misma convención.
        AgregarGrupo(lista, "Baja", "#22B981", t =>
            !string.Equals(t.Prioridad, "Alta", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(t.Prioridad, "Media", StringComparison.OrdinalIgnoreCase));
    }

    private void AgregarGrupo(IEnumerable<TareaDTO> lista, string nombre, string color, Func<TareaDTO, bool> condicion)
    {
        var tareas = lista.Where(condicion).ToList();
        if (tareas.Count > 0)
            GruposTareas.Add(new GrupoPrioridadTareas(nombre, color, tareas));
    }

    [RelayCommand] private void MostrarTareas()
    {
        PestanaTareasActiva = true;
        PestanaInformacionActiva = false;
    }

    [RelayCommand] private void MostrarInformacion()
    {
        PestanaTareasActiva = false;
        PestanaInformacionActiva = true;
    }

    [RelayCommand] private void MostrarTodas()
    {
        _filtroActual = FiltroTareas.Todas;
        CambiarEstadoFiltros();
    }

    [RelayCommand] private void MostrarPendientes()
    {
        _filtroActual = FiltroTareas.Pendientes;
        CambiarEstadoFiltros();
    }

    [RelayCommand] private void MostrarCompletadas()
    {
        _filtroActual = FiltroTareas.Completadas;
        CambiarEstadoFiltros();
    }

    private void CambiarEstadoFiltros()
    {
        FiltroTodasActivo = _filtroActual == FiltroTareas.Todas;
        FiltroPendientesActivo = _filtroActual == FiltroTareas.Pendientes;
        FiltroCompletadasActivo = _filtroActual == FiltroTareas.Completadas;
        ActualizarGrupos();
    }

    // Comandos originales: el comportamiento y las rutas se conservan.
    [RelayCommand]
    private async Task AbrirTarea(TareaDTO? tarea)
    {
        if (tarea == null || Ocupado)
            return;

        await Shell.Current.GoToAsync(
            "detalletarea",
            new Dictionary<string, object> { { "id", tarea.Id } });
    }

    [RelayCommand]
    private async Task NuevaTarea()
    {
        if (_cursoId <= 0 || Ocupado)
            return;

        await Shell.Current.GoToAsync(
            "nuevatarea",
            new Dictionary<string, object> { { "cursoId", _cursoId } });
    }

    // No elimina cursos con tareas, igual que la versión validada.
    [RelayCommand]
    private async Task EliminarCurso()
    {
        if (_cursoId <= 0 || Ocupado)
            return;

        if (TotalTareas > 0)
        {
            await Shell.Current.DisplayAlert(
                "No se puede eliminar",
                "Este curso tiene tareas registradas. Elimínalas o reasígnalas antes de borrar el curso.",
                "Entendido");
            return;
        }

        bool confirmado = await Shell.Current.DisplayAlert(
            "Eliminar curso",
            $"¿Deseas eliminar '{NombreCurso}'? Esta acción no se puede deshacer.",
            "Eliminar",
            "Cancelar");

        if (!confirmado)
            return;

        Ocupado = true;
        try
        {
            var (exito, mensaje) = await _cursos.EliminarCurso(_cursoId);
            if (!exito)
            {
                await Shell.Current.DisplayAlert("No se pudo eliminar", mensaje, "OK");
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error al eliminar", $"No se pudo eliminar el curso: {ex.Message}", "OK");
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private async Task Volver()
    {
        await Shell.Current.GoToAsync("..");
    }
}
