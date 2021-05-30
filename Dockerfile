FROM mcr.microsoft.com/dotnet/core/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 9040

FROM mcr.microsoft.com/dotnet/core/sdk:5.0 AS build
WORKDIR /src
COPY ["Qrame.Web.FileServer/Qrame.Web.FileServer.csproj", "Qrame.Web.FileServer/"]
RUN dotnet restore "Qrame.Web.FileServer/Qrame.Web.FileServer.csproj"
COPY . .
WORKDIR "/src/Qrame.Web.FileServer"
RUN dotnet build "Qrame.Web.FileServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Qrame.Web.FileServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Qrame.Web.FileServer.dll"]