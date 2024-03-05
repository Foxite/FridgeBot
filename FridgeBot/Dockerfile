FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["FridgeBot/FridgeBot.csproj", "./FridgeBot/"]
COPY ["FridgeBot.Tests/FridgeBot.Tests.csproj", "./FridgeBot.Tests/"]
RUN dotnet restore "FridgeBot/FridgeBot.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "FridgeBot/FridgeBot.csproj" -c Release -o /app/build
RUN dotnet test
RUN dotnet publish "FridgeBot/FridgeBot.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS final

ARG FRIDGEBOT_VERSION

WORKDIR /app
COPY --from=build /app/publish .

ENV FRIDGEBOT_VERSION=$FRIDGEBOT_VERSION

ENTRYPOINT ["./FridgeBot"]


