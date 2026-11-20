using System.Security.Cryptography;
using System.Text.Json;
using Aplicacion.CasosDeUso;
using Dominio.Entidades;
using Infraestructura.DBContext;

namespace StudySpace.Servicios;

/// <summary>
/// HU16: abre copias .ssbackup v1 creadas por ServicioRespaldoCifrado.
/// Previsualiza los datos descifrados y permite añadir solo cursos/tareas
/// faltantes, sin borrar ni sobrescribir información del dispositivo.
/// No copia usuarios, sesiones ni vínculos de Google/Android.
/// </summary>
public sealed class ServicioRestauracionCifrada
{
    private const int MaximoArchivoBytes = 10 * 1024 * 1024;
    private const int MaximoCursos = 500;
    private const int MaximoTareas = 20000;
    private const int Version = 1;
    private const int Iteraciones = 210_000;
    private const int EtiquetaBytes = 16;

    private readonly ContextoBaseDatos _contexto;
    private readonly ServicioAutenticacion _autenticacion;
    private readonly ServicioNotificaciones _notificaciones;
    private DatosRespaldo? _pendiente;
    private Guid _identificadorPendiente;
    private int _usuarioPendiente;

    public ServicioRestauracionCifrada(
        ContextoBaseDatos contexto,
        ServicioAutenticacion autenticacion,
        ServicioNotificaciones notificaciones)
    {
        _contexto = contexto;
        _autenticacion = autenticacion;
        _notificaciones = notificaciones;
    }

    public sealed record VistaPrevia(
        Guid Id,
        DateTimeOffset CreadoUtc,
        int CursosEnArchivo,
        int TareasEnArchivo,
        int CursosPorAgregar,
        int TareasPorAgregar);

    public sealed record ResultadoRestauracion(
        int CursosAgregados,
        int TareasAgregadas,
        int RecordatoriosProgramados);

    public void DescartarVistaPrevia()
    {
        _pendiente = null;
        _identificadorPendiente = Guid.Empty;
        _usuarioPendiente = 0;
    }

