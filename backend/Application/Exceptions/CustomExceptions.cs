namespace CleanArchitectur.Application.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"L'entité \"{name}\" ({key}) est introuvable.") { }
}

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base("Accès interdit.") { }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("Une ou plusieurs erreurs de validation se sont produites.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors) : this()
    {
        Errors = errors;
    }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
