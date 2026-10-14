using Aplicacion.DTOs;
using Dominio.Entidades;

namespace Aplicacion.Mappings;

public static class Mapeos
{
    public static UsuarioDTO ADto(this Usuario u) =>
        new() { Id = u.Id, Nombre = u.Nombre, Correo = u.Correo };

    public static CursoDTO ADto(this Curso c) =>
        new() { Id = c.Id, Nombre = c.Nombre, Color = c.Color };

    public static TareaDTO ADto(this Tarea t, string cursoNombre = "", string cursoColor = "#304BFF") => new()
    {
        Id = t.Id,
        CursoId = t.CursoId,
        CursoNombre = cursoNombre,
        CursoColor = cursoColor,
        Titulo = t.Titulo,
        Descripcion = t.Descripcion,
        FechaLimite = t.FechaLimite,
        Prioridad = t.Prioridad,
        Completada = t.Completada,
        MinutosAviso = t.MinutosAviso
    };
}