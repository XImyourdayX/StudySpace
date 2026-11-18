using Dominio.Entidades;

namespace Aplicacion.Interfaces;

public interface IRepositorioCursos
{
    Task<List<Curso>> ObtenerPorUsuario(int usuarioId);
    Task<Curso?> ObtenerPorNombre(int usuarioId, string nombre);
    Task<int> ContarPorUsuario(int usuarioId);
    Task Agregar(Curso curso);

    // Elimina solo si pertenece al usuario y no existen tareas asociadas.
    Task<bool> EliminarSiNoTieneTareas(int usuarioId, int cursoId);
}