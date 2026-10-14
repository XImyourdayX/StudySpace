using System.Collections.ObjectModel;
using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StudySpace.VistaModelos;

public partial class CursosVM : ObservableObject
{
    private readonly ServicioCursos _cursos;

    public ObservableCollection<CursoDTO> Cursos { get; } = new();

    [ObservableProperty] private bool estaVacio = true;

    public CursosVM(ServicioCursos cursos) => _cursos = cursos;

    public async Task Cargar()
    {
        var lista = await _cursos.ObtenerCursos();
        Cursos.Clear();
        foreach (var c in lista) Cursos.Add(c);
        EstaVacio = Cursos.Count == 0;
    }

    [RelayCommand]
    private async Task Agregar()
    {
        var nombre = await Shell.Current.DisplayPromptAsync("Nuevo curso", "Nombre del curso:");
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var (ok, mensaje) = await _cursos.AgregarCurso(nombre);
        if (!ok)
        {
            await Shell.Current.DisplayAlert("Aviso", mensaje, "OK");
            return;
        }
        await Cargar();
    }
    [RelayCommand]
    private async Task NuevaTarea() => await Shell.Current.GoToAsync("nuevatarea");
}