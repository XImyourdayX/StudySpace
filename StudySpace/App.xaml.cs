
namespace StudySpace
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // RECUPERAR EL TEMA GUARDADO
            RestaurarTema();
        }

        // RESTAURAR PREFERENCIA DEL USUARIO
        private void RestaurarTema()
        {
            var temaGuardado = Preferences.Default.Get(
                "TemaAplicacion",
                "Sistema");

            UserAppTheme = temaGuardado switch
            {
                "Claro" => AppTheme.Light,
                "Oscuro" => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }

        protected override Window CreateWindow(
            IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}
