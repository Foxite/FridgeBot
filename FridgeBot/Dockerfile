FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["FridgeBot/FridgeBot.csproj", "./FridgeBot/"]
COPY ["FridgeBot.Tests/FridgeBot.Tests.csproj", "./FridgeBot.Tests/"]
RUN dotnet restore "FridgeBot/FridgeBot.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "FridgeBot/FridgeBot.csproj" -c Release -o /app/build
ENV TESTDB="Host=localhost; Port=5432; Username=fridgebot; Password=test123"
RUN dotnet test
RUN dotnet publish "FridgeBot/FridgeBot.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["./FridgeBot"]
