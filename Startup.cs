using Amazon.KinesisFirehose;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SageMaker;
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
                    options.LoginPath = "/Landing/Login";
                });

            services.Configure<OpenIdConnectOptions>(AzureADDefaults.OpenIdScheme, options =>
            {
                options.Authority += "/v2.0/";
                options.TokenValidationParameters.ValidateIssuer = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SkipUnrecognizedRequests = true;
                options.MaxAge = TimeSpan.FromHours(1);
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
                    OnTokenResponseReceived = loginContext =>
                    {
                        var claimsIdentity = (ClaimsIdentity)loginContext.Principal.Identity;
                        AuthenticationContext authenticationContext = loginContext.HttpContext.RequestServices.GetRequiredService<AuthenticationContext>();
                        User retrieved = authenticationContext.Users.Find(claimsIdentity.FindFirst("preferred_username").Value);
                        if (retrieved == null)
                        {
                            User import = new User
                            {
                                Username = claimsIdentity.FindFirst("preferred_username").Value,
                                Name = claimsIdentity.FindFirst("name").Value,
                                Password = Password.GetRandomSalt(),
                                EmailAddress = claimsIdentity.FindFirst(ClaimTypes.Email).Value,
                                VerifiedEmail = true,
                                VerifiedPhoneNumber = false,
                                Existence = Existence.External,
                                ForceSignOut = false,
                                IDPReference = claimsIdentity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value,
                                LastPasswordChange = DateTime.Now,
                                LastAuthentication = DateTime.Now
                            };
                            foreach (var claim in claimsIdentity.Claims)
                            {
                                if (claim.Type.Equals("groups"))
                                {
                                    List<Role> retrievedRoles = authenticationContext.Roles.ToList();
                                    foreach (var retrievedRole in retrievedRoles)
                                    {
                                        if (retrievedRole.IDPReference.Equals(claim.Value))
                                            import.LinkedRole = retrievedRole;
                                    }
                                }
                            }
                            authenticationContext.Add(import);
                            authenticationContext.SaveChanges();
                        }
                        else
                        {
                            if (retrieved.IDPReference.Equals(claimsIdentity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value))
                            {
                                retrieved.ForceSignOut = false;
                                retrieved.EmailAddress = claimsIdentity.FindFirst(ClaimTypes.Email).Value;
                                retrieved.LastAuthentication = DateTime.Now;
                                foreach (var claim in claimsIdentity.Claims)
                                {
                                    if (claim.Type.Equals("groups"))
                                    {
                                        List<Role> retrievedRoles = authenticationContext.Roles.ToList();
                                        foreach (var retrievedRole in retrievedRoles)
                                        {
                                            if (retrievedRole.IDPReference.Equals(claim.Value))
                                                retrieved.LinkedRole = retrievedRole;
                                        }
                                    }
                                }
                                authenticationContext.Update(retrieved);
                                authenticationContext.SaveChanges();
                            } else
                            {
                                loginContext.Response.Redirect("/Landing/Unauthenticated?reason=mismatch");
                                loginContext.HandleResponse();
                                return Task.FromResult(0);
                            }

                        }
                        foreach (var claim in claimsIdentity.Claims.ToList())
                        {
                            if (claim.Type.Equals("groups"))
                            {
                                List<Role> retrievedRoles = authenticationContext.Roles.ToList();
                                foreach (var retrievedRole in retrievedRoles)
                                {
                                    if (retrievedRole.IDPReference.Equals(claim.Value))
                                        claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, retrievedRole.RoleName));
                                }
                            }
                        }
                        return Task.FromResult(0);
                    }
                };
            });
            services.Configure<CookieAuthenticationOptions>(AzureADDefaults.CookieScheme, options =>
            {
                options.AccessDeniedPath = "/Account/Unauthorised";
                options.LoginPath = "/Landing/Login";
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
            var SESAWSOptions = Configuration.GetAWSOptions();
            SESAWSOptions.Region = Amazon.RegionEndpoint.USEast1;
            SESAWSOptions.Credentials = new EnvironmentVariablesAWSCredentials();
            services.AddDefaultAWSOptions(defaultAWSOptions);
            //S3 Initialization
            services.AddAWSService<IAmazonS3>();
            //SagerMaker Initialization
            services.AddAWSService<IAmazonSageMaker>();
            //Kinesis Firehose Initialization
            services.AddAWSService<IAmazonKinesisFirehose>();
            //Simple Notification / Email Services Initialization
            services.AddAWSService<IAmazonSimpleNotificationService>();
            services.AddAWSService<IAmazonSimpleEmailService>(SESAWSOptions);
            //Entity Framework Initialization
            services.AddDbContext<AuthenticationContext>(options =>
            {
                options.UseLazyLoadingProxies().UseSqlServer(GetRdsConnectionString("Authentication"),
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
            //services.AddScoped<IScopedUpdateService, ScopedUpdateService>();
            //services.AddScoped<IScopedSetupService, ScopedSetupService>();
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
            app.UseStatusCodePagesWithReExecute("/Landing/Error", "?code={0}");
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
