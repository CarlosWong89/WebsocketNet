FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# copy csproj FIRST (important for restore cache)
COPY *.csproj ./
# dotnet restore is a .NET CLI command that restores the NuGet packages required by a project or solution.
RUN dotnet restore

# copy everything else
COPY . ./

# dotnet publish -c Release -o /out is a .NET CLI command used to build and package your application for deployment.
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /out .

ENTRYPOINT ["dotnet", "WebsocketNet_JwtAuth.dll"]