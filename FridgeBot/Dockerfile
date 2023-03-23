FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["FridgeBot.csproj", "./"]
RUN dotnet restore "FridgeBot.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "FridgeBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FridgeBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./FridgeBot"]
