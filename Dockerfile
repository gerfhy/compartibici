# Etapa 1: Compilación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CompartiBici.csproj", "./"]
RUN dotnet restore "CompartiBici.csproj"
COPY . .
RUN dotnet publish "CompartiBici.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Runtime optimizado
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN mkdir -p /var/data && chmod 777 /var/data
COPY --from=build /app/publish .

EXPOSE 8080
CMD ["sh", "-c", "dotnet CompartiBici.dll --urls http://0.0.0.0:${PORT:-8080}"]
