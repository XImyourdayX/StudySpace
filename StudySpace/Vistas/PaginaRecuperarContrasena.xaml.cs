
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaRecuperarContrasena : ContentPage
{
    private readonly RecuperarContrasenaVM _viewModel;

    public PaginaRecuperarContrasena(
        RecuperarContrasenaVM viewModel)
    {
        InitializeComponent();

        // RECIBIR EL VIEWMODEL MEDIANTE
        // INYECCION DE DEPENDENCIAS.
        _viewModel = viewModel;

        // CONECTAR LOS CAMPOS Y BOTONES
        // DE LA PANTALLA CON EL VIEWMODEL.
        BindingContext = _viewModel;
    }
}
