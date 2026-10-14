using Dominio.Entidades;

namespace Aplicacion.Interfaces;

public interface IRepositorioUsuarios
{
    Task<Usuario?> ObtenerPorCorreo(string correo);
    Task<Usuario?> ObtenerPorId(int id);
    Task Agregar(Usuario usuario);
}