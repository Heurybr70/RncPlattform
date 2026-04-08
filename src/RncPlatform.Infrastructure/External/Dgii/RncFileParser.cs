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
                ActividadEconomica = parts[3].Trim(), // En el TXT actual, la actividad/categoría parece estar en el mismo campo
                FechaConstitucion = parts.Length > 8 ? parts[8].Trim() : null,
                Estado = parts.Length > 9 ? parts[9].Trim() : null,
                RegimenPago = parts.Length > 10 ? parts[10].Trim() : null,
            };
        }
    }
}
