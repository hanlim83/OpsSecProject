using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Policies
{
    public static class AdministratorAuthorizationPolicy
    {
        public static string Name => "Administrator";

        public static void Build(AuthorizationPolicyBuilder builder) =>
            builder.RequireClaim("groups", "8325c997-07db-4297-8f1e-e2c8b506e309");
    }
}
