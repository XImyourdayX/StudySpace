using Aplicacion.CasosDeUso;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StudySpace.VistaModelos;

public partial class PerfilVM : ObservableObject
{
    private readonly ServicioAutenticacion _auth;

    [ObservableProperty] private string nombre = "";
    [ObservableProperty] private string correo = "";

    public PerfilVM(ServicioAutenticacion auth) => _auth = auth;

    public void Cargar()
    {
        Nombre = _auth.UsuarioActual?.Nombre ?? "";
        Correo = _auth.UsuarioActual?.Correo ?? "";
    }

    [RelayCommand]
    private async Task CerrarSesion()
    {
        var confirmar = await Shell.Current.DisplayAlert("Cerrar sesión", "¿Seguro que quieres salir?", "Sí", "No");
        if (!confirmar) return;

        _auth.CerrarSesion();
        await Shell.Current.GoToAsync("//bienvenida");
    }
}