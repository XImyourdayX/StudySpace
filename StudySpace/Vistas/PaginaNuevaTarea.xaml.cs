using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaNuevaTarea : ContentPage
{
    private readonly NuevaTareaVM _vm;

    public PaginaNuevaTarea(NuevaTareaVM vm)
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