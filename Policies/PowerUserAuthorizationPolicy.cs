using Microsoft.AspNetCore.Authorization;

namespace OpsSecProject.Policies
{
    public static class PowerUserAuthorizationPolicy
    {
        public static string Name => "PowerUser";

        public static void Build(AuthorizationPolicyBuilder builder) =>
            builder.RequireClaim("groups", "9eb7a8cb-db12-4f3e-bbb3-f4576868b3ec");
    }
}
