using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RncPlatform.Application.Abstractions.ExternalServices;

namespace RncPlatform.Infrastructure.External.Dgii;

public class DgiiRncDownloader : IRncSourceDownloader
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DgiiRncDownloader> _logger;
    private string? _lastDownloadedFilePath;

    public DgiiRncDownloader(HttpClient httpClient, IConfiguration configuration, ILogger<DgiiRncDownloader> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(string FilePath, string SourceName)> DownloadLatestDataAsync(CancellationToken cancellationToken = default)
    {
        var zipUrl = _configuration["Dgii:ZipUrl"] ?? "https://dgii.gov.do/app/WebApps/Consultas/RNC/DGII_RNC.zip";
        var sourceName = _configuration["Dgii:SourceName"] ?? "DGII_RNC";

        _logger.LogInformation("Descargando archivo desde {Url}", zipUrl);

        const int maxRetries = 3;
        HttpResponseMessage? response = null;
        
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                response = await _httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode) break;
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Intento de descarga {Attempt} fallido. Reintentando...", i + 1);
                await Task.Delay(2000, cancellationToken);
            }
        }

        response!.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType != null && contentType.Contains("text/html"))
        {
            throw new InvalidOperationException("El servidor de DGII devolvió una página HTML en lugar del ZIP. Es posible que el servicio esté temporalmente bloqueado o en mantenimiento.");
        }

        var tempZipPath = Path.GetTempFileName();
        var tempTxtPath = Path.GetTempFileName() + ".txt";

        using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
        using (var fileStream = File.Create(tempZipPath))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        var fileInfo = new FileInfo(tempZipPath);
        if (fileInfo.Length < 1000)
        {
            if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
            throw new InvalidOperationException($"El archivo descargado es demasiado pequeño ({fileInfo.Length} bytes). Posible descarga corrupta.");
        }

        _logger.LogInformation("Archivo ZIP descargado ({Size} bytes) en {TempZipPath}. Extrayendo TXT...", fileInfo.Length, tempZipPath);

        try
        {
            using (var zip = ZipFile.OpenRead(tempZipPath))
            {
                if (zip.Entries.Count == 0)
                    throw new InvalidOperationException("El ZIP descargado no contiene archivos.");

                var entry = zip.Entries[0];
                entry.ExtractToFile(tempTxtPath, overwrite: true);
            }
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "Error al abrir el archivo ZIP. Parece estar corrupto.");
            throw new InvalidOperationException("El archivo descargado no es un ZIP válido o está incompleto. Por favor, intente de nuevo en unos minutos.", ex);
        }
        finally
        {
            if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
        }

        _logger.LogInformation("Archivo TXT extraído exitosamente en {TempTxtPath}", tempTxtPath);
        _lastDownloadedFilePath = tempTxtPath;
        return (tempTxtPath, sourceName);
    }

    public async Task<string> GetLastFileHashAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_lastDownloadedFilePath) || !File.Exists(_lastDownloadedFilePath))
        {
            throw new InvalidOperationException("No existe un archivo descargado disponible para calcular su hash.");
        }

        await using var stream = File.OpenRead(_lastDownloadedFilePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
