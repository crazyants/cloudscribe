﻿// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Author:					Joe Audette
// Created:					2015-06-20
// Last Modified:			2015-08-02
// 

using System;
using System.Collections.Generic;
//using Microsoft.AspNet.Hosting;
//using JetBrains.Annotations;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Authentication;
using Microsoft.AspNet.Authentication.Cookies;
using Microsoft.AspNet.Session;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Caching;
using Microsoft.Framework.Caching.Distributed;
using Microsoft.Framework.Configuration;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Razor;
using Microsoft.AspNet.Routing;
using cloudscribe.Configuration;
using cloudscribe.Core.Models;
using cloudscribe.Core.Models.Identity;
using cloudscribe.Core.Models.Site;
using cloudscribe.Core.Models.Geography;
using cloudscribe.Core.Web;
using cloudscribe.Core.Web.Components;
using cloudscribe.Core.Web.Navigation;
using cloudscribe.Messaging;
using cloudscribe.Web.Navigation;
using cloudscribe.Core.Identity;

namespace cloudscribe.WebHost
{
    public static class CloudscribeCoreServiceCollectionExtensions
    {

        /// <summary>
        /// Setup dependency injection for cloudscribe components
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureCloudscribeCore(this IServiceCollection services, IConfiguration configuration)
        {

            services.AddCaching();
            services.AddSession();
            //services.ConfigureSession(o =>
            //{
            //    o.IdleTimeout = TimeSpan.FromSeconds(10);
            //});


            services.AddInstance<IConfiguration>(configuration);

            services.Configure<MultiTenantOptions>(configuration.GetConfigurationSection("MultiTenantOptions"));


            //*** Database platform ****************************************************************
            // here is where you could change to use one of the other db platforms
            // we have support for MySql, PostgreSql, Firebird, SQLite, and SqlCe
            // as of 2015-06-24 those can only be used in the full desktop framework (there are not yet ado.net drivers that support dnxcore50 but they should be available at some point)
            // so you would have to remove the dnxcore50 from the project.json in this project
            // add a nuget for one of the other cloudscribe.Core.Repositories.dbplatform 
            // and cloudscribe.DbHelpers.dbplatform packages
            services.TryAdd(ServiceDescriptor.Scoped<ISiteRepository, cloudscribe.Core.Repositories.MSSQL.SiteRepository>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserRepository, cloudscribe.Core.Repositories.MSSQL.UserRepository>());
            services.TryAdd(ServiceDescriptor.Scoped<IGeoRepository, cloudscribe.Core.Repositories.MSSQL.GeoRepository>());
            services.TryAdd(ServiceDescriptor.Scoped<IDb, cloudscribe.DbHelpers.MSSQL.Db>());
            //**************************************************************************************

            // RequestSiteResolver resolves ISiteSettings based on the request to support multi tenancy based on either host name or first folder depending on configuration
            services.TryAdd(ServiceDescriptor.Scoped<ISiteResolver, RequestSiteResolver>());
            services.TryAdd(ServiceDescriptor.Scoped<SiteManager, SiteManager>());
            services.TryAdd(ServiceDescriptor.Scoped<GeoDataManager, GeoDataManager>());

            // VersionProviders are used by the Setup controller to determine what install and upgrade scripts to run
            services.TryAdd(ServiceDescriptor.Scoped<IVersionProviderFactory, ConfigVersionProviderFactory>());

            services.AddCloudscribeIdentity<SiteUser, SiteRole>();
            
            

            // you can use either json or xml to maintain your navigation map we provide examples of each navigation.xml and 
            // navigation.json in the root of this project
            // you can override the name of the file used with AppSettings:NavigationXmlFileName or AppSettings:NavigationJsonFileName in config.json
            // the file must live in the root of the web project code not in wwwroot

            // it is arguable which is easier for humans to read and maintain, myself I think for something like a navigation tree
            // that could get large xml is easier to work with and not make mistakes. in json one missing or extra comma can break it
            // granted xml can be broken by typos too but the end tags make it easier to keep track of where you are imho (JA)
            //services.TryAdd(ServiceDescriptor.Scoped<INavigationTreeBuilder, JsonNavigationTreeBuilder>());
            services.TryAdd(ServiceDescriptor.Scoped<INavigationTreeBuilder, XmlNavigationTreeBuilder>());
            services.TryAdd(ServiceDescriptor.Scoped<INodeUrlPrefixProvider, FolderTenantNodeUrlPrefixProvider>()); 
            services.TryAdd(ServiceDescriptor.Scoped<INavigationNodePermissionResolver, NavigationNodePermissionResolver>());
            services.TryAdd(ServiceDescriptor.Transient<IBuildPaginationLinks, PaginationLinkBuilder>());

            services.AddTransient<IEmailSender, AuthMessageSender>();
            services.AddTransient<ISmsSender, AuthMessageSender>();

            
            // Add MVC services to the services container.
            services.AddMvc().ConfigureMvcViews(options =>
            {
                options.ViewEngines.Clear();
                // cloudscribe.Core.Web.CoreViewEngine adds /Views/Sys as the last place to search for views
                // cloudscribe views are all under Views/Sys
                // to modify a view just copy it to a higher priority location
                // ie copy /Views/Sys/Manage/*.cshtml up to /Views/Manage/ and that one will have higher priority
                // and you can modify it however you like
                // upgrading to newer versions of cloudscribe could modify or add views below /Views/Sys
                // so you may need to compare your custom views to the originals again after upgrades
                options.ViewEngines.Add(typeof(CoreViewEngine));
            });

            return services;
        }


