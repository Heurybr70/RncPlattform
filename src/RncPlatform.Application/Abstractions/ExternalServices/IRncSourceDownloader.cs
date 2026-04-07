using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RncPlatform.Application.Abstractions.ExternalServices;

public interface IRncSourceDownloader
{
    /// <summary>
    /// Descarga el archivo (si es ZIP lo extrae a TXT) y devuelve la ruta temporal del archivo fuente y su nombre base.
    /// </summary>
    Task<(string FilePath, string SourceName)> DownloadLatestDataAsync(CancellationToken cancellationToken = default);
    Task<string> GetLastFileHashAsync(CancellationToken cancellationToken = default);
}
