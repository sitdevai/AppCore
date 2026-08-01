using System.ComponentModel.DataAnnotations;
using AppCore.Api.ErrorHandling;

namespace AppCore.Api.Validation;

public sealed class DataAnnotationsValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        Dictionary<string, string[]> errors = context.Arguments
            .OfType<object>()
            .SelectMany(ValidateArgument)
            .GroupBy(
                result => result.MemberNames.FirstOrDefault() ?? string.Empty,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(result => result.ErrorMessage ?? "validation.invalid")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        if (errors.Count == 0)
        {
            return await next(context);
        }

        return Results.ValidationProblem(
            errors,
            statusCode: StatusCodes.Status400BadRequest,
            title: "validation.failed",
            type: ProblemTypes.Validation);
    }

    private static IEnumerable<ValidationResult> ValidateArgument(
        object argument)
    {
        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(argument);
        _ = Validator.TryValidateObject(
            argument,
            validationContext,
            results,
            validateAllProperties: true);

        return results;
    }
}
