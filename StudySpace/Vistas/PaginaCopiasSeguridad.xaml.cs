using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using StudySpace.Servicios;

namespace StudySpace.Vistas;

/// <summary>
/// HU16, paso 2. Interfaz para exportar y compartir una copia cifrada.
/// No almacena contraseñas de respaldo en Preferences o SecureStorage.
/// La restauración se implementará en otro paso.
/// </summary>
public partial class PaginaCopiasSeguridad : ContentPage
{
    private readonly ServicioRespaldoCifrado _respaldo;
    private readonly ServicioRestauracionCifrada _restauracion;
    private Guid? _vistaPrevia;
    private bool _ocupado;

    public PaginaCopiasSeguridad(
        ServicioRespaldoCifrado respaldo,
        ServicioRestauracionCifrada restauracion)
    {
        InitializeComponent();
        _respaldo = respaldo;
        _restauracion = restauracion;
    }

    private void OnContrasenaCompleted(object? sender, EventArgs e)
    {
        ConfirmacionEntry.Focus();
    }

    private void OnMostrarContrasenaClicked(object? sender, EventArgs e)
    {
        ContrasenaEntry.IsPassword = !ContrasenaEntry.IsPassword;
        MostrarContrasenaButton.Text = ContrasenaEntry.IsPassword ? "Mostrar" : "Ocultar";
    }

    private void OnMostrarConfirmacionClicked(object? sender, EventArgs e)
    {
        ConfirmacionEntry.IsPassword = !ConfirmacionEntry.IsPassword;
        MostrarConfirmacionButton.Text = ConfirmacionEntry.IsPassword ? "Mostrar" : "Ocultar";
    }

    // Los campos no deben permanecer visibles después de compartir el respaldo.
    private void OcultarCamposContrasena()
    {
        ContrasenaEntry.IsPassword = true;
        ConfirmacionEntry.IsPassword = true;
        MostrarContrasenaButton.Text = "Mostrar";
        MostrarConfirmacionButton.Text = "Mostrar";
    }

    private async void OnVolverClicked(object? sender, EventArgs e)
    {
        if (_ocupado)
            return;

        _restauracion.DescartarVistaPrevia();
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCrearCopiaClicked(object? sender, EventArgs e)
    {
        if (_ocupado)
            return;

        string contrasena = ContrasenaEntry.Text ?? string.Empty;
        string confirmacion = ConfirmacionEntry.Text ?? string.Empty;

        if (contrasena.Length < 12 || contrasena.Length > 256)
        {
            MostrarMensaje("La contraseña de respaldo debe tener entre 12 y 256 caracteres.");
            return;
        }

        if (!string.Equals(contrasena, confirmacion, StringComparison.Ordinal))
        {
            MostrarMensaje("Las contraseñas no coinciden. Revisa ambos campos.");
            return;
        }

        _ocupado = true;
        CrearCopiaButton.IsEnabled = false;
        IndicadorActividad.IsVisible = true;
        IndicadorActividad.IsRunning = true;
        MensajeLabel.IsVisible = false;

        try
        {
            // El servicio cifra los datos antes de generar el archivo .ssbackup.
            var resultado = await _respaldo.CrearRespaldoAsync(contrasena);

            // No conservar la frase en los controles mientras se comparte.
            ContrasenaEntry.Text = string.Empty;
            ConfirmacionEntry.Text = string.Empty;
            OcultarCamposContrasena();
            ContrasenaEntry.Unfocus();
            ConfirmacionEntry.Unfocus();

            await DisplayAlert(
                "Copia cifrada preparada",
                $"Se incluyeron {resultado.CantidadCursos} cursos y " +
                $"{resultado.CantidadTareas} tareas.\n\n" +
                "Ahora elige un destino externo para guardar el archivo. " +
                "El archivo temporal de Study Space no es una copia permanente.",
                "Elegir destino");

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Guardar copia cifrada de Study Space",
                File = new ShareFile(resultado.RutaTemporal)
            });

            MostrarMensaje(
                "Se abrió el menú para compartir. Comprueba en el destino elegido " +
                "que el archivo .ssbackup se haya guardado: compartir no confirma que se haya almacenado.",
                esError: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU16 - Exportar copia: {ex.GetType().Name}");
            // No incluir excepciones internas, rutas ni datos de la cuenta en la interfaz.
            MostrarMensaje(
                "No se pudo crear o compartir la copia. Comprueba el espacio disponible " +
                "y vuelve a intentarlo.");
        }
        finally
        {
            // Limpiar el control también en errores. No registrar contraseñas.
            ContrasenaEntry.Text = string.Empty;
            ConfirmacionEntry.Text = string.Empty;
            OcultarCamposContrasena();
            contrasena = string.Empty;
            confirmacion = string.Empty;
            IndicadorActividad.IsRunning = false;
            IndicadorActividad.IsVisible = false;
            CrearCopiaButton.IsEnabled = true;
            _ocupado = false;
        }
    }


