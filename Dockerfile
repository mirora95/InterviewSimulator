# Use official .NET 8 SDK to build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy and restore
COPY *.sln .
COPY InterviewSimulator.Api/*.csproj InterviewSimulator.Api/
RUN dotnet restore InterviewSimulator.Api/InterviewSimulator.Api.csproj

# Build and publish
COPY . .
WORKDIR /src/InterviewSimulator.Api
RUN dotnet publish -c Release -o /app/publish

# Run stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "InterviewSimulator.Api.dll"]
