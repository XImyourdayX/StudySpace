using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaRegistro : ContentPage
{
    public PaginaRegistro(RegistroVM vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        if (PasswordEntry == null || TogglePasswordButton == null) return;
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        var glyph = PasswordEntry.IsPassword ? "\ue8f4" : "\ue8f5";
        TogglePasswordButton.Source = new FontImageSource
        {
            FontFamily = "IconosMaterial",
            Glyph = glyph,
            Size = 20,
            Color = (Color)Application.Current.Resources["TextoSecundarioClaro"]
        };
    }

    private void OnToggleConfirmClicked(object sender, EventArgs e)
    {
        if (ConfirmEntry == null || ToggleConfirmButton == null) return;
        ConfirmEntry.IsPassword = !ConfirmEntry.IsPassword;
        var glyph = ConfirmEntry.IsPassword ? "\ue8f4" : "\ue8f5";
        ToggleConfirmButton.Source = new FontImageSource
        {
            FontFamily = "IconosMaterial",
            Glyph = glyph,
            Size = 20,
            Color = (Color)Application.Current.Resources["TextoSecundarioClaro"]
        };
    }
}