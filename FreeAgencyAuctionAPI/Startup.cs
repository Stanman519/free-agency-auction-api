using System;
using FreeAgencyAuctionAPI.Hub;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RabbitMQ.Client;
using RestEase;


namespace FreeAgencyAuctionAPI
{
    public class Startup
    {
        public IConfiguration Configuration;
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(c =>
            {
                c.AddPolicy("AllowSpecificOrigin",
                    options => options.WithOrigins("https://capn-crunch-gm-bot.herokuapp.com", "https://stanfan.herokuapp.com", "http://capn-crunch-gm-bot.herokuapp.com", "http://stanfan.herokuapp.com",
                            "http://localhost:3000", "https://localhost:3000", "https://capn-crunch.herokuapp.com", "http://capn-crunch.herokuapp.com", "http://localhost:8080", "https://localhost:8080", "https://free-agency-auction.herokuapp.com")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .WithExposedHeaders("Access-Control-Allow-Origin"));
            });
            services.AddSignalR();
            services.AddControllers();
            //services.AddSingleton<IRabbitMqProducer<WinMessage>, WinProducer>();
            services.AddSingleton(_ =>
                {
                    var uri = new Uri("amqps://zafemuwu:f8cock5ulqBvvuwhzRjKX_UVkxkkWKiw@clam.rmq.cloudamqp.com/zafemuwu");
                    return new ConnectionFactory
                    {
                        Uri = uri
                    };
                });
            services.AddSingleton(serviceProvider =>
            {
                var uri = new Uri("amqps://zafemuwu:f8cock5ulqBvvuwhzRjKX_UVkxkkWKiw@clam.rmq.cloudamqp.com/zafemuwu");
                return new ConnectionFactory
                {
                    Uri = uri,
                    DispatchConsumersAsync = true
                };
            });
            services.AddSwaggerGen();
            services.AddSingleton(RestClient.For<IGMBot>("https://capn-crunch-gm-bot.herokuapp.com"));
            services.AddSingleton(RestClient.For<IGlobalMflApi>("https://api.myfantasyleague.com"));
            services.AddSingleton(RestClient.For<IMflApi>("https://www64.myfantasyleague.com"));
            services.AddSingleton(RestClient.For<IBingImageApi>("https://api.bing.microsoft.com/v7.0"));
            services.AddScoped<IPlayerServiceLayer, PlayerServiceLayer>();
            services.AddScoped<IHeadshotLoadingService, HeadshotLoadingService>();
            services.AddScoped<IOwnerServiceLayer, OwnerServiceLayer>();
            services.AddScoped<IMflService, MflService>();
            services.AddScoped<IBidLotService, BidLotService>();
            services.AddScoped<IPlayerRepo, PlayerRepo>();
            services.AddScoped<IOwnerRepo, OwnerRepo>();
            services.AddScoped<IBidLotRepo, BidLotRepo>();
            //services.AddScoped<IWinSendingSvc, WinSendingSvc>();
            services.AddAutoMapper(typeof(Startup));
            

            var databaseUrl =
                @"postgres://REDACTED_HEROKU_PG_USER:REDACTED_HEROKU_PG_PW@ec2-54-161-150-170.compute-1.amazonaws.com:5432/dacgk47k91p2vs";
            var databaseUri = new Uri(databaseUrl);
            var userInfo = databaseUri.UserInfo.Split(':');
            
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = databaseUri.Host,
                Port = databaseUri.Port,
                Username = userInfo[0],
                Password = userInfo[1],
                Database = databaseUri.LocalPath.TrimStart('/'),
                SslMode = SslMode.Require, 
                TrustServerCertificate = true
            };
            
            services.AddDbContext<AuctionContext>(
                options =>
                {
                    options.UseNpgsql(builder.ConnectionString);
                });
            
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
            app.UseCors("AllowSpecificOrigin");

            app.UseSwagger();
            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Free Agency Auction"); });
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<AuctionHub>("/auction-hub");
            });


        }
    }
}