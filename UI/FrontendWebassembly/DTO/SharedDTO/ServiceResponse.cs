namespace FrontendWebassembly.DTO.SharedDTO;

public record ServiceResponse<T>(bool IsSuccess, string ErrorDetail, T? Data)
{
	public static ServiceResponse<T> Success(T data) => new(true, string.Empty, data);
	public static ServiceResponse<T> Failure(string errorDetail) => new(false, errorDetail, default);
}
