using Aplicacion.Interfaces;

namespace StudySpace.Servicios;

public class AlmacenSesionPreferences : IAlmacenSesion
{
    private static string Ruta => Path.Combine(FileSystem.AppDataDirectory, "sesion.txt");

    public int ObtenerUsuarioId()
    {
        try
        {
            return File.Exists(Ruta) && int.TryParse(File.ReadAllText(Ruta), out var id) ? id : 0;
        }
        catch { return 0; }
    }

    public void GuardarUsuarioId(int id)
    {
        try { File.WriteAllText(Ruta, id.ToString()); } catch { }
    }

    public void Limpiar()
    {
        try { if (File.Exists(Ruta)) File.Delete(Ruta); } catch { }
    }
}