using Aplicacion.DTOs;
using Aplicacion.Interfaces;
using Aplicacion.Mappings;
using Dominio.Entidades;

namespace Aplicacion.CasosDeUso;

public class ServicioTareas
{
    private readonly IRepositorioTareas _tareas;
    private readonly IRepositorioCursos _cursos;
    private readonly ServicioAutenticacion _auth;

    public ServicioTareas(IRepositorioTareas tareas, IRepositorioCursos cursos, ServicioAutenticacion auth)
    {
        _tareas = tareas;
        _cursos = cursos;
        _auth = auth;
    }

    public async Task<List<TareaDTO>> ObtenerTareas()
    {
        if (_auth.UsuarioActual == null) return new();

        var usuarioId = _auth.UsuarioActual.Id;
        var cursos = (await _cursos.ObtenerPorUsuario(usuarioId)).ToDictionary(c => c.Id);
        var lista = await _tareas.ObtenerPorUsuario(usuarioId);

        return lista.Select(t =>
        {
            cursos.TryGetValue(t.CursoId, out var c);
            return t.ADto(c?.Nombre ?? "", c?.Color ?? "#304BFF");
        }).ToList();
    }

    public async Task<(bool Exito, string Mensaje)> AgregarTarea(
        string titulo, string descripcion, int cursoId,
        DateTime fechaLimite, string prioridad, int minutosAviso)
    {
        titulo = titulo.Trim();
        if (_auth.UsuarioActual == null) return (false, "No hay sesión iniciada.");
        if (titulo == "") return (false, "Escribe el título de la tarea.");
        if (cursoId == 0) return (false, "Elige un curso.");
        if (fechaLimite < DateTime.Now) return (false, "La fecha límite no puede ser pasada.");

        await _tareas.Agregar(new Tarea
        {
            UsuarioId = _auth.UsuarioActual.Id,
            CursoId = cursoId,
            Titulo = titulo,
            Descripcion = descripcion.Trim(),
            FechaLimite = fechaLimite,
            Prioridad = prioridad,
            MinutosAviso = minutosAviso
        });
        return (true, "");
    }
    public async Task AlternarCompletada(int id)
    {
        if (_auth.UsuarioActual == null) return;

        var tarea = await _tareas.ObtenerPorId(id);
        if (tarea == null || tarea.UsuarioId != _auth.UsuarioActual.Id) return;

        tarea.Completada = !tarea.Completada;
        await _tareas.Actualizar(tarea);
    }
}