FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy all source code and publish release binaries
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

# Automatically locate and execute the primary project DLL file
CMD ["sh", "-c", "dotnet $(ls *.dll | grep -i -v 'Microsoft\\|System\\|Swashbuckle' | head -n 1)"]