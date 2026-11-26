using Microsoft.Maui.Controls.Shapes;

namespace StudySpace.Vistas;

/// <summary>
/// HU17: presenta borradores de política y términos SIN aceptar términos
/// automáticamente y SIN realizar operaciones de red o acceso a SQLite.
/// Estos textos se deben revisar antes de publicarse o utilizarse para consentimiento.
/// </summary>
public partial class PaginaDocumentoLegal : ContentPage, IQueryAttributable
{
    public PaginaDocumentoLegal()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        string tipo = query.TryGetValue("tipo", out var valor)
            ? Uri.UnescapeDataString(valor?.ToString() ?? string.Empty)
            : string.Empty;

        bool esPrivacidad = !string.Equals(tipo, "terminos", StringComparison.OrdinalIgnoreCase);
        TituloDocumento.Text = esPrivacidad ? "Política de privacidad" : "Términos y condiciones";
        SeccionesDocumento.Children.Clear();

        var secciones = esPrivacidad ? SeccionesPrivacidad : SeccionesTerminos;
        bool oscuro = Application.Current?.RequestedTheme == AppTheme.Dark;
        Color fondo = Color.FromArgb(oscuro ? "#222139" : "#FFFFFF");
        Color texto = Color.FromArgb(oscuro ? "#F4F2FA" : "#202033");
        Color secundario = Color.FromArgb(oscuro ? "#CBC7DA" : "#57546B");

