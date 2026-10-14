namespace Dominio.Entidades;

public class Curso
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Nombre { get; set; } = "";
    public string Color { get; set; } = "#304BFF";
}