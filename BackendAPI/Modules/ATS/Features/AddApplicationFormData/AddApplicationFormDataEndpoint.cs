namespace ATS.Features.AddApplicationFormData;

public record AddApplicationFormDataRequest(string HashToken,
											PersonalDetailsDTO PersonalDetails,
											AddressDetailsDTO AddressDetails,
											EducationalBackgroundDTO EducationalBackground,
											LicensesDetailsDTO LicensesDetails,
											ProfessionalExperiencesDTO ProfessionalExperiences,
											ReferenceDetailsDTO ReferenceDetails,
											SignatureDetailsDTO SignatureDetails);
public record AddApplicationFormDataResponse(bool IsAdded);

public class AddApplicationFormDataEndpoint : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		// Anonymous by design: candidates fill this in from an emailed link without an
		// account. The hash token is what authorizes the write - see
		// ApplicationFormService.AuthorizeApplicationFormAsync.
		app.MapPost("addapplicationformdata", async ([FromForm] AddApplicationFormDataRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddApplicationFormDataCommand(request.HashToken,
															request.PersonalDetails,
															request.AddressDetails,
															request.EducationalBackground,
															request.LicensesDetails,
															request.ProfessionalExperiences,
															request.ReferenceDetails,
															request.SignatureDetails);
			AddApplicationFormDataResult result = await sender.Send(command, cancellationToken);
			var response = new AddApplicationFormDataResponse(result.IsAdded);
			return Results.Ok(response.IsAdded);
		})
		.AllowAnonymous()
		.DisableAntiforgery()
		.WithName("AddApplicationFormData")
		.WithTags("ATS")
		.Produces<bool>()
		.ProducesProblem(StatusCodes.Status400BadRequest)
		.ProducesProblem(StatusCodes.Status404NotFound)
		.ProducesProblem(StatusCodes.Status409Conflict)
		.WithSummary("Add Application Form Data")
		.WithDescription("Adds a new application form data entry to the database. Authorized by the emailed hash token.");
	}
}
