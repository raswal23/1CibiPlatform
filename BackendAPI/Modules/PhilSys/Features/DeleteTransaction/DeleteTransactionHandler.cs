namespace PhilSys.Features.DeleteTransaction;
public record DeleteTransactionCommand(string HashToken) : ICommand<DeleteTransactionResult>;
public record DeleteTransactionResult(bool IsDeleted);
public class DeleteTransactionCommandValidator : AbstractValidator<DeleteTransactionCommand>
{
	public DeleteTransactionCommandValidator()
	{
		RuleFor(x => x.HashToken)
			.NotEmpty().WithMessage("HashToken is required.");
	}
}

public class DeleteTransactionHandler : ICommandHandler<DeleteTransactionCommand, DeleteTransactionResult>
{
	private readonly DeleteTransactionService _deleteTransactionService;
	public DeleteTransactionHandler(DeleteTransactionService deleteTransactionService)
	{
		_deleteTransactionService = deleteTransactionService;
	}
	public async Task<DeleteTransactionResult> Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
	{
		var deletedTransaction = await _deleteTransactionService.DeleteTransactionAsync(request.HashToken);
		return new DeleteTransactionResult(deletedTransaction);
	}
}
