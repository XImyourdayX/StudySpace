
namespace Dominio.Entidades;

public class Usuario
{
    public int Id { get; set; }

    public string Nombre { get; set; } = "";

    public string Correo { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    // INDICA SI EL CORREO FUE VERIFICADO
    public bool CorreoVerificado { get; set; } = false;
}
