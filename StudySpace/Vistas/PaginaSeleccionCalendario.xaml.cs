using StudySpace.Servicios;
using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;

namespace StudySpace.Vistas;

// Selector propio de Study Space: visual, con una sola selección y sin IDs visibles.
public partial class PaginaSeleccionCalendario : ContentPage
{
    private readonly TaskCompletionSource<OpcionCalendarioAndroid?> _resultado =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _cerrando;
    private bool _mostrandoOtros;
    private readonly IReadOnlyList<OpcionCalendarioAndroid> _todosCalendarios;
    private readonly IReadOnlyList<OpcionCalendarioAndroid> _calendariosGoogle;

    // CollectionView recibe una colección observable para que el filtro cambie sin recrear la pantalla.
    public ObservableCollection<OpcionCalendarioAndroid> Calendarios { get; }

    public PaginaSeleccionCalendario(IReadOnlyList<OpcionCalendarioAndroid> calendarios)
    {
        InitializeComponent();

        _todosCalendarios = calendarios;
        _calendariosGoogle = calendarios.Where(c => c.EsGoogle).ToList();
        var inicial = _calendariosGoogle.Count > 0 ? _calendariosGoogle : _todosCalendarios;
        Calendarios = new ObservableCollection<OpcionCalendarioAndroid>(inicial);
        BindingContext = this;

        int otros = calendarios.Count(c => !c.EsGoogle);
        BotonMostrarOtros.IsVisible = _calendariosGoogle.Count > 0 && otros > 0;
        if (_calendariosGoogle.Count > 0)
        {
            TextoAyuda.Text = "Elige uno de tus calendarios Google. Si está sincronizado en Android, " +
                              "la entrega podrá verse también en otros dispositivos.";
            BotonMostrarOtros.Text = $"Mostrar otros calendarios ({otros})";
        }
        else
        {
            TextoAyuda.Text = "No hay calendarios Google editables configurados. " +
                              "Puedes usar un calendario local, pero es posible que no se sincronice con Google.";
        }
    }

    private void OnMostrarOtrosClicked(object sender, EventArgs e)
    {
        _mostrandoOtros = !_mostrandoOtros;
        // El usuario debe seleccionar explícitamente un destino después de cambiar el filtro.
        ListaCalendarios.SelectedItem = null;
        Calendarios.Clear();
        var visibles = _mostrandoOtros ? _todosCalendarios : _calendariosGoogle;
        foreach (var calendario in visibles)
            Calendarios.Add(calendario);
        BotonMostrarOtros.Text = _mostrandoOtros
            ? "Mostrar solo calendarios Google"
            : $"Mostrar otros calendarios ({_todosCalendarios.Count(c => !c.EsGoogle)})";
    }

    public Task<OpcionCalendarioAndroid?> EsperarSeleccionAsync() => _resultado.Task;

    private void OnSeleccionCambio(object sender, SelectionChangedEventArgs e)
    {
        var calendario = e.CurrentSelection.FirstOrDefault() as OpcionCalendarioAndroid;
        BotonConfirmar.IsEnabled = calendario is not null;
        TextoSeleccion.Text = calendario is null
            ? "Selecciona una opción para continuar."
            : $"Destino: {calendario.NombreVisible}";
    }

    private async void OnConfirmarClicked(object sender, EventArgs e)
    {
        await CerrarAsync(ListaCalendarios.SelectedItem as OpcionCalendarioAndroid);
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        await CerrarAsync(null);
    }

    private async Task CerrarAsync(OpcionCalendarioAndroid? seleccionado)
    {
        if (_cerrando)
            return;
        _cerrando = true;
        try
        {
            await Navigation.PopModalAsync();
            _resultado.TrySetResult(seleccionado);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU15 selector: {ex}");
            _resultado.TrySetResult(null);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () => await CerrarAsync(null));
        return true;
    }
}
