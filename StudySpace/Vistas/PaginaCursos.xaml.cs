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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Cargar();
    }
}