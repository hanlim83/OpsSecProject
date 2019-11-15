using Amazon.KinesisFirehose;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SageMaker;
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
using OpsSecProject.Data;
using OpsSecProject.Policies;
using OpsSecProject.Services;
using System;
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

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddAuthentication(AzureADDefaults.AuthenticationScheme)
                .AddAzureAD(options => Configuration.Bind("AzureAd", options));

            services.Configure<OpenIdConnectOptions>(AzureADDefaults.OpenIdScheme, options =>
            {
                options.Authority += "/v2.0/";
                options.TokenValidationParameters.ValidateIssuer = false;
                options.Prompt = "select_account";
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SkipUnrecognizedRequests = true;
                options.MaxAge = TimeSpan.FromHours(3);
                options.Events = new OpenIdConnectEvents
                {
                    OnRemoteFailure = context =>
                    {
                        context.Response.Redirect("/Landing/Unauthenticated");
                        context.HandleResponse();

                        return Task.FromResult(0);
                    },
                    OnRedirectToIdentityProvider = context =>
                    {
                        if (context.ProtocolMessage.RedirectUri.StartsWith("http://"))
                            context.ProtocolMessage.RedirectUri = context.ProtocolMessage.RedirectUri.Replace("http://", "https://");
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
                        context.Response.Redirect("/Landing/Logout");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    },
                    OnRemoteSignOut = context =>
                    {
                        context.Response.Redirect("/Landing/Logout");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    }
                };
            });

            services.Configure<CookieAuthenticationOptions>(AzureADDefaults.CookieScheme, options => options.AccessDeniedPath = "/Landing/Unauthorised");

            services.AddAuthorization(options =>
            {
                options.AddPolicy(UserAuthorizationPolicy.Name,
                                  UserAuthorizationPolicy.Build);
                options.AddPolicy(PowerUserAuthorizationPolicy.Name,
                                  PowerUserAuthorizationPolicy.Build);
                options.AddPolicy(AdministratorAuthorizationPolicy.Name,
                                  AdministratorAuthorizationPolicy.Build);
            });

            services.AddSession();

            services.AddMvc(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            })
            .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            //Core AWS Initialization
            var awsOptions = Configuration.GetAWSOptions();
            awsOptions.Region = Amazon.RegionEndpoint.APSoutheast1;
            awsOptions.Credentials = new EnvironmentVariablesAWSCredentials();
            services.AddDefaultAWSOptions(awsOptions);
            //S3 Initialization
            services.AddAWSService<IAmazonS3>();
            //SagerMaker Initialization
            services.AddAWSService<IAmazonSageMaker>();
            //Kinesis Firehose Initialization
            services.AddAWSService<IAmazonKinesisFirehose>();
            //Entity Framework Initialization
            services.AddDbContext<LogDataContext>(options =>
            options.UseSqlServer(GetRdsConnectionString("LogData")));

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
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseSession();
            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Landing}/{action=Index}/{id?}");
            });
        }
        private string GetRdsConnectionString(string dbname)
        {
            string hostname = Configuration.GetValue<string>("RDS_HOSTNAME");
            string port = Configuration.GetValue<string>("RDS_PORT");
            string username = Configuration.GetValue<string>("RDS_USERNAME");
            string password = Configuration.GetValue<string>("RDS_PASSWORD");

            return $"Data Source={hostname},{port};Initial Catalog={dbname};User ID={username};Password={password};";
        }
    }
}