        foreach (var seccion in secciones)
        {
            var tarjeta = new Border
            {
                StrokeThickness = 0,
                Padding = new Thickness(15),
                BackgroundColor = fondo,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
                Content = new VerticalStackLayout
                {
                    Spacing = 7,
                    Children =
                    {
                        new Label
                        {
                            Text = seccion.Titulo,
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = texto,
                            LineBreakMode = LineBreakMode.WordWrap
                        },
                        new Label
                        {
                            Text = seccion.Contenido,
                            FontSize = 13,
                            LineHeight = 1.25,
                            TextColor = secundario,
                            LineBreakMode = LineBreakMode.WordWrap
                        }
                    }
                }
            };
            SeccionesDocumento.Children.Add(tarjeta);
        }
    }

    private async void OnVolverClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private sealed record Seccion(string Titulo, string Contenido);

    // Basado en la implementación inspeccionada de Study Space, HU01–HU17.
    // No asumir datos de contacto, operador legal ni plazos que aún no están definidos.
    private static readonly Seccion[] SeccionesPrivacidad =
    {
        new("1. ¿Qué es Study Space?",
            "Study Space es una aplicación de organización de cursos, tareas, fechas de entrega y recordatorios para estudiantes. Esta versión se encuentra en etapa de desarrollo y pruebas académicas."),
        new("2. Responsable y contacto — por completar",
            "El correo de atención técnica es studyspace.soporte@wopres.com. Antes de ofrecer la aplicación a participantes externos, el equipo debe identificar formalmente al responsable del tratamiento de datos, definir el procedimiento de atención de derechos y revisar esta política. Este documento sigue siendo un borrador."),
        new("3. Información de la cuenta",
            "Para registrar y autenticar una cuenta se utilizan datos como nombre, correo electrónico y credenciales de acceso. El inicio de sesión, la verificación y la recuperación se gestionan mediante una API PHP con almacenamiento remoto. La aplicación guarda el token de sesión en el almacenamiento seguro del dispositivo."),
        new("4. Cursos, tareas y progreso",
            "Los cursos, tareas, prioridades, fechas de entrega, estados y fechas de finalización se almacenan principalmente en una base SQLite de este teléfono, vinculados al perfil autenticado. Actualmente no se sincronizan automáticamente entre teléfonos."),
        new("5. Recordatorios y calendario externo",
            "Study Space utiliza notificaciones para avisarte sobre tareas, sujeto a los permisos y ajustes del teléfono. Si activas la integración con calendario en Perfil, la aplicación puede crear y actualizar eventos vinculados en el calendario Android. Su eventual sincronización con Google depende del calendario y de la configuración del dispositivo."),
        new("6. Copias de seguridad",
            "Puedes crear un archivo .ssbackup protegido con una contraseña que tú eliges y compartirlo con el destino que selecciones, como Drive u otra aplicación. Study Space no solicita ni almacena la contraseña de ese archivo para recuperarla después. Al compartirlo, el servicio de destino aplica sus propias condiciones y políticas."),
        new("7. Uso y conservación de los datos",
            "Los datos académicos locales permanecen en el dispositivo hasta que los modifiques, los elimines o se borren los datos de la aplicación. La información de la cuenta remota tiene un ciclo de vida separado. Antes de publicar la política definitiva, se deben precisar los plazos de conservación de datos remotos y registros técnicos, así como los proveedores involucrados."),
        new("8. Opciones de control",
            "Desde la aplicación puedes consultar los datos locales, hacer respaldos y borrar cursos y tareas de este teléfono. En Opciones avanzadas también puedes solicitar la eliminación de tu cuenta MySQL, con contraseña y confirmaciones: una vez que el servidor la confirme, la app borra el perfil y los datos locales de esa cuenta en este teléfono. Los archivos externos y datos de otros dispositivos no se borran automáticamente. Los respaldos actuales solo se pueden restaurar para su cuenta original, por lo que no podrán importarse en otra cuenta tras eliminarla."),
        new("9. Protección y limitaciones",
            "Se aplican mecanismos técnicos como el uso de HTTPS para la API, almacenamiento seguro del token y cifrado AES-256-GCM para los respaldos. Ninguna medida elimina totalmente los riesgos: protege tu dispositivo y conserva la contraseña de respaldo de forma segura. Los mecanismos deberán pasar pruebas de seguridad antes de un lanzamiento público."),
        new("10. Solicitudes y cambios de esta política",
            "Antes del piloto se deberá habilitar un canal para ejercer los derechos aplicables sobre los datos personales y responder solicitudes relativas a la cuenta remota. También se publicará una versión fechada de la política y se informarán sus cambios relevantes. Este texto no sustituye esa versión final.")
    };

    private static readonly Seccion[] SeccionesTerminos =
    {
        new("1. Objeto del servicio",
            "Study Space es una herramienta de apoyo para organizar asignaturas, tareas, vencimientos, prioridades, reportes y recordatorios. No es una plataforma oficial de ninguna institución educativa ni presenta entregas académicas en nombre del estudiante."),
        new("2. Acceso y responsabilidad de la cuenta",
            "Para usar las funciones asociadas a una cuenta es necesario registrarse y autenticarse. Debes proporcionar datos correctos, mantener tus credenciales protegidas y evitar compartir tu sesión con otras personas."),
        new("3. Información académica",
            "Cada estudiante es responsable de registrar y comprobar sus cursos, tareas y fechas límite. Study Space no modifica los plazos oficiales establecidos por docentes o instituciones. Posponer un recordatorio no amplía la fecha real de entrega."),
        new("4. Recordatorios y calendario",
            "Los avisos dependen de los permisos, ajustes de batería y condiciones de Android. Los calendarios externos funcionan solo si la integración está habilitada y el dispositivo permite las operaciones. Es recomendable comprobar las fechas directamente en el aula virtual o la fuente oficial."),
        new("5. Respaldo y recuperación",
            "La exportación cifrada requiere que guardes el archivo en un lugar seguro y recuerdes su contraseña. La restauración existente agrega datos faltantes, sin sustituir automáticamente registros ya presentes. La pérdida del archivo o de la contraseña puede impedir su recuperación."),
        new("6. Disponibilidad durante el piloto",
            "La versión actual está en evaluación. Puede presentar errores o interrupciones y recibir actualizaciones. Durante las pruebas se informará a los participantes de los límites del servicio, de los canales de ayuda y de las medidas para proteger sus datos."),
        new("7. Precio y funcionalidades",
            "En la versión actual las funcionalidades habilitadas se ofrecen sin suscripción Premium. Los planes, cobros y funciones de pago forman parte de una propuesta futura y no están disponibles ni contratados en esta etapa. Cualquier cambio comercial deberá comunicarse antes de ofrecer pagos."),
        new("8. Uso adecuado",
            "El usuario debe utilizar la aplicación de manera lícita, respetar la privacidad de otras personas y no intentar acceder a cuentas o información ajena. La aplicación no debe usarse para actividades que vulneren sistemas de terceros."),
        new("9. Gestión de cuenta",
            "La app permite eliminar cursos y tareas locales sin cerrar la cuenta. También ofrece eliminación de la cuenta MySQL con contraseña y confirmaciones; al confirmarse en el servidor, se limpia el perfil de esa cuenta en este teléfono. Esto no elimina respaldos de Drive, eventos de calendarios externos ni los datos conservados en otros teléfonos. El procedimiento debe validarse con una cuenta de prueba antes de habilitarse para participantes externos."),
        new("10. Titularidad, contacto y aceptación — pendiente",
            "Antes de utilizar estos términos como contrato deberán identificarse el titular del servicio, el contacto de atención, las reglas finales aplicables y un mecanismo claro de aceptación cuando sea necesario. Este borrador no registra consentimiento ni crea por sí mismo una relación contractual definitiva.")
    };
}
