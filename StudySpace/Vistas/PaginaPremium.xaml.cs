namespace StudySpace.Vistas;

public partial class PaginaPremium : ContentPage
{
    public PaginaPremium()
    {
        InitializeComponent();
    }

    private async void OnVolverClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    // Solo información. No se inicia ningún pago ni suscripción.
    private async void OnObtenerPremiumClicked(object? sender, EventArgs e)
    {
        await DisplayAlert(
            "Study Space Premium",
            "¡Gracias por tu interés! Premium estará disponible en una versión futura. " +
            "Por ahora puedes seguir disfrutando de todas las herramientas gratuitas de Study Space. " +
            "No se ha realizado ningún cobro ni suscripción.",
            "Entendido");
    }
}
