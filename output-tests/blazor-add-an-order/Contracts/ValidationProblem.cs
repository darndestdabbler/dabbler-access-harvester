using System.Collections.Generic;

namespace BlazorAddAnOrder.Contracts;

public sealed class ValidationProblemResponse
{
    public string Message { get; set; } = string.Empty;

    public List<string> Errors { get; set; } = new();
}
