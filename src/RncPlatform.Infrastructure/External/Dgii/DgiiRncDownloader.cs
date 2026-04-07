using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
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

    public DgiiRncDownloader(HttpClient httpClient, IConfiguration configuration, ILogger<DgiiRncDownloader> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(string FilePath, string SourceName)> DownloadLatestDataAsync(CancellationToken cancellationToken = default)
    {
        var zipUrl = _configuration["Dgii:ZipUrl"] ?? "https://dgii.gov.do/app/WebApps/ConsultasWeb2/ConsultasWeb/consultas/rnc.aspx/DGII_RNC.zip";
        var sourceName = _configuration["Dgii:SourceName"] ?? "DGII_RNC";

        _logger.LogInformation("Descargando archivo desde {Url}", zipUrl);

        var response = await _httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tempZipPath = Path.GetTempFileName();
        var tempTxtPath = Path.GetTempFileName() + ".txt";

        using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
        using (var fileStream = File.Create(tempZipPath))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("Archivo ZIP descargado en {TempZipPath}. Extrayendo TXT...", tempZipPath);

        // DGII file is usually a ZIP with a single TXT file inside
        using (var zip = ZipFile.OpenRead(tempZipPath))
        {
            if (zip.Entries.Count == 0)
                throw new InvalidOperationException("El ZIP descargado está vacío.");

            var entry = zip.Entries[0]; // Assume first file is the TXT
            entry.ExtractToFile(tempTxtPath, overwrite: true);
        }

        File.Delete(tempZipPath);
        _logger.LogInformation("Archivo TXT extraído en {TempTxtPath}", tempTxtPath);

        return (tempTxtPath, sourceName);
    }

    public Task<string> GetLastFileHashAsync(CancellationToken cancellationToken = default)
    {
        // En un mundo ideal haríamos un HEAD request a la web de DGII o checaríamos ETag.
        // Simularemos con GUID por ahora o dejaremos que se calcule por archivo.
        return Task.FromResult(Guid.NewGuid().ToString());
    }
}
