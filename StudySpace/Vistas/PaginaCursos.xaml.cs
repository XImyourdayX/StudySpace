
using Aplicacion.DTOs;
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaCursos : ContentPage
{
    private readonly CursosVM _vm;

    public PaginaCursos(CursosVM vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // ACTUALIZAR LOS CURSOS AL ENTRAR
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Cargar();
    }

    // ABRIR DETALLE DEL CURSO SELECCIONADO
    private async void OnCursoTapped(
        object? sender,
        TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext
            is not CursoDTO curso)
            return;

        await Shell.Current.GoToAsync(
            "detallecurso",
            new Dictionary<string, object>
            {
                { "id", curso.Id }
            });
    }
}
