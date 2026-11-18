using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaReportes : ContentPage
{
    private readonly ReportesVM _vm;

    public PaginaReportes(ReportesVM vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Cargar();
    }

    private async void OnVolverClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
