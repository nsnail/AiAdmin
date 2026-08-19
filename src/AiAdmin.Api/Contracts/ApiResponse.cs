namespace AiAdmin.Api.Contracts;

public sealed record ApiResponse<T>(int Code, string Msg, T? Data)
{
    public static ApiResponse<T> Ok(
        T data
        , string message = "OK"
    ) {
        return new ApiResponse<T>(200, message, data);
    }
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Records, int Current, int Size, int Total);