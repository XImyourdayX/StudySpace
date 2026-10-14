using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace StudySpace.VistaModelos;

public partial class NuevaTareaVM : ObservableObject
{
    private readonly ServicioTareas _tareas;
    private readonly ServicioCursos _cursos;
    private readonly int[] _minutosAviso = { 15, 60, 1440, 2880 };

    public ObservableCollection<CursoDTO> Cursos { get; } = new();
    public List<string> Prioridades { get; } = new() { "Baja", "Media", "Alta" };
    public List<string> Avisos { get; } = new() { "15 minutos antes", "1 hora antes", "1 día antes", "2 días antes" };

    [ObservableProperty] private string titulo = "";
    [ObservableProperty] private string descripcion = "";
    [ObservableProperty] private CursoDTO? cursoSeleccionado;
    [ObservableProperty] private DateTime fecha = DateTime.Today;
    [ObservableProperty] private TimeSpan hora = new(23, 59, 0);
    [ObservableProperty] private string prioridad = "Media";
    [ObservableProperty] private int indiceAviso = 2;
    [ObservableProperty] private bool ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string mensajeError = "";

    public bool TieneError => !string.IsNullOrEmpty(MensajeError);

    public NuevaTareaVM(ServicioTareas tareas, ServicioCursos cursos)
    {
        _tareas = tareas;
        _cursos = cursos;
    }

    public async Task Cargar()
    {
        var lista = await _cursos.ObtenerCursos();
        Cursos.Clear();
        foreach (var c in lista) Cursos.Add(c);

        Titulo = "";
        Descripcion = "";
        CursoSeleccionado = Cursos.FirstOrDefault();
        Fecha = DateTime.Today;
        Hora = new TimeSpan(23, 59, 0);
        Prioridad = "Media";
        IndiceAviso = 2;
        MensajeError = "";
    }

    [RelayCommand]
    private async Task Guardar()
    {
        if (Ocupado) return;
        MensajeError = "";
        Ocupado = true;

        var fechaLimite = Fecha.Date + Hora;
        var minutos = _minutosAviso[Math.Max(IndiceAviso, 0)];

        var (ok, mensaje) = await _tareas.AgregarTarea(
            Titulo, Descripcion, CursoSeleccionado?.Id ?? 0, fechaLimite, Prioridad, minutos);

        Ocupado = false;
        if (!ok)
        {
            MensajeError = mensaje;
            return;
        }

        await Shell.Current.DisplayAlert("Listo", "Tarea guardada.", "OK");
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Volver() => await Shell.Current.GoToAsync("..");
}