    private void OnMostrarClaveRestauracionClicked(object? sender, EventArgs e)
    {
        ClaveRestauracionEntry.IsPassword = !ClaveRestauracionEntry.IsPassword;
        MostrarClaveRestauracionButton.Text =
            ClaveRestauracionEntry.IsPassword ? "Mostrar" : "Ocultar";
    }

    private void OcultarClaveRestauracion()
    {
        ClaveRestauracionEntry.Text = string.Empty;
        ClaveRestauracionEntry.IsPassword = true;
        MostrarClaveRestauracionButton.Text = "Mostrar";
        ClaveRestauracionEntry.Unfocus();
    }

    private void MostrarResultadoRestauracion(string texto, bool error)
    {
        MensajeRestauracionLabel.Text = texto;
        MensajeRestauracionLabel.TextColor = error ? Colors.Red : Colors.Green;
        MensajeRestauracionLabel.IsVisible = true;
    }

    private void IniciarOperacion()
    {
        _ocupado = true;
        CrearCopiaButton.IsEnabled = false;
        SeleccionarCopiaButton.IsEnabled = false;
        ConfirmarRestauracionButton.IsEnabled = false;
        IndicadorActividad.IsVisible = true;
        IndicadorActividad.IsRunning = true;
    }

    private void FinalizarOperacion()
    {
        IndicadorActividad.IsRunning = false;
        IndicadorActividad.IsVisible = false;
        CrearCopiaButton.IsEnabled = true;
        SeleccionarCopiaButton.IsEnabled = true;
        ConfirmarRestauracionButton.IsEnabled = true;
        _ocupado = false;
    }

    private async void OnSeleccionarCopiaClicked(object? sender, EventArgs e)
    {
        if (_ocupado) return;

        string clave = ClaveRestauracionEntry.Text ?? string.Empty;
        if (clave.Length < 12 || clave.Length > 256)
        {
            MostrarResultadoRestauracion(
                "Introduce la contraseña de la copia (12 a 256 caracteres).", true);
            return;
        }

        // Descartar una vista previa anterior para evitar importar otro archivo.
        _restauracion.DescartarVistaPrevia();
        _vistaPrevia = null;
        _restauracionTienePendientes = false;
        ConfirmarRestauracionButton.Text = "Restaurar datos faltantes";
        ConfirmarRestauracionButton.IsEnabled = false;
        VistaPreviaBorder.IsVisible = false;
        MensajeRestauracionLabel.IsVisible = false;

        IniciarOperacion();
        try
        {
            var seleccionado = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona una copia cifrada de Study Space"
            });
            if (seleccionado is null)
                return;
            if (!seleccionado.FileName.EndsWith(
                ".ssbackup", StringComparison.OrdinalIgnoreCase))
            {
                MostrarResultadoRestauracion(
                    "Selecciona un archivo con extensión .ssbackup.", true);
                return;
            }

            await using var archivo = await seleccionado.OpenReadAsync();
            var previa = await _restauracion.PrepararAsync(archivo, clave);
            _vistaPrevia = previa.Id;
            _restauracionTienePendientes =
                previa.CursosPorAgregar > 0 || previa.TareasPorAgregar > 0;
            ResumenVistaPreviaLabel.Text =
                $"Creado: {previa.CreadoUtc.ToLocalTime():dd/MM/yyyy HH:mm}\n" +
                $"En la copia: {previa.CursosEnArchivo} cursos y " +
                $"{previa.TareasEnArchivo} tareas.\n" +
                $"Por agregar: {previa.CursosPorAgregar} cursos y " +
                $"{previa.TareasPorAgregar} tareas.\n" +
                "Las coincidencias permanecerán sin cambios." +
                (_restauracionTienePendientes
                    ? "\nPuedes importar los elementos que faltan."
                    : "\nNo hay datos nuevos para restaurar en este teléfono.");

