using Aplicacion.CasosDeUso;
using Microsoft.Maui.Storage;
using StudySpace.Servicios;

namespace StudySpace.Vistas;

/// <summary>
/// HU17: resumen de datos, enlace a respaldos e información de privacidad.
/// Dos acciones separadas: borrar datos SQLite y eliminar la cuenta remota.
/// La eliminación de cuenta requiere contraseña, dos confirmaciones y
/// una respuesta afirmativa del servidor antes de limpiar SQLite.
/// </summary>
public partial class PaginaPrivacidadDatos : ContentPage
{
    private const string ClaveToken = "studyspace_api_php_token";
    private readonly ServicioGestionDatosLocales _gestion;
    private readonly ServicioEliminacionCuentaRemota _cuentaRemota;
    private readonly ServicioAutenticacion _auth;
    private bool _ocupado;

    public PaginaPrivacidadDatos(
        ServicioGestionDatosLocales gestion,
        ServicioEliminacionCuentaRemota cuentaRemota,
        ServicioAutenticacion auth)
    {
        InitializeComponent();
        _gestion = gestion;
        _cuentaRemota = cuentaRemota;
        _auth = auth;
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
        ContrasenaEliminarCuentaEntry.Text = string.Empty;
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
        if (!PanelAvanzado.IsVisible)
        {
            PanelEliminarCuenta.IsVisible = false;
            ContrasenaEliminarCuentaEntry.Text = string.Empty;
        }
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
            "Eliminar datos académicos",
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
        MostrarEliminarCuentaButton.IsEnabled = false;
        IndicadorEliminacion.IsVisible = true;
        IndicadorEliminacion.IsRunning = true;

        try
        {
            var resultado = await _gestion.EliminarDatosAcademicosAsync();
            string detalle = resultado.AvisosConError > 0
                ? $" No se pudieron cancelar {resultado.AvisosConError} aviso(s) antiguos; revísalos en Android."
                : string.Empty;

            await DisplayAlert(
                "Datos académicos eliminados",
                $"Se eliminaron {resultado.CursosEliminados} curso(s) y " +
                $"{resultado.TareasEliminadas} tarea(s). " +
                "Tus archivos de respaldo y eventos externos permanecen." + detalle,
                "Entendido");
            PanelAvanzado.IsVisible = false;
            PanelEliminarCuenta.IsVisible = false;
            ContrasenaEliminarCuentaEntry.Text = string.Empty;
            MostrarEliminarCuentaButton.Text = "Eliminar mi cuenta  ›";
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
            MostrarEliminarCuentaButton.IsEnabled = true;
            _ocupado = false;
            await ActualizarResumenAsync();
        }
    }

    private void OnMostrarEliminarCuentaClicked(object? sender, EventArgs e)
    {
        if (_ocupado) return;
        PanelEliminarCuenta.IsVisible = !PanelEliminarCuenta.IsVisible;
        MostrarEliminarCuentaButton.Text = PanelEliminarCuenta.IsVisible
            ? "Ocultar eliminación de cuenta  ‹"
            : "Eliminar mi cuenta  ›";
        if (!PanelEliminarCuenta.IsVisible)
            ContrasenaEliminarCuentaEntry.Text = string.Empty;
    }

