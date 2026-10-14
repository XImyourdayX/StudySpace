using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaInicioSesion : ContentPage
{
    public PaginaInicioSesion(InicioSesionVM vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        if (PasswordEntry == null || TogglePasswordButton == null) return;
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        // Cambiar icono según el estado (mostrar u ocultar)
        var glyph = PasswordEntry.IsPassword ? "\ue8f4" : "\ue8f5";
        TogglePasswordButton.Source = new FontImageSource
        {
            FontFamily = "IconosMaterial",
            Glyph = glyph,
            Size = 20,
            Color = (Color)Application.Current.Resources["TextoSecundarioClaro"]
        };
    }
}