            VistaPreviaBorder.IsVisible = true;
            // Si no hay novedades, el botón explica el resultado en lugar
            // de quedar deshabilitado sin información para el estudiante.
            ConfirmarRestauracionButton.Text = _restauracionTienePendientes
                ? "Restaurar datos faltantes" : "Comprobar restauración";
            ConfirmarRestauracionButton.IsEnabled = true;
            MostrarResultadoRestauracion(
                _restauracionTienePendientes
                    ? "Copia verificada. Revisa la vista previa antes de confirmar."
                    : "Copia verificada. Tus cursos y tareas ya están en este teléfono; no se requiere importar nada.",
                false);
        }
        catch (InvalidDataException ex)
        {
            MostrarResultadoRestauracion(ex.Message, true);
        }
        catch (InvalidOperationException ex)
        {
            MostrarResultadoRestauracion(ex.Message, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"HU16 - Lectura de copia: {ex.GetType().Name}");
            MostrarResultadoRestauracion(
                "No se pudo leer el archivo. Revisa Drive, el formato y los permisos.", true);
        }
        finally
        {
            // No conservar la contraseña de restauración en los controles.
            clave = string.Empty;
            OcultarClaveRestauracion();
            FinalizarOperacion();
            ConfirmarRestauracionButton.IsEnabled = _vistaPrevia is not null;
        }
    }

    private bool _restauracionTienePendientes;

    private async void OnConfirmarRestauracionClicked(object? sender, EventArgs e)
    {
        if (_ocupado || _vistaPrevia is null)
            return;

        // La importación es aditiva: un respaldo con los mismos datos no
        // debe ejecutar operaciones sobre SQLite ni duplicar registros.
        if (!_restauracionTienePendientes)
        {
            await DisplayAlert(
                "No hay datos nuevos",
                "La copia se verificó correctamente, pero sus cursos y tareas ya existen en este teléfono. " +
                "No se ha modificado ninguna información.\n\n" +
                "La restauración sirve para recuperar elementos faltantes; no reemplaza ni revierte los actuales.",
                "Entendido");
            return;
        }

        bool acepta = await DisplayAlert(
            "Confirmar restauración",
            "¿Quieres añadir los cursos y tareas faltantes de esta copia?\n\n" +
            "No se borrará ni reemplazará información existente. " +
            "La operación puede tardar unos segundos.",
            "Restaurar", "Cancelar");
        if (!acepta) return;

        IniciarOperacion();
        try
        {
            var resultado = await _restauracion.RestaurarAsync(_vistaPrevia.Value);
            VistaPreviaBorder.IsVisible = false;
            _vistaPrevia = null;
            _restauracionTienePendientes = false;
            MostrarResultadoRestauracion(
                $"Restauración completada: {resultado.CursosAgregados} cursos y " +
                $"{resultado.TareasAgregadas} tareas agregadas. " +
                $"Recordatorios programados: {resultado.RecordatoriosProgramados}. " +
                "Puedes volver a Inicio para consultar los cambios.", false);
            await DisplayAlert("Restauración completada",
                "Tus cursos y tareas existentes se conservaron. " +
                "Las actividades nuevas aparecerán en Inicio, Cursos y Reportes.", "Aceptar");
        }
        catch (InvalidDataException ex)
        {
            MostrarResultadoRestauracion(ex.Message, true);
        }
        catch (InvalidOperationException ex)
        {
            MostrarResultadoRestauracion(ex.Message, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"HU16 - Restauración: {ex.GetType().Name}");
            MostrarResultadoRestauracion(
                "No se pudo completar la restauración. No se aplicaron cambios parciales.", true);
        }
        finally
        {
            FinalizarOperacion();
        }
    }

    private void MostrarMensaje(string texto, bool esError = true)
    {
        MensajeLabel.Text = texto;
        MensajeLabel.TextColor = esError
            ? Colors.Red
            : Colors.Green;
        MensajeLabel.IsVisible = true;
    }
}
