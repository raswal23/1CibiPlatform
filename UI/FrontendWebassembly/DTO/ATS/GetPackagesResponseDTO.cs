namespace FrontendWebassembly.DTO.ATS;

public class GetPackagesResponseDTO
{
	public KeysetPaginatedResult<PackageDetailsDTO>? Packages { get; set; }
}
