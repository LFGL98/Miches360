using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Dominio.Negocio;

namespace Miches360.Infraestructura.Persistencia;

public static class InicializadorBaseDatos
{
    public const string PinInicial = "1234";

    public const string AnotacionValorInicial = "Miches360:ValorInicial";

    public static async Task InicializarAsync(IServiceProvider servicios, CancellationToken cancellationToken = default)
    {
        var fabrica = servicios.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var hasher = servicios.GetRequiredService<IHasherPin>();

        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        await db.Database.EnsureCreatedAsync(cancellationToken);
        await AgregarColumnasNuevasAsync(db, cancellationToken);
        await AgregarTablasNuevasAsync(db, cancellationToken);

        if (!await db.Categorias.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            DatosIniciales.Cargar(db);
        }

        if (!await db.ConfiguracionesNegocio.AnyAsync(cancellationToken))
        {
            db.ConfiguracionesNegocio.Add(new ConfiguracionNegocio(hasher.Calcular(PinInicial)));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task AgregarTablasNuevasAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var script = db.Database.GenerateCreateScript();
        var sentencias = script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var sentencia in sentencias)
        {
            if (!sentencia.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sql = sentencia
                .Replace("CREATE TABLE \"", "CREATE TABLE IF NOT EXISTS \"", StringComparison.OrdinalIgnoreCase)
                .Replace("CREATE UNIQUE INDEX \"", "CREATE UNIQUE INDEX IF NOT EXISTS \"", StringComparison.OrdinalIgnoreCase)
                .Replace("CREATE INDEX \"", "CREATE INDEX IF NOT EXISTS \"", StringComparison.OrdinalIgnoreCase);

            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    private static async Task AgregarColumnasNuevasAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var conexion = db.Database.GetDbConnection();
        var debeCerrar = conexion.State != ConnectionState.Open;
        if (debeCerrar)
        {
            await conexion.OpenAsync(cancellationToken);
        }

        try
        {
            var modelo = db.GetService<IDesignTimeModel>().Model;
            foreach (var entidad in modelo.GetEntityTypes())
            {
                var tabla = entidad.GetTableName();
                if (tabla is null)
                {
                    continue;
                }

                var existentes = await LeerColumnasAsync(conexion, tabla, cancellationToken);
                if (existentes.Count == 0)
                {
                    continue;
                }

                var tablaId = StoreObjectIdentifier.Table(tabla, entidad.GetSchema());
                foreach (var propiedad in entidad.GetProperties())
                {
                    var columna = propiedad.GetColumnName(tablaId);
                    if (columna is null || existentes.Contains(columna))
                    {
                        continue;
                    }

                    var tipo = propiedad.GetColumnType();
                    var sql = propiedad.IsColumnNullable(tablaId)
                        ? $"ALTER TABLE \"{tabla}\" ADD COLUMN \"{columna}\" {tipo} NULL"
                        : $"ALTER TABLE \"{tabla}\" ADD COLUMN \"{columna}\" {tipo} NOT NULL DEFAULT {ValorInicial(propiedad, tipo)}";

                    using var comando = conexion.CreateCommand();
                    comando.CommandText = sql;
                    await comando.ExecuteNonQueryAsync(cancellationToken);
                    existentes.Add(columna);
                }
            }
        }
        finally
        {
            if (debeCerrar)
            {
                await conexion.CloseAsync();
            }
        }
    }

    private static async Task<HashSet<string>> LeerColumnasAsync(DbConnection conexion, string tabla, CancellationToken cancellationToken)
    {
        var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var comando = conexion.CreateCommand();
        comando.CommandText = $"PRAGMA table_info(\"{tabla.Replace("\"", "\"\"")}\")";

        using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        var indiceNombre = lector.GetOrdinal("name");
        while (await lector.ReadAsync(cancellationToken))
        {
            columnas.Add(lector.GetString(indiceNombre));
        }

        return columnas;
    }

    private static string ValorInicial(IProperty propiedad, string tipo)
    {
        if (propiedad.FindAnnotation(AnotacionValorInicial)?.Value is string valor)
        {
            return valor;
        }

        var clr = Nullable.GetUnderlyingType(propiedad.ClrType) ?? propiedad.ClrType;
        var esTexto = tipo.Contains("TEXT", StringComparison.OrdinalIgnoreCase);

        if (clr.IsEnum)
        {
            var primero = Enum.GetNames(clr).FirstOrDefault() ?? string.Empty;
            return esTexto ? $"'{primero}'" : "0";
        }

        if (clr == typeof(Guid))
        {
            return $"'{Guid.Empty}'";
        }

        if (clr == typeof(DateTime))
        {
            return $"'{DateTime.MinValue.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}'";
        }

        if (clr == typeof(decimal))
        {
            return "'0.0'";
        }

        return esTexto ? "''" : "0";
    }
}
