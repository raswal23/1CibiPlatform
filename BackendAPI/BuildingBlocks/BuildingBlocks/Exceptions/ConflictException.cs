namespace BuildingBlocks.Exceptions;

/// <summary>
/// The request was understood and authorized, but the resource is in a state that
/// forbids it - a form already submitted, an invitation already withdrawn. Maps to 409
/// so callers can tell "you may not" apart from "not any more".
/// </summary>
public class ConflictException : Exception
{
	public ConflictException(string message) : base(message)
	{
	}

	public ConflictException(string message, string details) : base(message)
	{
		Details = details;
	}

	public string? Details { get; }
}
