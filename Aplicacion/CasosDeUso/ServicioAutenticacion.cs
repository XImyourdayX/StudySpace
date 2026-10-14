using Aplicacion.DTOs;
using Aplicacion.Mappings;
using Dominio.Entidades;
using Aplicacion.Interfaces;

namespace Aplicacion.CasosDeUso;

public class ServicioAutenticacion
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IAlmacenSesion _sesion;

    public UsuarioDTO? UsuarioActual { get; private set; }

    public ServicioAutenticacion(IRepositorioUsuarios usuarios, IAlmacenSesion sesion)
    {
        _usuarios = usuarios;
        _sesion = sesion;
    }

    public async Task<(bool Exito, string Mensaje)> Registrar(string nombre, string correo, string password, string confirmar)
    {
        nombre = nombre.Trim();
        correo = correo.Trim().ToLower();

        if (nombre == "" || correo == "" || password == "")
            return (false, "Completa todos los campos.");
        if (!correo.Contains('@'))
            return (false, "Escribe un correo válido.");
        if (password.Length < 6)
            return (false, "La contraseña debe tener al menos 6 caracteres.");
        if (password != confirmar)
            return (false, "Las contraseñas no coinciden.");
        if (await _usuarios.ObtenerPorCorreo(correo) != null)
            return (false, "Ya existe una cuenta con ese correo.");

        var usuario = new Usuario
        {
            Nombre = nombre,
            Correo = correo,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };
        await _usuarios.Agregar(usuario);

        UsuarioActual = usuario.ADto();
        _sesion.GuardarUsuarioId(usuario.Id);
        return (true, "");
    }

    public async Task<(bool Exito, string Mensaje)> IniciarSesion(string correo, string password)
    {
        correo = correo.Trim().ToLower();
        var usuario = await _usuarios.ObtenerPorCorreo(correo);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash))
            return (false, "Correo o contraseña incorrectos.");

        UsuarioActual = usuario.ADto();
        _sesion.GuardarUsuarioId(usuario.Id);
        return (true, "");
    }

    public async Task<bool> RestaurarSesion()
    {
        var id = _sesion.ObtenerUsuarioId();
        if (id == 0) return false;

        var usuario = await _usuarios.ObtenerPorId(id);
        if (usuario == null) return false;

        UsuarioActual = usuario.ADto();
        return true;
    }

    public void CerrarSesion()
    {
        UsuarioActual = null;
        _sesion.Limpiar();
    }
}
