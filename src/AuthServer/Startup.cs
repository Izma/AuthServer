// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using AuthServer.Data;
using AuthServer.Models;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace AuthServer
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Store connection string as a var
            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            // Store Assembly for migrations
            var assemblyInfo = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            // Replace DbContext database from Sqlite in template to Sql Server
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                 .AddEntityFrameworkStores<ApplicationDbContext>()
                 .AddDefaultTokenProviders();

            services.AddMvc().SetCompatibilityVersion(Microsoft.AspNetCore.Mvc.CompatibilityVersion.Version_2_1);
            services.Configure<IISOptions>(iis =>
            {
                iis.AuthenticationDisplayName = "Windows";
                iis.AutomaticAuthentication = false;
            });

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
            })

            // Use our Sql Database for storing configuration data
            .AddConfigurationStore(configDb =>
            {
                configDb.ConfigureDbContext = db =>
                db.UseSqlServer(connectionString,
                sql => sql.MigrationsAssembly(assemblyInfo));
            })

            // Use our Sql Database for storing operational data
            .AddOperationalStore(operationalDb =>
            {
                operationalDb.ConfigureDbContext = db => db.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(assemblyInfo));
            })
            .AddAspNetIdentity<ApplicationUser>();

            if (Environment.IsDevelopment())
            {
                builder.AddDeveloperSigningCredential();
            }
            else
            {
                throw new Exception("need to configure key material");
            }
            services.AddAuthentication()
                .AddGoogle(options =>
                {
                    // register your IdentityServer with Google at https://console.developers.google.com
                    // enable the Google+ API
                    // set the redirect URI to http://localhost:5000/signin-google
                    options.ClientId = "910262944710-fqnk2idp2d5eja35k5pe9m4cgoa50rc0.apps.googleusercontent.com";
                    options.ClientSecret = "BWKUxo0m17a-QgwTLpaSNCMa";
                });
        }

        public void Configure(IApplicationBuilder app)
        {
            InitializedDatabase(app);
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseIdentityServer();
            app.UseMvcWithDefaultRoute();
        }

        private void InitializedDatabase(IApplicationBuilder app)
        {
            // Using a services scope
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                var persistedGrantDbContext = serviceScope.ServiceProvider
                .GetRequiredService<PersistedGrantDbContext>();
                persistedGrantDbContext.Database.Migrate();

                var configDbContext = serviceScope.ServiceProvider
                .GetRequiredService<ConfigurationDbContext>();
                configDbContext.Database.Migrate();

                if (!configDbContext.Clients.Any())
                {
                    foreach (var client in Config.GetClients())
                    {
                        configDbContext.Clients.Add(client.ToEntity());
                    }
                    configDbContext.SaveChanges();
                }
                if (!configDbContext.IdentityResources.Any())
                {
                    foreach (var resources in Config.GetIdentityResources())
                    {
                        configDbContext.IdentityResources.Add(resources.ToEntity());
                    }
                    configDbContext.SaveChanges();
                }

                if (!configDbContext.ApiResources.Any())
                {
                    foreach (var apiResources in Config.GetApis())
                    {
                        configDbContext.ApiResources.Add(apiResources.ToEntity());
                    }
                    configDbContext.SaveChanges();
                }

            }
        }
    }
}