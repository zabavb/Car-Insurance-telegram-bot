# Use .NET 9 SDK to build
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# Use runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0-preview
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "CarInsurance.dll"]
