
using StudySpace.Vistas;

namespace StudySpace;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // AUTENTICACIÓN
        Routing.RegisterRoute(
            "login",
            typeof(PaginaInicioSesion));

        Routing.RegisterRoute(
            "registro",
            typeof(PaginaRegistro));

        // RECUPERACIÓN DE CONTRASEÑA
        Routing.RegisterRoute(
            "recuperarcontrasena",
            typeof(PaginaRecuperarContrasena));

        // VERIFICACIÓN DE CORREO
        Routing.RegisterRoute(
            "verificarcorreo",
            typeof(PaginaVerificarCorreo));

        // NUEVA TAREA
        Routing.RegisterRoute(
            "nuevatarea",
            typeof(PaginaNuevaTarea));

        // HU12 - DETALLE DE TAREA
        Routing.RegisterRoute(
            "detalletarea",
            typeof(PaginaDetalleTarea));

        // DETALLE DE CURSO
        Routing.RegisterRoute(
            "detallecurso",
            typeof(PaginaDetalleCurso));
    }
}
