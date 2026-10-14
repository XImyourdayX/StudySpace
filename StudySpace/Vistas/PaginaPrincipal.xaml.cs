using Aplicacion.DTOs;
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaPrincipal : ContentPage
{
    private readonly PrincipalVM _vm;

    public PaginaPrincipal(PrincipalVM vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Cargar();
    }

    private async void OnCompletarTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is TareaDTO tarea)
            await _vm.Completar(tarea);
    }
}