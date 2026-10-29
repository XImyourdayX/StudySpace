
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaCalendario : ContentPage
{
    private readonly CalendarioVM _vm;

    public PaginaCalendario(CalendarioVM vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _vm.Cargar();
    }
}
