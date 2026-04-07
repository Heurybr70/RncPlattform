namespace RncPlatform.Contracts.Responses;

public class TaxpayerSearchItemDto
{
    public string Rnc { get; set; } = default!;
    public string NombreORazonSocial { get; set; } = default!;
    public string? NombreComercial { get; set; }
    public string? Estado { get; set; }
    public bool IsActive { get; set; }
}
