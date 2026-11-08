using SQLite;

namespace Infraestructura.DBContext;

[Table("VinculoUsuarioRemoto")]
public class VinculoUsuarioRemoto
{
    // IDENTIFICADOR DE LA VINCULACION EN SQLITE
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // ID DE LA CUENTA REAL EN MYSQL
    // SE OBTENDRA DE LA API PHP AUTENTICADA.
    [Indexed(Unique = true)]
    public long UsuarioRemotoId { get; set; }

    // ID DEL PERFIL LOCAL EN SQLITE
    // LOS CURSOS Y TAREAS UTILIZAN ESTE ID.
    [Indexed(Unique = true)]
    public int UsuarioLocalId { get; set; }

    // FECHA DE CREACION DE LA VINCULACION
    public DateTime FechaVinculacionUtc { get; set; }
        = DateTime.UtcNow;
}
