using acme_order.Auth;
using acme_order.Configuration;
using acme_order.Db;
using acme_order.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace acme_order
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

            // Obtén una instancia de ILoggerFactory
            var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();

            // Crea un ILogger para la clase Startup
            var logger = loggerFactory.CreateLogger<Startup>();
            
            services.Configure<AcmeServiceSettings>(
                Configuration.GetSection(nameof(AcmeServiceSettings)));

            services.AddSingleton<IAcmeServiceSettings>(sp =>
                sp.GetRequiredService<IOptions<AcmeServiceSettings>>().Value);

            //switch (Configuration["DatabaseProvider"])
            //{
            //    case "Sqlite":
            //        services.AddDbContext<OrderContext, SqliteOrderContext>(ServiceLifetime.Singleton);
            //        break;
            //    
            //    case "Postgres":
            //        services.AddDbContext<OrderContext, PostgresOrderContext>(ServiceLifetime.Singleton);
            //        break;
            //}

            var host = ReadFileContent("/bindings/db/host");
            var port = ReadFileContent("/bindings/db/port");
            var database = ReadFileContent("/bindings/db/database");
            var username = ReadFileContent("/bindings/db/username");
            var password = ReadFileContent("/bindings/db/password");

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port) ||
                string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("One or more PostgreSQL connection values are missing or invalid.");
            }

            var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
            logger.LogInformation("PostgreSQL connection string: {ConnectionString}", connectionString);

            Configuration["ConnectionStrings:OrderContext"] = connectionString;

            services.AddDbContext<OrderContext, PostgresOrderContext>(options => options.UseNpgsql(connectionString), ServiceLifetime.Singleton);

            services.AddSingleton<OrderService>();
            services.AddControllers();
            services.AddScoped<AuthorizeResource>();

            services.AddApplicationInsightsTelemetry();
            services.AddSingleton<ITelemetryInitializer, CloudRoleNameTelemetryInitializer>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }

        private string ReadFileContent(string filePath)
        {
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                return string.IsNullOrWhiteSpace(content) ? null : content;
            }

            return null;
        }

    }
}