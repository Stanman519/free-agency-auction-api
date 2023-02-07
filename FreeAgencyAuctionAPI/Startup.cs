using Azure.Identity;
using FreeAgencyAuctionAPI.Hub;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestEase;
using StreamChat.Clients;


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
            var appConfig = new AppConfig();
            Configuration.Bind(appConfig);
            services.AddCors(c =>
            {
                c.AddPolicy("AllowSpecificOrigin",
                    options => options.WithOrigins("https://capn-crunch-gm-bot.herokuapp.com", 
                            "https://stanfan.herokuapp.com", 
                            "http://capn-crunch-gm-bot.herokuapp.com", 
                            "http://stanfan.herokuapp.com",
                            "http://localhost:3000", 
                            "https://localhost:3000", 
                            "https://capn-crunch.herokuapp.com", 
                            "http://capn-crunch.herokuapp.com", 
                            "http://localhost:8080", 
                            "https://localhost:8080",
                            "https://free-agency-auction.herokuapp.com")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains());

            });
            services.AddSignalR();
            services.AddControllers();
            services.AddSwaggerGen();
            services.AddSingleton(RestClient.For<IGMBot>("https://capncrunch-api.azurewebsites.net/Bot"));
            var mflGlobal = RestClient.For<IGlobalMflApi>("https://api.myfantasyleague.com");
            mflGlobal.CommishCookie = appConfig.Mfl.CommishCookie;
            services.AddSingleton(mflGlobal);
            var leagueMfl = RestClient.For<IMflApi>("https://www49.myfantasyleague.com");
            leagueMfl.CommishCookie = appConfig.Mfl.CommishCookie;
            services.AddSingleton(leagueMfl);
            services.AddSingleton(RestClient.For<ISharkApi>("https://www.fantasysharks.com/apps/Projections"));
            var bing = RestClient.For<IBingImageApi>("https://api.bing.microsoft.com/v7.0");
            bing.BingKey = appConfig.Bing.BingSubscriptionKey;
            services.AddSingleton(bing);
            services.AddScoped<IPlayerService, PlayerService>();
            services.AddScoped<IHeadshotLoadingService, HeadshotLoadingService>();
            services.AddScoped<IOwnerServiceLayer, OwnerServiceLayer>();
            services.AddScoped<IMflService, MflService>();
            services.AddScoped<IBidLotService, BidLotService>();
            services.AddScoped<IPlayerRepo, PlayerRepo>();
            services.AddScoped<IOwnerRepo, OwnerRepo>();
            services.AddScoped<IBidLotRepo, BidLotRepo>();
            //services.AddScoped<IWinSendingSvc, WinSendingSvc>();
            services.AddAutoMapper(typeof(Startup));

            services.AddAzureClients(builder =>
            {
                // Use the environment credential by default
                builder.UseCredential(new DefaultAzureCredential());
                builder.AddQueueServiceClient(appConfig.QueueConfig.AzureStorageConnectionString)
                  .ConfigureOptions(c => c.MessageEncoding = Azure.Storage.Queues.QueueMessageEncoding.Base64);
            });


            services.AddDbContext<AuctionContext>(
                options =>
                {
                    options.UseSqlServer(appConfig.SqlServerConnectionString);
                    options.UseLazyLoadingProxies();
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