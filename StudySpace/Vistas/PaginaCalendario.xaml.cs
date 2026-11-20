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

    private void OnTareaTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is TareaDTO tarea &&
            _vm.AbrirTareaCommand.CanExecute(tarea))
        {
            _vm.AbrirTareaCommand.Execute(tarea);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Cargar();
    }
}
