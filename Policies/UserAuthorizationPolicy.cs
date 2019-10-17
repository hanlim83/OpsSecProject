using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Policies
{
    public static class UserAuthorizationPolicy
    {
        public static string Name => "User";

        public static void Build(AuthorizationPolicyBuilder builder) =>
            builder.RequireClaim("groups", "40703417-c7dd-4b0c-b260-9718d9e352fa");
    }
}
