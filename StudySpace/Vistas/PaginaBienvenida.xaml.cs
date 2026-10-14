using Aplicacion.CasosDeUso;

namespace StudySpace.Vistas;

public partial class PaginaBienvenida : ContentPage
{
    private bool _sesionRevisada;

    public PaginaBienvenida()
    {
        InitializeComponent();
    }

    private async void OnComenzar(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("registro");

    private async void OnTengoCuenta(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("login");

        protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_sesionRevisada) return;
        _sesionRevisada = true;

        try
        {
            var auth = IPlatformApplication.Current?.Services.GetService<ServicioAutenticacion>();
            if (auth == null) return;

            if (await auth.RestaurarSesion())
            {
                await Task.Delay(150);
                await Shell.Current.GoToAsync("//principal");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de sesión", ex.Message, "OK");
        }
    }
}
