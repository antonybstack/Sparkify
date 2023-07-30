using Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Sparkify.Features.Payment;

public static class PaymentApiEndpointRouteBuilderExtensions
{
    /// <summary>
    ///     Add endpoints for registering, logging in, and logging out using ASP.NET Core Identity.
    /// </summary>
    /// <typeparam name="TUser">
    ///     The type describing the user. This should match the generic parameter in
    ///     <see cref="UserManager{TUser}" />.
    /// </typeparam>
    /// <param name="endpoints">
    ///     The <see cref="IEndpointRouteBuilder" /> to add the identity endpoints to.
    ///     Call <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, string)" /> to add a prefix to all
    ///     the endpoints.
    /// </param>
    /// <returns>An <see cref="IEndpointConventionBuilder" /> to further customize the added endpoints.</returns>
    public static IEndpointConventionBuilder MapPaymentApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder routeGroup = endpoints.MapGroup("/payment");

        routeGroup.MapGet("/", Ok () => TypedResults.Ok());

        routeGroup.MapPost("/", async Task<Results<Created, ValidationProblem>>
            ([FromBody] PaymentEvent @event, [FromServices] IDocumentStore store) =>
        {
            try
            {
                using IAsyncDocumentSession session = store.OpenAsyncSession();

                await session.StoreAsync(@event);

                await session.SaveChangesAsync();


                return TypedResults.Created();
            }
            catch (Exception e)
            {
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]> { { "Exception", new[] { e.Message } } });
            }
        });
        //
        // routeGroup.MapPost("/login",
        //     async Task<Results<UnauthorizedHttpResult, Ok<AccessTokenResponse>, SignInHttpResult>>
        //         ([FromBody] LoginRequest login, [FromQuery] bool? cookieMode, [FromServices] IServiceProvider sp) =>
        //     {
        //         UserManager<TUser> userManager = sp.GetRequiredService<UserManager<TUser>>();
        //         TUser? user = await userManager.FindByNameAsync(login.Username);
        //
        //         if (user is null || !await userManager.CheckPasswordAsync(user, login.Password))
        //         {
        //             return TypedResults.Unauthorized();
        //         }
        //
        //         IUserClaimsPrincipalFactory<TUser> claimsFactory =
        //             sp.GetRequiredService<IUserClaimsPrincipalFactory<TUser>>();
        //         ClaimsPrincipal claimsPrincipal = await claimsFactory.CreateAsync(user);
        //
        //         var useCookies = cookieMode ?? false;
        //         var scheme = useCookies ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;
        //
        //         return TypedResults.SignIn(claimsPrincipal, authenticationScheme: scheme);
        //     });
        //
        // routeGroup.MapPost("/refresh",
        //     async Task<Results<UnauthorizedHttpResult, Ok<AccessTokenResponse>, SignInHttpResult, ChallengeHttpResult>>
        //     ([FromBody] RefreshRequest refreshRequest,
        //         [FromServices] IOptionsMonitor<BearerTokenOptions> optionsMonitor,
        //         [FromServices] TimeProvider timeProvider, [FromServices] IServiceProvider sp) =>
        //     {
        //         SignInManager<TUser> signInManager = sp.GetRequiredService<SignInManager<TUser>>();
        //         BearerTokenOptions identityBearerOptions = optionsMonitor.Get(IdentityConstants.BearerScheme);
        //         ISecureDataFormat<AuthenticationTicket> refreshTokenProtector =
        //             identityBearerOptions.RefreshTokenProtector ?? throw new ArgumentException(
        //                 $"{nameof(identityBearerOptions.RefreshTokenProtector)} is null", nameof(optionsMonitor));
        //         AuthenticationTicket? refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);
        //
        //         // Reject the /refresh attempt with a 401 if the token expired or the security stamp validation fails
        //         if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
        //             timeProvider.GetUtcNow() >= expiresUtc ||
        //             await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not TUser user)
        //
        //         {
        //             return TypedResults.Challenge();
        //         }
        //
        //         ClaimsPrincipal newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
        //         return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
        //     });

        return routeGroup;
    }
}
