
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaVerificarCorreo : ContentPage
{
    private readonly VerificarCorreoVM _viewModel;

    public PaginaVerificarCorreo(
        VerificarCorreoVM viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        // CONECTAR LA PANTALLA CON SU VIEWMODEL
        BindingContext = _viewModel;
    }
}
