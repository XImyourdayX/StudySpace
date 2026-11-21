using StudySpace.Servicios;

namespace StudySpace.Vistas;

/// <summary>
/// HU17: resumen de datos, enlace a respaldos e información de privacidad.
/// El borrado local solo está disponible dentro de opciones avanzadas y
/// requiere dos confirmaciones. NO elimina datos del servidor ni de Drive.
/// </summary>
public partial class PaginaPrivacidadDatos : ContentPage
{
    private readonly ServicioGestionDatosLocales _gestion;
    private bool _ocupado;

    public PaginaPrivacidadDatos(ServicioGestionDatosLocales gestion)
    {
        InitializeComponent();
        _gestion = gestion;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ActualizarResumenAsync();
    }

    private async Task ActualizarResumenAsync()
    {
        try
        {
            var resumen = await _gestion.ConsultarAsync();
            CantidadCursosLabel.Text = resumen.Cursos.ToString();
            CantidadTareasLabel.Text = resumen.Tareas.ToString();
            EstadoDatosLabel.Text = "Datos de la cuenta activa, guardados en este dispositivo.";
            BorrarDatosButton.IsEnabled = !_ocupado && (resumen.Cursos + resumen.Tareas > 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU17 - Consultar resumen: {ex.GetType().Name}");
            CantidadCursosLabel.Text = "—";
            CantidadTareasLabel.Text = "—";
            EstadoDatosLabel.Text = "No pudimos consultar estos datos. Comprueba tu sesión.";
            BorrarDatosButton.IsEnabled = false;
        }
    }

    private async void OnVolverClicked(object? sender, EventArgs e)
    {
        if (_ocupado) return;
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCopiasSeguridadClicked(object? sender, EventArgs e)
    {
        if (_ocupado) return;
        await Shell.Current.GoToAsync("copiasseguridad");
    }

    private async void OnPoliticaPrivacidadClicked(object? sender, EventArgs e)
    {
        if (_ocupado) return;
        await Shell.Current.GoToAsync("documentolegal?tipo=privacidad");
    }

    private async void OnTerminosCondicionesClicked(object? sender, EventArgs e)
    {
        if (_ocupado) return;
        await Shell.Current.GoToAsync("documentolegal?tipo=terminos");
    }

    private void OnOpcionesAvanzadasClicked(object? sender, EventArgs e)
    {
        if (_ocupado) return;
        PanelAvanzado.IsVisible = !PanelAvanzado.IsVisible;
        OpcionesAvanzadasButton.Text = PanelAvanzado.IsVisible
            ? "Ocultar opciones avanzadas  ‹"
            : "Opciones avanzadas de datos  ›";
    }

    private async void OnBorrarDatosClicked(object? sender, EventArgs e)
    {
        if (_ocupado) return;
        ServicioGestionDatosLocales.Resumen resumen;
        try
        {
            resumen = await _gestion.ConsultarAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU17 - Comprobar datos: {ex.GetType().Name}");
            await DisplayAlert("No se pudo comprobar", "Comprueba que tengas una sesión activa e inténtalo de nuevo.", "Entendido");
            return;
        }

        if (resumen.Cursos + resumen.Tareas == 0)
        {
            await DisplayAlert("Sin datos para eliminar", "Esta cuenta no tiene cursos ni tareas locales que borrar.", "Entendido");
            await ActualizarResumenAsync();
            return;
        }

        // Dos confirmaciones: no se exige escribir una palabra, pero sí
        // se informa de la irreversibilidad y del alcance real del borrado.
        bool continuar = await DisplayAlert(
            "Eliminar información local",
            $"Se eliminarían {resumen.Cursos} curso(s) y {resumen.Tareas} tarea(s) de este teléfono. " +
            "¿Quieres revisar la confirmación final?",
            "Continuar", "Cancelar");
        if (!continuar) return;

        bool confirmar = await DisplayAlert(
            "Confirmación final",
            "Esta acción no se puede deshacer sin una copia válida. " +
            "NO elimina tu cuenta, los respaldos de Drive ni los eventos del calendario externo. " +
            "¿Eliminar definitivamente los cursos y tareas de este dispositivo?",
            "Sí, eliminar datos", "Conservar mis datos");
        if (!confirmar) return;

        _ocupado = true;
        BorrarDatosButton.IsEnabled = false;
        OpcionesAvanzadasButton.IsEnabled = false;
        IndicadorEliminacion.IsVisible = true;
        IndicadorEliminacion.IsRunning = true;

        try
        {
            var resultado = await _gestion.EliminarDatosAcademicosAsync();
            string detalle = resultado.AvisosConError > 0
                ? $" No se pudieron cancelar {resultado.AvisosConError} aviso(s) antiguos; revísalos en Android."
                : string.Empty;

            await DisplayAlert(
                "Datos locales eliminados",
                $"Se eliminaron {resultado.CursosEliminados} curso(s) y " +
                $"{resultado.TareasEliminadas} tarea(s). " +
                "Tus archivos de respaldo y eventos externos permanecen." + detalle,
                "Entendido");
            PanelAvanzado.IsVisible = false;
            OpcionesAvanzadasButton.Text = "Opciones avanzadas de datos  ›";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU17 - Borrar datos: {ex}");
            await DisplayAlert(
                "No se pudo completar",
                "No pudimos completar la operación. La cuenta remota no se ha modificado. " +
                "Comprueba tu sesión e inténtalo nuevamente.",
                "Entendido");
        }
        finally
        {
            IndicadorEliminacion.IsVisible = false;
            IndicadorEliminacion.IsRunning = false;
            OpcionesAvanzadasButton.IsEnabled = true;
            _ocupado = false;
            await ActualizarResumenAsync();
        }
    }
}
