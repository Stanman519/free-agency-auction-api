FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FreeAgencyAuctionAPI/FreeAgencyAuctionAPI.csproj FreeAgencyAuctionAPI/
RUN dotnet restore FreeAgencyAuctionAPI/FreeAgencyAuctionAPI.csproj
COPY . .
WORKDIR /src/FreeAgencyAuctionAPI
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FreeAgencyAuctionAPI.dll"]
