using StudySpace.Vistas;

namespace StudySpace;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("login", typeof(PaginaInicioSesion));
        Routing.RegisterRoute("registro", typeof(PaginaRegistro));
        Routing.RegisterRoute("nuevatarea", typeof(PaginaNuevaTarea));
    }
}