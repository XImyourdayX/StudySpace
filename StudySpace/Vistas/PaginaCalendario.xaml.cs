using Aplicacion.DTOs;
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaCalendario : ContentPage
{
    private readonly CalendarioVM _vm;

    public PaginaCalendario(CalendarioVM vm)
    {
        _vm = vm;
        BindingContext = vm;
        InitializeComponent();
    }

    // El calendario interno usa los mismos comandos del ViewModel,
    // evitando enlaces compilados a un antecesor todavia no disponible.
    private void OnDiaTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is DiaCalendario dia &&
            _vm.SeleccionarDiaCommand.CanExecute(dia))
        {
            _vm.SeleccionarDiaCommand.Execute(dia);
        }
    }

    private async void OnTareaSeleccionada(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView lista ||
            e.CurrentSelection.FirstOrDefault() is not TareaDTO tarea)
            return;

        // Permite volver a abrir la misma tarea al regresar.
        lista.SelectedItem = null;
        if (!_vm.AbrirTareaCommand.CanExecute(tarea))
            return;

        await _vm.AbrirTareaCommand.ExecuteAsync(tarea);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Cargar();
    }
}
