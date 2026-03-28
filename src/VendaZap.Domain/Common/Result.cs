namespace VendaZap.Domain.Common;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None) throw new InvalidOperationException();
        if (!isSuccess && error == Error.None) throw new InvalidOperationException();
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
        => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failed result.");
}

public record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "Null value provided.");

    // Domain errors
    public static Error NotFound(string entity) => new($"{entity}.NotFound", $"{entity} não encontrado.");
    public static Error Conflict(string entity) => new($"{entity}.Conflict", $"{entity} já existe.");
    public static Error Unauthorized() => new("Auth.Unauthorized", "Não autorizado.");
    public static Error Forbidden() => new("Auth.Forbidden", "Acesso negado.");
    public static Error Validation(string field, string msg) => new($"Validation.{field}", msg);
    public static Error BusinessRule(string code, string msg) => new($"Business.{code}", msg);
}
