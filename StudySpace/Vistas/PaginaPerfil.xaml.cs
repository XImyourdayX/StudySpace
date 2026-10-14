using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaPerfil : ContentPage
{
    private readonly PerfilVM _vm;

    public PaginaPerfil(PerfilVM vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Cargar();
    }
}