    /// <summary>
    /// Abre el archivo elegido mediante FilePicker, lo descifra en memoria y
    /// valida su versión, estructura y titular remoto antes de permitir cambios.
    /// </summary>
    public async Task<VistaPrevia> PrepararAsync(Stream archivo, string contrasena)
    {
        DescartarVistaPrevia();

        if (string.IsNullOrEmpty(contrasena) || contrasena.Length is < 12 or > 256)
            throw new InvalidDataException("Introduce la contraseña utilizada para crear la copia.");

        int usuarioLocal = _autenticacion.UsuarioActual?.Id ?? 0;
        if (usuarioLocal <= 0)
            throw new InvalidOperationException("Inicia sesión para restaurar una copia.");

        // Acotamos el tamaño ANTES de intentar procesar JSON o derivar claves.
        using var memoria = new MemoryStream();
        byte[] bloque = new byte[81920];
        int leidos;
        while ((leidos = await archivo.ReadAsync(bloque.AsMemory())) != 0)
        {
            if (memoria.Length + leidos > MaximoArchivoBytes)
                throw new InvalidDataException("El archivo es demasiado grande.");
            memoria.Write(bloque, 0, leidos);
        }
        if (memoria.Length == 0)
            throw new InvalidDataException("El archivo está vacío.");

        ContenedorCifrado contenedor;
        try
        {
            contenedor = JsonSerializer.Deserialize<ContenedorCifrado>(memoria.ToArray())
                ?? throw new InvalidDataException("El archivo no es una copia de Study Space.");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("El archivo no tiene un formato de respaldo válido.");
        }

        // No aceptamos parámetros KDF elegidos arbitrariamente por un archivo.
        if (contenedor.Formato != "studyspace-backup-aes256gcm" ||
            contenedor.Version != Version ||
            contenedor.Kdf != "PBKDF2-SHA256" ||
            contenedor.Iteraciones != Iteraciones)
        {
            throw new InvalidDataException("Formato o versión de respaldo no compatible.");
        }

        byte[] sal, nonce, etiqueta, cifrado;
        try
        {
            sal = Convert.FromBase64String(contenedor.Sal ?? "");
            nonce = Convert.FromBase64String(contenedor.Nonce ?? "");
            etiqueta = Convert.FromBase64String(contenedor.Etiqueta ?? "");
            cifrado = Convert.FromBase64String(contenedor.DatosCifrados ?? "");
        }
        catch (FormatException)
        {
            throw new InvalidDataException("El archivo cifrado está incompleto o dañado.");
        }

        if (sal.Length != 16 || nonce.Length != 12 || etiqueta.Length != EtiquetaBytes ||
            cifrado.Length == 0 || cifrado.Length > MaximoArchivoBytes)
            throw new InvalidDataException("El archivo cifrado está incompleto o dañado.");

        byte[] clave = Rfc2898DeriveBytes.Pbkdf2(
            contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, 32);
        byte[] plano = new byte[cifrado.Length];
        DatosRespaldo datos;
        try
        {
            try
            {
                using var aes = new AesGcm(clave, EtiquetaBytes);
                aes.Decrypt(nonce, cifrado, etiqueta, plano);
            }
            catch (CryptographicException)
            {
                throw new InvalidDataException(
                    "Contraseña incorrecta o archivo alterado. No se modificaron tus datos.");
            }

            try
            {
                datos = JsonSerializer.Deserialize<DatosRespaldo>(plano)
                    ?? throw new InvalidDataException("El respaldo no contiene información válida.");
            }
            catch (JsonException)
            {
                throw new InvalidDataException("Los datos internos del respaldo no son válidos.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
            CryptographicOperations.ZeroMemory(plano);
        }

        ValidarContenido(datos);
        var db = await _contexto.ObtenerDb();
        await ValidarCuentaAsync(db, usuarioLocal, datos.CuentaRemotaId);

        var cursosActuales = await db.Table<Curso>()
            .Where(c => c.UsuarioId == usuarioLocal).ToListAsync();
        var tareasActuales = await db.Table<Tarea>()
            .Where(t => t.UsuarioId == usuarioLocal).ToListAsync();
        var (nuevosCursos, nuevasTareas) = ContarPendientes(
            datos, cursosActuales, tareasActuales);

        // Conservamos el objeto descifrado SOLO en memoria mientras la pantalla
        // muestra la vista previa. Nunca escribimos un archivo sin cifrar.
        _pendiente = datos;
        _usuarioPendiente = usuarioLocal;
        _identificadorPendiente = Guid.NewGuid();
        return new VistaPrevia(
            _identificadorPendiente, datos.CreadoUtc,
            datos.Cursos.Count, datos.Tareas.Count, nuevosCursos, nuevasTareas);
    }

    /// <summary>
    /// Una transacción SQLite inserta exclusivamente cursos/tareas que no
    /// existan. Si falla una inserción, se revierte TODA la importación.
    /// </summary>
    public async Task<ResultadoRestauracion> RestaurarAsync(Guid identificador)
    {
        DatosRespaldo datos = _pendiente
            ?? throw new InvalidOperationException("Selecciona primero una copia válida.");
        int usuarioLocal = _usuarioPendiente;
        if (identificador == Guid.Empty || identificador != _identificadorPendiente ||
            usuarioLocal <= 0 || _autenticacion.UsuarioActual?.Id != usuarioLocal)
        {
            DescartarVistaPrevia();
            throw new InvalidOperationException("La sesión cambió. Vuelve a seleccionar el archivo.");
        }

        var db = await _contexto.ObtenerDb();
        await ValidarCuentaAsync(db, usuarioLocal, datos.CuentaRemotaId);

        int cursosNuevos = 0;
        int tareasNuevas = 0;
        var avisos = new List<(int Id, string Titulo, DateTime Fecha, int Minutos, string Curso)>();

        await db.RunInTransactionAsync(conexion =>
        {
            // Revalidar dentro de la misma transacción, antes de insertar.
            var vinculo = conexion.Table<VinculoUsuarioRemoto>()
                .Where(v => v.UsuarioLocalId == usuarioLocal).FirstOrDefault();
            if (vinculo is null || vinculo.UsuarioRemotoId != datos.CuentaRemotaId)
                throw new InvalidOperationException("Esta copia no pertenece a la cuenta activa.");

            var cursosLocales = conexion.Table<Curso>()
                .Where(c => c.UsuarioId == usuarioLocal).ToList();
            var tareasLocales = conexion.Table<Tarea>()
                .Where(t => t.UsuarioId == usuarioLocal).ToList();

            var cursosPorNombre = new Dictionary<string, Curso>(StringComparer.OrdinalIgnoreCase);
            foreach (var curso in cursosLocales)
                cursosPorNombre.TryAdd(Canon(curso.Nombre), curso);

            var mapa = new Dictionary<int, Curso>();
            foreach (var original in datos.Cursos)
            {
                var claveCurso = Canon(original.Nombre);
                if (!cursosPorNombre.TryGetValue(claveCurso, out var curso))
                {
                    curso = new Curso
                    {
                        UsuarioId = usuarioLocal,
                        Nombre = original.Nombre.Trim(),
                        Color = ColorValido(original.Color) ? original.Color : "#304BFF"
                    };
                    conexion.Insert(curso);
                    cursosPorNombre.Add(claveCurso, curso);
                    cursosNuevos++;
                }
                mapa.Add(original.IdOriginal, curso);
            }

            // Contar coincidencias (no solo usar HashSet): el usuario puede
            // tener legítimamente dos tareas con mismo curso, título y fecha.
            // Se conserva esa multiplicidad sin importar duplicados al repetir.
            var coincidenciasExistentes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var actual in tareasLocales)
            {
                var clave = ClaveTarea(actual.CursoId, actual.Titulo, actual.FechaLimite);
                coincidenciasExistentes[clave] =
                    coincidenciasExistentes.GetValueOrDefault(clave) + 1;
            }

            foreach (var original in datos.Tareas)
            {
                var curso = mapa[original.CursoIdOriginal];
                var clave = ClaveTarea(curso.Id, original.Titulo, original.FechaLimite);
                // No tocar datos locales coincidentes. Si la copia contiene
                // varias tareas iguales, importar solo la cantidad faltante.
                if (coincidenciasExistentes.TryGetValue(clave, out int restantes)
                    && restantes > 0)
                {
                    coincidenciasExistentes[clave] = restantes - 1;
                    continue;
                }

                var tarea = new Tarea
                {
                    UsuarioId = usuarioLocal,
                    CursoId = curso.Id,
                    Titulo = original.Titulo.Trim(),
                    Descripcion = original.Descripcion,
                    FechaLimite = original.FechaLimite,
                    Prioridad = original.Prioridad,
                    Completada = original.Completada,
                    FechaCompletada = original.Completada ? original.FechaCompletada : null,
                    MinutosAviso = original.MinutosAviso
                };
                conexion.Insert(tarea);
                tareasNuevas++;

                if (!tarea.Completada && tarea.FechaLimite > DateTime.Now)
                    avisos.Add((tarea.Id, tarea.Titulo, tarea.FechaLimite,
                        tarea.MinutosAviso, curso.Nombre));
            }
        });

        DescartarVistaPrevia();

        // Los avisos son secundarios: un fallo al programarlos NO revierte
        // la transacción ni pone en riesgo los datos ya restaurados.
        int programados = 0;
        foreach (var a in avisos)
        {
            try
            {
                if (await _notificaciones.ProgramarRecordatorio(
                    a.Id, a.Titulo, a.Fecha, a.Minutos, a.Curso))
                    programados++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"HU16: aviso no programado ({ex.GetType().Name}).");
            }
        }

        return new ResultadoRestauracion(cursosNuevos, tareasNuevas, programados);
    }

