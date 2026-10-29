
using Dominio.Entidades;

namespace Aplicacion.Interfaces;

public interface IRepositorioUsuarios
{
    // BUSCAR USUARIO POR CORREO
    Task<Usuario?> ObtenerPorCorreo(string correo);

    // BUSCAR USUARIO POR ID
    Task<Usuario?> ObtenerPorId(int id);

    // REGISTRAR NUEVO USUARIO
    Task Agregar(Usuario usuario);

    // ACTUALIZAR USUARIO EXISTENTE
    // NECESARIO PARA RECUPERAR CONTRASEÑA
    Task Actualizar(Usuario usuario);
}
