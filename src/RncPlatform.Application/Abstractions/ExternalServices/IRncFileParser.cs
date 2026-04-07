using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Application.Abstractions.ExternalServices;

public interface IRncFileParser
{
    IAsyncEnumerable<RncStaging> ParseFileAsync(string filePath, System.Guid executionId, CancellationToken cancellationToken = default);
}
