using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;

namespace AutoLeaseNet.Bff.Authentication;

public static class DevJwtStubExtensions
{
    /// <summary>
    /// Registers <see cref="DevJwtStubHandler"/> as an authentication scheme. Hard-fails when
    /// the host environment is Production — this scheme exists ONLY for local dev / CI tests.
    ///
    /// Wire-up in Program.cs (Development only):
    /// <code>
    /// builder.Services.AddAuthentication(DevJwtStubHandler.SchemeName)
    ///     .AddDevJwtStub(builder.Environment);
    /// </code>
    /// </summary>
    public static AuthenticationBuilder AddDevJwtStub(
        this AuthenticationBuilder builder,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                $"{nameof(DevJwtStubHandler)} must not be registered in Production. "
                + "Use Entra ID authentication instead.");
        }

        return builder.AddScheme<AuthenticationSchemeOptions, DevJwtStubHandler>(
            DevJwtStubHandler.SchemeName,
            displayName: "Development header-based stub",
            configureOptions: _ => { });
    }
}
