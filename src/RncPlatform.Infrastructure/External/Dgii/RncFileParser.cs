using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using RncPlatform.Application.Abstractions.ExternalServices;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.External.Dgii;

public class RncFileParser : IRncFileParser
{
    public async IAsyncEnumerable<RncStaging> ParseFileAsync(string filePath, Guid executionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // DGII files typically use ISO-8859-1 or plain ANSI.
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.GetEncoding("ISO-8859-1"));

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split('|');
            if (parts.Length < 11) continue; // Formato esperado según DGII

            var rnc = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(rnc)) continue;

            yield return new RncStaging
            {
                ExecutionId = executionId,
                Rnc = rnc,
                NombreORazonSocial = parts[1].Trim(),
                NombreComercial = parts[2].Trim(),
                Categoria = parts[3].Trim(),
                RegimenPago = parts[4].Trim(),
                Estado = parts[5].Trim(),
                ActividadEconomica = parts[6].Trim(),
                // ...otros campos se mapearían según pos en el TXT real
                // Por ejemplo, parts 7, 8, etc. Depende de DGII 2024.
            };
        }
    }
}
