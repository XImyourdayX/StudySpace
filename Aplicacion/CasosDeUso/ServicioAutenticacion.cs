
using System.Text;
using Aplicacion.DTOs;
using Aplicacion.Interfaces;
using Aplicacion.Mappings;

namespace Aplicacion.CasosDeUso;

public class ServicioAutenticacion
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IAlmacenSesion _sesion;

    public UsuarioDTO? UsuarioActual { get; private set; }

    public ServicioAutenticacion(
        IRepositorioUsuarios usuarios,
        IAlmacenSesion sesion)
    {
        _usuarios = usuarios ??
            throw new ArgumentNullException(nameof(usuarios));

        _sesion = sesion ??
            throw new ArgumentNullException(nameof(sesion));
    }

    // =====================================================
    // VALIDAR DATOS ANTES DEL REGISTRO EN MYSQL
    // =====================================================
    //
    // Solo valida el formulario.
    // No consulta ni modifica usuarios de SQLite.
    // La API PHP comprueba los correos duplicados.
    // =====================================================

    public Task<(bool Exito, string Mensaje)> ValidarRegistro(
        string nombre,
        string correo,
        string password,
        string confirmar)
    {
        nombre = nombre?.Trim() ?? "";
        correo = correo?.Trim().ToLowerInvariant() ?? "";
        password ??= "";
        confirmar ??= "";

        if (string.IsNullOrWhiteSpace(nombre) ||
            string.IsNullOrWhiteSpace(correo) ||
            string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(confirmar))
        {
            return Resultado(
                false,
                "Completa todos los campos.");
        }

        if (nombre.Length < 2 || nombre.Length > 120)
        {
            return Resultado(
                false,
                "El nombre debe tener entre 2 y 120 caracteres.");
        }

        if (correo.Length > 254 ||
            !correo.Contains('@') ||
            correo.StartsWith('@') ||
            correo.EndsWith('@') ||
            correo.Any(char.IsWhiteSpace))
        {
            return Resultado(
                false,
                "Escribe un correo electrónico válido.");
        }

        int longitudBytes =
            Encoding.UTF8.GetByteCount(password);

        if (longitudBytes < 8 ||
            longitudBytes > 72)
        {
            return Resultado(
                false,
                "La contraseña debe tener entre 8 y 72 bytes.");
        }

        if (password != confirmar)
        {
            return Resultado(
                false,
                "Las contraseñas no coinciden.");
        }

        return Resultado(true, "");
    }

    // =====================================================
    // REGISTRO DIRECTO EN SQLITE DESACTIVADO
    // =====================================================
    //
    // Las cuentas reales se crean mediante
    // registro-iniciar.php y registro-verificar.php.
    // =====================================================

    public Task<(bool Exito, string Mensaje)> Registrar(
        string nombre,
        string correo,
        string password,
        string confirmar)
    {
        return Resultado(
            false,
            "El registro de cuentas se realiza " +
            "mediante el servidor de Study Space.");
    }

    // =====================================================
    // INICIO DE SESION CON SQLITE DESACTIVADO
    // =====================================================
    //
    // Las contrasenas solo se comprueban en MySQL
    // a traves de iniciar-sesion.php.
    // =====================================================

    public Task<(bool Exito, string Mensaje)> IniciarSesion(
        string correo,
        string password)
    {
        return Resultado(
            false,
            "El inicio de sesión se realiza " +
            "mediante el servidor de Study Space.");
    }

    // =====================================================
    // NUEVO: ACTIVAR PERFIL LOCAL TRAS LOGIN REMOTO
    // =====================================================
    //
    // InicioSesionVM llamara a este metodo SOLAMENTE
    // despues de:
    //
    // 1. Autenticar al usuario en la API PHP.
    // 2. Obtener su ID real desde el servidor.
    // 3. Resolver su vinculacion MySQL - SQLite.
    //
    // Este metodo NO verifica el token por si solo.
    // Esa responsabilidad corresponde al flujo
    // de autenticacion con la API PHP.
    //
    // El ID recibido aqui es el ID LOCAL de SQLite,
    // nunca el ID remoto de MySQL.
    // =====================================================

    public async Task<bool> ActivarPerfilLocalAsync(
        int usuarioLocalId)
    {
        if (usuarioLocalId <= 0)
            return false;

        var usuario =
            await _usuarios.ObtenerPorId(usuarioLocalId);

        if (usuario == null)
            return false;

        // Guardar el identificador local para que
        // los servicios de cursos y tareas puedan
        // utilizar el perfil vinculado.
        _sesion.GuardarUsuarioId(usuario.Id);

        // Mantener disponible el usuario local
        // para los servicios y ViewModels actuales.
        UsuarioActual = usuario.ADto();

        return true;
    }

    // =====================================================
    // RESTAURACION DE SESION
    // =====================================================
    //
    // Todavia no restauramos sesiones automaticamente.
    //
    // Una sesion local de SQLite no es prueba de
    // que el token remoto siga siendo valido.
    //
    // Mas adelante comprobaremos el token guardado
    // en SecureStorage mediante sesion-validar.php.
    // =====================================================

    public Task<bool> RestaurarSesion()
    {
        UsuarioActual = null;

        _sesion.Limpiar();

        return Task.FromResult(false);
    }

    // =====================================================
    // LIMPIAR ESTADO DE SESION LOCAL
    // =====================================================
    //
    // Este metodo NO revoca el token remoto.
    // El cierre de sesion completo utilizara
    // sesion-cerrar.php y SecureStorage.
    // =====================================================

    public void CerrarSesion()
    {
        UsuarioActual = null;

        _sesion.Limpiar();
    }

    // =====================================================
    // RESULTADO ASINCRONO
    // =====================================================

    private static Task<(bool Exito, string Mensaje)> Resultado(
        bool exito,
        string mensaje)
    {
        return Task.FromResult((
            Exito: exito,
            Mensaje: mensaje
        ));
    }
}