    private static async Task ValidarCuentaAsync(
        SQLite.SQLiteAsyncConnection db, int local, long remoto)
    {
        if (remoto <= 0)
            throw new InvalidDataException("El archivo no identifica una cuenta válida.");
        var vinculo = await db.Table<VinculoUsuarioRemoto>()
            .Where(v => v.UsuarioLocalId == local).FirstOrDefaultAsync();
        if (vinculo is null || vinculo.UsuarioRemotoId != remoto)
            throw new InvalidOperationException(
                "Esta copia pertenece a otra cuenta de Study Space. Inicia sesión en la cuenta original.");
    }

    private static string Canon(string texto) => (texto ?? "").Trim().ToUpperInvariant();
    private static string ClaveTarea(int curso, string titulo, DateTime fecha) =>
        $"{curso}|{fecha.Ticks}|{Canon(titulo)}";

    private static bool ColorValido(string? color) =>
        color?.Length == 7 && color[0] == '#' &&
        color.Skip(1).All(Uri.IsHexDigit);

    private static (int Cursos, int Tareas) ContarPendientes(
        DatosRespaldo datos, List<Curso> cursos, List<Tarea> tareas)
    {
        var nombres = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cursos)
            nombres.TryAdd(Canon(c.Nombre), c.Id);
        int ficticio = -1;
        int nuevosCursos = 0;
        var mapa = new Dictionary<int, int>();
        foreach (var c in datos.Cursos)
        {
            var clave = Canon(c.Nombre);
            if (!nombres.TryGetValue(clave, out int id))
            {
                id = ficticio--;
                nombres.Add(clave, id);
                nuevosCursos++;
            }
            mapa.Add(c.IdOriginal, id);
        }
        var coincidencias = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var actual in tareas)
        {
            var clave = ClaveTarea(actual.CursoId, actual.Titulo, actual.FechaLimite);
            coincidencias[clave] = coincidencias.GetValueOrDefault(clave) + 1;
        }
        int nuevasTareas = 0;
        foreach (var t in datos.Tareas)
        {
            var clave = ClaveTarea(mapa[t.CursoIdOriginal], t.Titulo, t.FechaLimite);
            if (coincidencias.TryGetValue(clave, out int restantes) && restantes > 0)
                coincidencias[clave] = restantes - 1;
            else
                nuevasTareas++;
        }
        return (nuevosCursos, nuevasTareas);
    }

    private static void ValidarContenido(DatosRespaldo d)
    {
        if (d.Formato != "studyspace-academico" || d.Version != Version ||
            d.CuentaRemotaId <= 0 || d.Cursos is null || d.Tareas is null ||
            d.Cursos.Count > MaximoCursos || d.Tareas.Count > MaximoTareas)
            throw new InvalidDataException("La copia contiene datos incompatibles.");

        var idsCursos = new HashSet<int>();
        foreach (var c in d.Cursos)
        {
            if (c is null || c.IdOriginal <= 0 || !idsCursos.Add(c.IdOriginal) ||
                string.IsNullOrWhiteSpace(c.Nombre) || c.Nombre.Length > 180)
                throw new InvalidDataException("Se encontró un curso no válido en la copia.");
        }
        var idsTareas = new HashSet<int>();
        foreach (var t in d.Tareas)
        {
            if (t is null || t.IdOriginal <= 0 || !idsTareas.Add(t.IdOriginal) ||
                !idsCursos.Contains(t.CursoIdOriginal) ||
                string.IsNullOrWhiteSpace(t.Titulo) || t.Titulo.Length > 250 ||
                t.Descripcion is null || t.Descripcion.Length > 10000 ||
                t.FechaLimite.Year < 2000 || t.FechaLimite.Year > 2200 ||
                t.Prioridad is not ("Alta" or "Media" or "Baja") ||
                t.MinutosAviso < 0 || t.MinutosAviso > 43200)
                throw new InvalidDataException("Se encontró una tarea no válida en la copia.");
        }
    }

    // Coinciden con los nombres de propiedad serializados por
    // ServicioRespaldoCifrado (formato .ssbackup v1).
    private sealed class ContenedorCifrado
    {
        public string Formato { get; set; } = "";
        public int Version { get; set; }
        public string Kdf { get; set; } = "";
        public int Iteraciones { get; set; }
        public string? Sal { get; set; }
        public string? Nonce { get; set; }
        public string? Etiqueta { get; set; }
        public string? DatosCifrados { get; set; }
    }
    private sealed class DatosRespaldo
    {
        public string Formato { get; set; } = "";
        public int Version { get; set; }
        public long CuentaRemotaId { get; set; }
        public DateTimeOffset CreadoUtc { get; set; }
        public List<CursoRespaldo> Cursos { get; set; } = new();
        public List<TareaRespaldo> Tareas { get; set; } = new();
    }
    private sealed class CursoRespaldo
    {
        public int IdOriginal { get; set; }
        public string Nombre { get; set; } = "";
        public string Color { get; set; } = "#304BFF";
    }
    private sealed class TareaRespaldo
    {
        public int IdOriginal { get; set; }
        public int CursoIdOriginal { get; set; }
        public string Titulo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public DateTime FechaLimite { get; set; }
        public string Prioridad { get; set; } = "Media";
        public bool Completada { get; set; }
        public DateTime? FechaCompletada { get; set; }
        public int MinutosAviso { get; set; }
    }
}
