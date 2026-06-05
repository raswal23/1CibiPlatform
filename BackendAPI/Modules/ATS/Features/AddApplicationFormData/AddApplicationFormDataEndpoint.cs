using Microsoft.AspNetCore.Mvc;

namespace ATS.Features.AddApplicationFormData;

public record AddApplicationFormDataRequest(PersonalDetailsDTO PersonalDetails, 
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
		app.MapPost("addapplicationformdata", async ([FromForm] AddApplicationFormDataRequest request, ISender sender, CancellationToken cancellationToken) =>
		{
			var command = new AddApplicationFormDataCommand(request.PersonalDetails, 
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
			   .DisableAntiforgery()
		  .WithName("AddApplicationFormData")
		  .WithTags("ATS")
		  .Produces<AddApplicationFormDataResponse>()
		  .ProducesProblem(StatusCodes.Status400BadRequest)
		  .WithSummary("Add Application Form Data")
		  .WithDescription("Adds a new application form data entry to the database.");
	}
}
