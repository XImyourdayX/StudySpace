namespace Aplicacion.Interfaces;

public interface IAlmacenSesion
{
    int ObtenerUsuarioId();
    void GuardarUsuarioId(int id);
    void Limpiar();
}