    private async void OnConfirmarEliminarCuentaClicked(object? sender, EventArgs e)
    {
        if (_ocupado) return;
        int usuarioLocalId = _auth.UsuarioActual?.Id ?? 0;
        if (usuarioLocalId <= 0)
        {
            await DisplayAlert("Sesión necesaria", "Debes iniciar sesión para eliminar tu cuenta.", "Entendido");
            return;
        }

        string contrasena = ContrasenaEliminarCuentaEntry.Text ?? string.Empty;
        if (string.IsNullOrEmpty(contrasena))
        {
            await DisplayAlert("Falta contraseña", "Escribe tu contraseña actual para continuar.", "Entendido");
            return;
        }

        bool primera = await DisplayAlert(
            "Eliminar mi cuenta",
            "Se borrará tu cuenta del servidor MySQL y se cerrarán sus sesiones. " +
            "Después se eliminarán de este teléfono los cursos, tareas y el perfil local " +
            "de la cuenta. ¿Deseas revisar la confirmación final?",
            "Revisar", "Cancelar");
        if (!primera)
        {
            ContrasenaEliminarCuentaEntry.Text = string.Empty;
            return;
        }

        bool definitiva = await DisplayAlert(
            "Confirmación definitiva",
            "La acción es irreversible. Los respaldos .ssbackup que ya tengas " +
            "seguirán en Drive, pero NO podrán restaurarse en una nueva cuenta. " +
            "Los eventos del calendario externo permanecerán. " +
            "¿Quieres ELIMINAR DEFINITIVAMENTE tu cuenta de Study Space?",
            "Sí, eliminar mi cuenta", "Conservar mi cuenta");
        if (!definitiva)
        {
            ContrasenaEliminarCuentaEntry.Text = string.Empty;
            return;
        }

        _ocupado = true;
        BorrarDatosButton.IsEnabled = false;
        MostrarEliminarCuentaButton.IsEnabled = false;
        ConfirmarEliminarCuentaButton.IsEnabled = false;
        ContrasenaEliminarCuentaEntry.IsEnabled = false;
        OpcionesAvanzadasButton.IsEnabled = false;
        IndicadorEliminacion.IsVisible = true;
        IndicadorEliminacion.IsRunning = true;

        bool servidorConfirmo = false;
        try
        {
            var resultado = await _cuentaRemota.SolicitarAsync(usuarioLocalId, contrasena);
            if (!resultado.Confirmada)
            {
                await DisplayAlert("Eliminación no confirmada", resultado.Mensaje, "Entendido");
                return;
            }

            servidorConfirmo = true;
            bool limpiezaCompletada = false;
            try
            {
                // Registrar antes de borrar: si Android cierra la aplicación,
                // se reintentará al validar una sesión en el próximo inicio.
                _gestion.MarcarLimpiezaPendiente(usuarioLocalId);
                await _gestion.CompletarLimpiezaPendienteAsync();
                limpiezaCompletada = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HU17 - Limpieza tras cuenta: {ex.GetType().Name}");
            }

            // El servidor ya eliminó la cuenta: no conservar su sesión local.
            SecureStorage.Default.Remove(ClaveToken);
            _auth.CerrarSesion();

            await DisplayAlert(
                "Cuenta eliminada",
                limpiezaCompletada
                    ? "Se eliminó tu cuenta del servidor y sus datos locales de este teléfono. " +
                      "Los archivos externos y eventos de calendario permanecen; los respaldos no se pueden importar en otra cuenta."
                    : "La cuenta se eliminó del servidor, pero la limpieza local no pudo completarse. " +
                      "No uses este dispositivo para otra cuenta hasta comprobar el borrado local; " +
                      "si el problema persiste, elimina los datos de la aplicación desde Android.",
                "Entendido");
            await Shell.Current.GoToAsync("//bienvenida");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU17 - Eliminar cuenta: {ex.GetType().Name}");
            if (servidorConfirmo)
            {
                // Una respuesta definitiva del servidor es irreversible;
                // jamás volver a mostrar la cuenta como si aún existiera.
                SecureStorage.Default.Remove(ClaveToken);
                _auth.CerrarSesion();
                await DisplayAlert("Cuenta eliminada en servidor",
                    "La cuenta remota fue eliminada. Comprueba si la limpieza local quedó pendiente " +
                    "y vuelve a abrir Study Space para reintentar la limpieza.", "Entendido");
                await Shell.Current.GoToAsync("//bienvenida");
            }
            else
            {
                await DisplayAlert("No se completó",
                    "No se ha confirmado ninguna eliminación. No se modificaron datos locales.",
                    "Entendido");
            }
        }
        finally
        {
            contrasena = string.Empty;
            ContrasenaEliminarCuentaEntry.Text = string.Empty;
            IndicadorEliminacion.IsRunning = false;
            IndicadorEliminacion.IsVisible = false;
            ConfirmarEliminarCuentaButton.IsEnabled = true;
            ContrasenaEliminarCuentaEntry.IsEnabled = true;
            MostrarEliminarCuentaButton.IsEnabled = true;
            OpcionesAvanzadasButton.IsEnabled = true;
            _ocupado = false;
            if (!servidorConfirmo)
                await ActualizarResumenAsync();
        }
    }
}
