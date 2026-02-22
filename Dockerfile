FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet publish src/ScreenShotRecipe.Web -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
VOLUME ["/data"]
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet","ScreenShotRecipe.Web.dll"]
