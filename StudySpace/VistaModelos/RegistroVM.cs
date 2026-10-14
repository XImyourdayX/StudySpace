using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aplicacion.CasosDeUso;

namespace StudySpace.VistaModelos;

public partial class RegistroVM : ObservableObject
{
    private readonly ServicioAutenticacion _auth;

    [ObservableProperty] private string nombre = "";
    [ObservableProperty] private string correo = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string confirmar = "";
    [ObservableProperty] private bool ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string mensajeError = "";

    public bool TieneError => !string.IsNullOrEmpty(MensajeError);

    public RegistroVM(ServicioAutenticacion auth) => _auth = auth;

    [RelayCommand]
    private async Task CrearCuenta()
    {
        MensajeError = "";
        Ocupado = true;
        var (ok, mensaje) = await _auth.Registrar(Nombre, Correo, Password, Confirmar);
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
    private async Task Volver() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task IrAInicioSesion() => await Shell.Current.GoToAsync("../login");
}