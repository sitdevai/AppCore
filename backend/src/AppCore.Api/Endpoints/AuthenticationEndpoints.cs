using System.Globalization;
using System.Security.Claims;
using Asp.Versioning;
using Asp.Versioning.Builder;
using AppCore.Api.RateLimiting;
using AppCore.Api.Security;
using AppCore.Api.Validation;
using AppCore.Application.Security;
using AppCore.Contracts.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace AppCore.Api.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ApiVersionSet versionSet = endpoints
            .NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();
        RouteGroupBuilder auth = endpoints
            .MapGroup("/api/v{version:apiVersion}/auth")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(new ApiVersion(1, 0))
            .WithTags("Authentication")
            .AddEndpointFilter<DataAnnotationsValidationFilter>();

        auth.MapGet("/csrf", BootstrapCsrfAsync)
            .AllowAnonymous();
        auth.MapGet("/recovery/csrf", BootstrapCsrfAsync)
            .RequireAuthorization("RecoveryOnly");
        auth.MapPost("/pre-session", CreatePreSessionAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost("/login/mfa", CompleteMfaAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost(
                "/activation/complete",
                (
                    ChallengeCompletionRequest request,
                    HttpContext context,
                    IAntiforgery antiforgery,
                    IAccountLifecycleService lifecycle,
                    CancellationToken cancellationToken) =>
                    CompleteChallengeAsync(
                        "activation",
                        request,
                        context,
                        antiforgery,
                        lifecycle,
                        cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost(
                "/password-reset/complete",
                (
                    ChallengeCompletionRequest request,
                    HttpContext context,
                    IAntiforgery antiforgery,
                    IAccountLifecycleService lifecycle,
                    CancellationToken cancellationToken) =>
                    CompleteChallengeAsync(
                        "password-reset",
                        request,
                        context,
                        antiforgery,
                        lifecycle,
                        cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapGet("/me", CurrentUserAsync);
        auth.MapPost("/password/change", ChangePasswordAsync)
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost("/mfa/enrollment", BeginMfaEnrollmentAsync)
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost("/mfa/enrollment/verify", VerifyMfaEnrollmentAsync)
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost("/logout", LogoutAsync);
        auth.MapPost("/recovery", BeginRecoveryAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost("/recovery/mfa/enrollment", BeginRecoveryMfaEnrollmentAsync)
            .RequireAuthorization("RecoveryOnly")
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost(
                "/recovery/mfa/enrollment/verify",
                VerifyRecoveryMfaEnrollmentAsync)
            .RequireAuthorization("RecoveryOnly")
            .RequireRateLimiting(RateLimitingPolicyNames.Sensitive);
        auth.MapPost("/recovery/logout", LogoutRecoveryAsync)
            .RequireAuthorization("RecoveryOnly");

        return endpoints;
    }

    private static async Task<IResult> BootstrapCsrfAsync(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(
            new CsrfBootstrapResponse(
                tokens.RequestToken
                ?? throw new InvalidOperationException(
                    "Antiforgery did not issue a request token.")));
    }

    private static async Task<IResult> CreatePreSessionAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAnonymousPreSessionStore preSessions,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        Guid preSessionId = await preSessions.CreateAsync(
            TimeSpan.FromMinutes(15),
            cancellationToken);
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new PreSessionResponse(preSessionId));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        LoginWorkflowResult result = await workflow.LoginAsync(
            request.Username,
            request.Password,
            request.PreSessionId,
            cancellationToken);
        return await MapLoginResultAsync(context, result);
    }

    private static async Task<IResult> CompleteMfaAsync(
        MfaLoginRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        LoginWorkflowResult result = await workflow.CompleteMfaLoginAsync(
            request.ChallengeId,
            request.PreSessionId,
            request.Code,
            cancellationToken);
        return await MapLoginResultAsync(context, result);
    }

    private static async Task<IResult> MapLoginResultAsync(
        HttpContext context,
        LoginWorkflowResult result)
    {
        if (result.Status == LoginWorkflowStatus.Invalid)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "authentication.invalid",
                type: "https://app-core.example/problems/authentication-invalid");
        }

        if (result.Status == LoginWorkflowStatus.MfaRequired)
        {
            return TypedResults.Ok(
                new LoginResponse("mfaRequired", result.MfaChallengeId));
        }

        if (result.Status == LoginWorkflowStatus.RecoveryRequired)
        {
            return TypedResults.Ok(new LoginResponse("recoveryRequired"));
        }

        await SignInSessionAsync(context, result);
        return TypedResults.Ok(new LoginResponse("authenticated"));
    }

    private static async Task SignInSessionAsync(
        HttpContext context,
        LoginWorkflowResult result)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(
                    ClaimTypes.NameIdentifier,
                    result.UserId!.Value.ToString()),
                new Claim(
                    AuthenticationSchemes.SessionIdClaim,
                    result.SessionId!.Value.ToString()),
                new Claim(
                    AuthenticationSchemes.AuthorizationVersionClaim,
                    result.AuthorizationVersion.ToString(
                        CultureInfo.InvariantCulture)),
            ],
            AuthenticationSchemes.Session);
        await context.SignInAsync(
            AuthenticationSchemes.Session,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = false,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            });
    }

    private static async Task<IResult> CompleteChallengeAsync(
        string purpose,
        ChallengeCompletionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAccountLifecycleService lifecycle,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        bool completed = await lifecycle.CompleteChallengeAsync(
            request.Username,
            purpose,
            request.Code,
            request.NewPassword,
            request.PreSessionId,
            cancellationToken);
        return completed
            ? TypedResults.Ok()
            : TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "challenge.invalid");
    }

    private static async Task<IResult> CurrentUserAsync(
        ClaimsPrincipal principal,
        IAuthenticationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                out Guid userId))
        {
            return TypedResults.Unauthorized();
        }

        CurrentUserResult? user = await workflow.GetCurrentUserAsync(
            userId,
            cancellationToken);
        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(
                new CurrentUserResponse(
                    user.UserId,
                    user.Username,
                    user.Email,
                    user.AccountStatus,
                    user.MfaState,
                    user.Permissions));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        if (!TryGetSession(
                context,
                out Guid userId,
                out Guid sessionId,
                out long authorizationVersion)
            || !await workflow.ChangePasswordAsync(
                userId,
                sessionId,
                authorizationVersion,
                request.CurrentPassword,
                request.NewPassword,
                cancellationToken))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "password.invalid");
        }

        await context.SignOutAsync(AuthenticationSchemes.Session);
        return TypedResults.Ok();
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        if (TryGetSession(context, out _, out Guid sessionId, out _))
        {
            await workflow.LogoutAsync(sessionId, cancellationToken);
        }

        await context.SignOutAsync(AuthenticationSchemes.Session);
        return TypedResults.Ok();
    }

    private static bool TryGetSession(
        HttpContext context,
        out Guid userId,
        out Guid sessionId,
        out long authorizationVersion)
    {
        userId = default;
        sessionId = default;
        authorizationVersion = default;
        return Guid.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId)
            && Guid.TryParse(
                context.User.FindFirstValue(
                    AuthenticationSchemes.SessionIdClaim),
                out sessionId)
            && long.TryParse(
                context.User.FindFirstValue(
                    AuthenticationSchemes.AuthorizationVersionClaim),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out authorizationVersion);
    }

    private static async Task<IResult> BeginMfaEnrollmentAsync(
        BeginMfaEnrollmentRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IMfaEnrollmentService enrollment,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        if (!TryGetSession(
                context,
                out Guid userId,
                out Guid sessionId,
                out long authorizationVersion))
        {
            return TypedResults.Unauthorized();
        }

        MfaEnrollmentResult? result = await enrollment.BeginEnrollmentAsync(
            userId,
            sessionId,
            authorizationVersion,
            request.CurrentPassword,
            restrictedRecovery: false,
            cancellationToken);
        SetSecretResponseHeaders(context);
        return result is null
            ? TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "mfa.enrollment.invalidState")
            : TypedResults.Ok(
                new MfaEnrollmentResponse(
                    result.AuthenticatorId,
                    result.ManualEntryKey,
                    result.ProvisioningUri));
    }

    private static async Task<IResult> BeginRecoveryAsync(
        BeginRecoveryRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        RecoveryWorkflowResult? result = await workflow.BeginRecoveryAsync(
            request.Username,
            request.Password,
            request.RecoveryCode,
            request.PreSessionId,
            cancellationToken);
        if (result is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "authentication.invalid");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
                new Claim(
                    AuthenticationSchemes.RecoverySessionIdClaim,
                    result.RecoverySessionId.ToString()),
            ],
            AuthenticationSchemes.Recovery);
        await context.SignInAsync(
            AuthenticationSchemes.Recovery,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15),
            });
        return TypedResults.Ok();
    }

    private static async Task<IResult> BeginRecoveryMfaEnrollmentAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IMfaEnrollmentService enrollment,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        if (!TryGetRecoverySession(
                context,
                out Guid userId,
                out Guid recoverySessionId))
        {
            return TypedResults.Unauthorized();
        }

        MfaEnrollmentResult? result = await enrollment.BeginEnrollmentAsync(
            userId,
            recoverySessionId,
            authorizationVersion: null,
            currentPassword: null,
            restrictedRecovery: true,
            cancellationToken);
        SetSecretResponseHeaders(context);
        return result is null
            ? TypedResults.Conflict()
            : TypedResults.Ok(
                new MfaEnrollmentResponse(
                    result.AuthenticatorId,
                    result.ManualEntryKey,
                    result.ProvisioningUri));
    }

    private static async Task<IResult> VerifyRecoveryMfaEnrollmentAsync(
        MfaEnrollmentVerificationRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IMfaEnrollmentService enrollment,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        if (!TryGetRecoverySession(
                context,
                out Guid userId,
                out Guid recoverySessionId))
        {
            return TypedResults.Unauthorized();
        }

        MfaVerificationResult? result = await enrollment.VerifyEnrollmentAsync(
            userId,
            recoverySessionId,
            authorizationVersion: null,
            restrictedRecovery: true,
            request.AuthenticatorId,
            request.Code,
            cancellationToken);
        if (result is null)
        {
            return TypedResults.BadRequest();
        }

        await context.SignOutAsync(AuthenticationSchemes.Recovery);
        SetSecretResponseHeaders(context);
        return TypedResults.Ok(
            new MfaRecoveryCodesResponse(result.RecoveryCodes));
    }

    private static async Task<IResult> LogoutRecoveryAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        if (TryGetRecoverySession(context, out _, out Guid recoverySessionId))
        {
            await workflow.LogoutRecoveryAsync(
                recoverySessionId,
                cancellationToken);
        }

        await context.SignOutAsync(AuthenticationSchemes.Recovery);
        return TypedResults.Ok();
    }

    private static bool TryGetRecoverySession(
        HttpContext context,
        out Guid userId,
        out Guid recoverySessionId)
    {
        userId = default;
        recoverySessionId = default;
        return Guid.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId)
            && Guid.TryParse(
                context.User.FindFirstValue(
                    AuthenticationSchemes.RecoverySessionIdClaim),
                out recoverySessionId);
    }

    private static async Task<IResult> VerifyMfaEnrollmentAsync(
        MfaEnrollmentVerificationRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IMfaEnrollmentService enrollment,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        if (!TryGetSession(
                context,
                out Guid userId,
                out Guid sessionId,
                out long authorizationVersion))
        {
            return TypedResults.Unauthorized();
        }

        MfaVerificationResult? result = await enrollment.VerifyEnrollmentAsync(
            userId,
            sessionId,
            authorizationVersion,
            restrictedRecovery: false,
            request.AuthenticatorId,
            request.Code,
            cancellationToken);
        SetSecretResponseHeaders(context);
        if (result is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "mfa.code.invalid");
        }

        await SignInSessionAsync(
            context,
            new LoginWorkflowResult(
                LoginWorkflowStatus.Authenticated,
                userId,
                result.SessionId,
                result.AuthorizationVersion));
        return TypedResults.Ok(
            new MfaRecoveryCodesResponse(result.RecoveryCodes));
    }

    private static void SetSecretResponseHeaders(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
}
