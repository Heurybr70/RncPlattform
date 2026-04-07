using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Contracts.Responses;

namespace RncPlatform.Application.Features.Rncs.Services;

public interface IRncQueryService
{
    Task<TaxpayerDto?> GetByRncAsync(string rnc, CancellationToken cancellationToken = default);
    Task<PagedResponse<TaxpayerSearchItemDto>> SearchAsync(string term, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<System.Collections.Generic.IEnumerable<TaxpayerChangeDto>> GetChangesByRncAsync(string rnc, CancellationToken cancellationToken = default);
}
