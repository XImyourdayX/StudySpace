using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aplicacion.CasosDeUso;

namespace StudySpace.VistaModelos;

public partial class InicioSesionVM : ObservableObject
{
    private readonly ServicioAutenticacion _auth;

    [ObservableProperty] private string correo = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private bool ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string mensajeError = "";

    public bool TieneError => !string.IsNullOrEmpty(MensajeError);

    public InicioSesionVM(ServicioAutenticacion auth) => _auth = auth;

    [RelayCommand]
    private async Task IniciarSesion()
    {
        MensajeError = "";
        Ocupado = true;
        var (ok, mensaje) = await _auth.IniciarSesion(Correo, Password);
        Ocupado = false;

        if (!ok)
        {
            MensajeError = mensaje;
            return;
        }

        // Provisional: luego lo cambiamos para ir a la pantalla principal
        await Shell.Current.GoToAsync("//principal");
    }

    [RelayCommand]
    private async Task IrARegistro()
        => await Shell.Current.GoToAsync("registro");
    [RelayCommand]
    private async Task Volver() => await Shell.Current.GoToAsync("..");
}