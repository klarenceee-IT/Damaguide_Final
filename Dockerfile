# Use the official .NET 10 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["damaguide-api.csproj", "./"]
RUN dotnet restore "damaguide-api.csproj"

# Copy remaining source code and publish
COPY . .
RUN dotnet publish "damaguide-api.csproj" -c Release -o /app/publish

# Use the ASP.NET Core runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render exposes port 10000 by default or uses the PORT environment variable
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "damaguide-api.dll"]