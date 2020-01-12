using Amazon.Glue;
using Amazon.KinesisAnalyticsV2;
using Amazon.KinesisFirehose;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SageMaker;
using Amazon.SageMakerRuntime;
using Amazon.SimpleEmail;
using Amazon.SimpleNotificationService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsSecProject.Areas.Internal.Data;
using OpsSecProject.Data;
using OpsSecProject.Models;
using OpsSecProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpsSecProject
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddAuthentication(AzureADDefaults.AuthenticationScheme)
                .AddAzureAD(options => Configuration.Bind("AzureAd", options))
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.AccessDeniedPath = "/Account/Unauthorised";
                    options.ExpireTimeSpan = TimeSpan.FromHours(1);
                    options.SlidingExpiration = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.EventsType = typeof(CustomCookieAuthenticationEvents);
                    options.LoginPath = "/Landing/RealmDiscovery";
                });

            services.Configure<OpenIdConnectOptions>(AzureADDefaults.OpenIdScheme, options =>
            {
                options.Authority += "/v2.0/";
                options.TokenValidationParameters.ValidateIssuer = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SkipUnrecognizedRequests = true;
                options.UseTokenLifetime = true;
                options.RemoteSignOutPath = "/single-signout";
                options.Events = new OpenIdConnectEvents
                {
                    OnRemoteFailure = context =>
                    {
                        context.Response.Redirect("/Landing/Unauthenticated");
                        context.HandleResponse();
                        return Task.FromResult(0);
                    },
                    OnAuthenticationFailed = context =>
                    {
                        context.Response.Redirect("/Landing/Unauthenticated");
                        context.HandleResponse();
                        return Task.FromResult(0);
                    },
                    OnRedirectToIdentityProvider = context =>
                    {
                        if (context.ProtocolMessage.RedirectUri.StartsWith("http://"))
                            context.ProtocolMessage.RedirectUri = context.ProtocolMessage.RedirectUri.Replace("http://", "https://");
                        if (context.Properties.Items.TryGetValue("prompt", out string prompt))
                        {
                            context.ProtocolMessage.Prompt = prompt;
                        }
                        if (context.Properties.Items.TryGetValue("login_hint", out string loginHint))
                        {
                            context.ProtocolMessage.LoginHint = loginHint;
                        }
                        return Task.CompletedTask;
                    },
                    OnRedirectToIdentityProviderForSignOut = context =>
                    {
                        if (context.ProtocolMessage.PostLogoutRedirectUri.StartsWith("http://"))
                            context.ProtocolMessage.PostLogoutRedirectUri = context.ProtocolMessage.PostLogoutRedirectUri.Replace("http://", "https://");
                        return Task.CompletedTask;
                    },
                    OnSignedOutCallbackRedirect = context =>
                    {
                        context.Response.Redirect("/Landing/Signout");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = loginContext =>
                    {
                        var claimsIdentity = (ClaimsIdentity)loginContext.Principal.Identity;
                        AccountContext accountContext = loginContext.HttpContext.RequestServices.GetRequiredService<AccountContext>();
                        User retrieved = accountContext.Users.Where(u => u.IDPReference == claimsIdentity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value).FirstOrDefault();
                        if (retrieved != null)
                        {
                            if (!retrieved.Username.Equals(claimsIdentity.FindFirst("preferred_username").Value))
                                retrieved.Username = claimsIdentity.FindFirst("preferred_username").Value;
                            retrieved.ForceSignOut = false;
                            retrieved.HybridSignIncount = 0;
                            foreach (var claim in claimsIdentity.Claims)
                            {
                                if (claim.Type.Equals("groups"))
                                {
                                    List<Role> retrievedRoles = accountContext.Roles.ToList();
                                    foreach (var retrievedRole in retrievedRoles)
                                    {
                                        if (retrievedRole.IDPReference.Equals(claim.Value))
                                            retrieved.LinkedRole = retrievedRole;
                                    }
                                }
                                else if (claim.Type.Equals(ClaimTypes.Email))
                                {
                                    retrieved.EmailAddress = claim.Value;
                                    retrieved.VerifiedEmailAddress = true;
                                }
                                else if (claim.Type.Equals(ClaimTypes.MobilePhone))
                                {
                                    retrieved.PhoneNumber = claim.Value;
                                    retrieved.VerifiedPhoneNumber = true;
                                }
                            }
                            if (retrieved.VerifiedEmailAddress && retrieved.VerifiedPhoneNumber)
                                retrieved.OverridableField = OverridableField.None;
                            else if (retrieved.VerifiedEmailAddress)
                                retrieved.OverridableField = OverridableField.PhoneNumber;
                            else if (retrieved.VerifiedPhoneNumber)
                                retrieved.OverridableField = OverridableField.EmailAddress;
                            retrieved.LastAuthentication = DateTime.Now;
                            accountContext.Update(retrieved);
                            accountContext.SaveChanges();
                        }
                        else
                        {
                            retrieved = accountContext.Users.Where(u => u.Username == claimsIdentity.FindFirst("preferred_username").Value).FirstOrDefault();
                            if (retrieved == null)
                            {
                                User import = new User
                                {
                                    Username = claimsIdentity.FindFirst("preferred_username").Value,
                                    Name = claimsIdentity.FindFirst("name").Value,
                                    Password = Password.GetRandomSalt(),
                                    Existence = Existence.External,
                                    IDPReference = claimsIdentity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value,
                                    LastPasswordChange = DateTime.Now,
                                    LastAuthentication = DateTime.Now,
                                    Status = UserStatus.Active,
                                    OverridableField = OverridableField.Both
                                };
                                foreach (var claim in claimsIdentity.Claims)
                                {
                                    if (claim.Type.Equals("groups"))
                                    {
                                        List<Role> retrievedRoles = accountContext.Roles.ToList();
                                        foreach (var retrievedRole in retrievedRoles)
                                        {
                                            if (retrievedRole.IDPReference.Equals(claim.Value))
                                                import.LinkedRole = retrievedRole;
                                        }
                                    }
                                    else if (claim.Type.Equals(ClaimTypes.Email))
                                    {
                                        import.EmailAddress = claim.Value;
                                        import.VerifiedEmailAddress = true;
                                    }
                                    else if (claim.Type.Equals(ClaimTypes.MobilePhone))
                                    {
                                        import.PhoneNumber = claim.Value;
                                        import.VerifiedPhoneNumber = true;
                                    }
                                }
                                if (import.VerifiedEmailAddress && import.VerifiedPhoneNumber)
                                    import.OverridableField = OverridableField.None;
                                else if (import.VerifiedEmailAddress)
                                    import.OverridableField = OverridableField.PhoneNumber;
                                else if (import.VerifiedPhoneNumber)
                                    import.OverridableField = OverridableField.EmailAddress;
                                accountContext.Add(import);
                                accountContext.SaveChanges();
                                import = accountContext.Users.Where(u => u.IDPReference == claimsIdentity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value).FirstOrDefault();
                                Settings settings = new Settings
                                {
                                    LinkedUserID = import.ID,
                                    LinkedUser = import
                                };
                                if (import.VerifiedEmailAddress == true)
                                    settings.CommmuicationOptions = CommmuicationOptions.EMAIL;
                                else if (import.VerifiedPhoneNumber == true || import.OverridableField == OverridableField.None)
                                    settings.CommmuicationOptions = CommmuicationOptions.SMS;
                                accountContext.Settings.Add(settings);
                                accountContext.SaveChanges();
                            }
                            else
                            {
                                loginContext.Response.Redirect("/Landing/Unauthenticated");
                                loginContext.HandleResponse();
                                return Task.FromResult(0);
                            }
                        }
                        foreach (var claim in claimsIdentity.Claims.ToList())
                        {
                            if (claim.Type.Equals("groups"))
                            {
                                List<Role> retrievedRoles = accountContext.Roles.ToList();
                                foreach (var retrievedRole in retrievedRoles)
                                {
                                    if (retrievedRole.IDPReference.Equals(claim.Value))
                                        claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, retrievedRole.RoleName));
                                }
                            }
                        }
                        if (claimsIdentity.FindFirst("http://schemas.microsoft.com/identity/claims/identityprovider") == null)
                            claimsIdentity.AddClaim(new Claim("http://schemas.microsoft.com/identity/claims/identityprovider", claimsIdentity.FindFirst("iss").Value.Remove(claimsIdentity.FindFirst("iss").Value.Length - 4)));
                        return Task.FromResult(0);
                    }
                };
            });
            services.Configure<CookieAuthenticationOptions>(AzureADDefaults.CookieScheme, options =>
            {
                options.AccessDeniedPath = "/Account/Unauthorised";
                options.LoginPath = "/Landing/RealmDiscovery";
                options.EventsType = typeof(CustomCookieAuthenticationEvents);
            });
            services.AddScoped<CustomCookieAuthenticationEvents>();

            services.AddAuthorization();

            services.AddSession();

            services.AddMvc(options =>
            {
                var policy = new AuthorizationPolicyBuilder(AzureADDefaults.AuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
                options.EnableEndpointRouting = false;
            })
            .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            //Core AWS Initialization
            var defaultAWSOptions = Configuration.GetAWSOptions();
            defaultAWSOptions.Region = Amazon.RegionEndpoint.APSoutheast1;
            defaultAWSOptions.Credentials = new EnvironmentVariablesAWSCredentials();
            services.AddDefaultAWSOptions(defaultAWSOptions);
            //S3 Initialization
            services.AddAWSService<IAmazonS3>();
            //glue Initialization
            services.AddAWSService<IAmazonGlue>();
            //SagerMaker Initialization
            services.AddAWSService<IAmazonSageMaker>();
            services.AddAWSService<IAmazonSageMakerRuntime>();
            //Kinesis Initialization
            services.AddAWSService<IAmazonKinesisAnalyticsV2>();
            services.AddAWSService<IAmazonKinesisFirehose>();
            //Simple Notification / Email Services Initialization
            services.AddAWSService<IAmazonSimpleNotificationService>();
            var SESAWSOptions = Configuration.GetAWSOptions();
            SESAWSOptions.Region = Amazon.RegionEndpoint.USEast1;
            SESAWSOptions.Credentials = new EnvironmentVariablesAWSCredentials();
            services.AddAWSService<IAmazonSimpleEmailService>(SESAWSOptions);
            //Entity Framework Initialization
            services.AddDbContext<AccountContext>(options =>
            {
                options.UseLazyLoadingProxies().UseSqlServer(GetRdsConnectionString("Account"),
                    sqlServerOptionsAction: sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 10,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });
            });
            services.AddDbContext<LogContext>(options =>
            {
                options.UseLazyLoadingProxies().UseSqlServer(GetRdsConnectionString("LogInputs"),
                    sqlServerOptionsAction: sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 10,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });
            });
          
            //Background Processing
            services.AddHostedService<ConsumeScopedServicesHostedService>();
            services.AddScoped<IScopedSetupService, ScopedSetupService>();
            services.AddHostedService<QueuedHostedService>();
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public static void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Landing/Error");
                app.UseHsts();
            }
            app.UseStatusCodePagesWithReExecute("/Landing/Error","?code={0}");
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseSession();
            app.UseAuthentication();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "areaRoute",
                    template: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Landing}/{action=Index}/{id?}");
            });
        }
        private string GetRdsConnectionString(string name)
        {
            string hostname = Configuration.GetValue<string>("RDS_HOSTNAME");
            string port = Configuration.GetValue<string>("RDS_PORT");
            string username = Configuration.GetValue<string>("RDS_USERNAME");
            string password = Configuration.GetValue<string>("RDS_PASSWORD");

            return $"Data Source={hostname},{port};Initial Catalog={name};User ID={username};Password={password};";
        }
    }
}