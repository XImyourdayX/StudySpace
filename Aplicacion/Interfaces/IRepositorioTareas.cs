using Dominio.Entidades;

namespace Aplicacion.Interfaces;

public interface IRepositorioTareas
{
    Task<List<Tarea>> ObtenerPorUsuario(int usuarioId);
    Task<Tarea?> ObtenerPorId(int id);
    Task Agregar(Tarea tarea);
    Task Actualizar(Tarea tarea);
    Task Eliminar(int id);
}