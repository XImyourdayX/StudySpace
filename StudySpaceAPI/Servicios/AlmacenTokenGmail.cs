
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace StudySpaceAPI.Servicios;

public class AlmacenTokenGmail
{
    private readonly IDataProtector _protector;
    private readonly string _rutaArchivo;

    public AlmacenTokenGmail(
        IDataProtectionProvider proveedor)
    {
        _protector = proveedor.CreateProtector(
            "StudySpaceAPI.Gmail.RefreshToken.v1");

        string carpeta = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "StudySpaceAPI");

        Directory.CreateDirectory(carpeta);

        _rutaArchivo = Path.Combine(
            carpeta,
            "gmail-token.dat");
    }

    // GUARDAR TOKEN PROTEGIDO
    public async Task GuardarAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException(
                "El token no puede estar vacío.");

        string protegido =
            _protector.Protect(refreshToken);

        await File.WriteAllTextAsync(
            _rutaArchivo,
            protegido,
            Encoding.UTF8);
    }

    // LEER TOKEN Y DESPROTEGERLO
    public async Task<string?> ObtenerAsync()
    {
        if (!File.Exists(_rutaArchivo))
            return null;

        string protegido =
            await File.ReadAllTextAsync(
                _rutaArchivo,
                Encoding.UTF8);

        return _protector.Unprotect(protegido);
    }

    // ELIMINAR AUTORIZACIÓN GUARDADA
    public void Eliminar()
    {
        if (File.Exists(_rutaArchivo))
            File.Delete(_rutaArchivo);
    }
}
