using System.Collections.ObjectModel;
using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StudySpace.VistaModelos;

public partial class PrincipalVM : ObservableObject
{
    private readonly ServicioTareas _tareas;
    private readonly ServicioAutenticacion _auth;

    public ObservableCollection<TareaDTO> Tareas { get; } = new();

    [ObservableProperty] private string saludo = "";
    [ObservableProperty] private int pendientes;
    [ObservableProperty] private int completadas;
    [ObservableProperty] private int vencidas;
    [ObservableProperty] private bool estaVacio = true;

    public PrincipalVM(ServicioTareas tareas, ServicioAutenticacion auth)
    {
        _tareas = tareas;
        _auth = auth;
    }

    public async Task Cargar()
    {
        var nombre = _auth.UsuarioActual?.Nombre ?? "";
        var primero = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        Saludo = primero == "" ? "Hola" : $"Hola, {primero}";

        var todas = await _tareas.ObtenerTareas();
        Pendientes = todas.Count(t => !t.Completada);
        Completadas = todas.Count(t => t.Completada);
        Vencidas = todas.Count(t => t.EstaVencida);

        Tareas.Clear();
        foreach (var t in todas.OrderBy(t => t.Completada).ThenBy(t => t.FechaLimite))
            Tareas.Add(t);
        EstaVacio = Tareas.Count == 0;
    }

    public async Task Completar(TareaDTO tarea)
    {
        await _tareas.AlternarCompletada(tarea.Id);
        await Cargar();
    }

    [RelayCommand]
    private async Task NuevaTarea() => await Shell.Current.GoToAsync("nuevatarea");
}