        public static IdentityBuilder AddCloudscribeIdentity<TUser, TRole>(
            this IServiceCollection services)
            where TUser : class
            where TRole : class
        {
            //****** cloudscribe implementation of AspNet.Identity****************************************************
            services.TryAdd(ServiceDescriptor.Scoped<IUserStore<SiteUser>, UserStore<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserPasswordStore<SiteUser>, UserStore<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserEmailStore<SiteUser>, UserStore<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserLoginStore<SiteUser>, UserStore<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserRoleStore<SiteUser>, UserStore<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserClaimStore<SiteUser>, UserStore<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserPhoneNumberStore<SiteUser>, UserStore<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserLockoutStore<SiteUser>, UserStore<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserTwoFactorStore<SiteUser>, UserStore<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IRoleStore<SiteRole>, RoleStore<SiteRole>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserClaimsPrincipalFactory<SiteUser>, SiteUserClaimsPrincipalFactory<SiteUser, SiteRole>>());
            // the DNX451 desktop version of SitePasswordHasher can validate against existing hashed or encrypted passwords from mojoportal users
            // so to use existing users from mojoportal you would have to run on the desktop version at least until all users update their passwords
            // then you could migrate to dnxcore50
            // it also alllows us to create a default admin@admin.com user with administrator role with a cleartext password which would be updated 
            // to the default identity hash as soon as you change the password from its default "admin"
            services.TryAdd(ServiceDescriptor.Transient<IPasswordHasher<SiteUser>, SitePasswordHasher<SiteUser>>());

            services.TryAdd(ServiceDescriptor.Scoped<SiteUserManager<SiteUser>, SiteUserManager<SiteUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<SiteRoleManager<SiteRole>, SiteRoleManager<SiteRole>>());
            services.TryAdd(ServiceDescriptor.Scoped<SiteSignInManager<SiteUser>, SiteSignInManager<SiteUser>>());
            //services.TryAdd(ServiceDescriptor.Scoped<SignInManager<SiteUser>, SignInManager<SiteUser>>());

            //services.TryAdd(ServiceDescriptor.Scoped<ICookieAuthenticationSchemeSet, DefaultCookieAuthenticationSchemeSet>());
            //services.TryAdd(ServiceDescriptor.Scoped<ICookieAuthenticationSchemeSet, FolderTenantCookieAuthSchemeResolver>());

            services.TryAdd(ServiceDescriptor.Scoped<MultiTenantCookieOptionsResolver, MultiTenantCookieOptionsResolver>());
            services.TryAdd(ServiceDescriptor.Scoped<MultiTenantCookieOptionsResolverFactory, MultiTenantCookieOptionsResolverFactory>());
            services.TryAdd(ServiceDescriptor.Scoped<MultiTenantAuthCookieValidator, MultiTenantAuthCookieValidator>());
            services.TryAdd(ServiceDescriptor.Scoped<MultiTenantCookieAuthenticationNotifications, MultiTenantCookieAuthenticationNotifications>());
            //********************************************************************************************************

            // most of the below code was borrowed from here:
            //https://github.com/aspnet/Identity/blob/dev/src/Microsoft.AspNet.Identity/IdentityServiceCollectionExtensions.cs


            // Services used by identity
            services.AddOptions();
            services.AddAuthentication();

            // Identity services
            services.TryAdd(ServiceDescriptor.Transient<IUserValidator<TUser>, UserValidator<TUser>>());
            services.TryAdd(ServiceDescriptor.Transient<IPasswordValidator<TUser>, PasswordValidator<TUser>>());
            services.TryAdd(ServiceDescriptor.Transient<IPasswordHasher<TUser>, PasswordHasher<TUser>>());
            services.TryAdd(ServiceDescriptor.Transient<ILookupNormalizer, UpperInvariantLookupNormalizer>());
            services.TryAdd(ServiceDescriptor.Transient<IRoleValidator<TRole>, RoleValidator<TRole>>());
            services.TryAdd(ServiceDescriptor.Transient<IdentityErrorDescriber, IdentityErrorDescriber>());
            services.TryAdd(ServiceDescriptor.Scoped<ISecurityStampValidator, SecurityStampValidator<TUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<IUserClaimsPrincipalFactory<TUser>, UserClaimsPrincipalFactory<TUser, TRole>>());
            services.TryAdd(ServiceDescriptor.Scoped<UserManager<TUser>, UserManager<TUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<SignInManager<TUser>, SignInManager<TUser>>());
            services.TryAdd(ServiceDescriptor.Scoped<RoleManager<TRole>, RoleManager<TRole>>());

            // this doesn't build must be out of sync with beta6
            //services.Configure<SharedAuthenticationOptions>(options =>
            //{
            //    options.SignInScheme = IdentityOptions.ExternalCookieAuthenticationScheme;
            //});

            // Configure all of the cookie middlewares
            //services.ConfigureIdentityApplicationCookie(options =>
            //{
            //    options.AuthenticationScheme = IdentityOptions.ApplicationCookieAuthenticationScheme;
            //    options.AutomaticAuthentication = true;
            //    options.LoginPath = new PathString("/Account/Login");
            //    options.Notifications = new CookieAuthenticationNotifications
            //    {
            //        OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
            //    };
            //});


            services.ConfigureCookieAuthentication(options =>
            {
                options.AuthenticationScheme = AuthenticationScheme.Application;
                options.CookieName = AuthenticationScheme.Application;
                options.AutomaticAuthentication = true;
                options.LoginPath = new PathString("/Account/Login");
                options.Notifications = new CookieAuthenticationNotifications
                {
                    OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync
                };
            }
            , AuthenticationScheme.Application
            );

            services.ConfigureCookieAuthentication(options =>
            {
                options.AuthenticationScheme = AuthenticationScheme.External;
                options.CookieName = AuthenticationScheme.External;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            }
            , AuthenticationScheme.External);

            services.ConfigureCookieAuthentication(options =>
            {
                options.AuthenticationScheme = AuthenticationScheme.TwoFactorRememberMe;
                options.CookieName = AuthenticationScheme.TwoFactorRememberMe;
            }
            , AuthenticationScheme.TwoFactorRememberMe);

            services.ConfigureCookieAuthentication(options =>
            {
                options.AuthenticationScheme = AuthenticationScheme.TwoFactorUserId;
                options.CookieName = AuthenticationScheme.TwoFactorUserId;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            }
            , AuthenticationScheme.TwoFactorUserId);


            return new IdentityBuilder(typeof(TUser), typeof(TRole), services);
        }

    }
}
