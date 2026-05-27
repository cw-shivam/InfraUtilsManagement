FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine
WORKDIR /app

COPY . .
RUN dotnet restore

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

CMD ["dotnet", "run", "--no-restore", "--urls", "http://+:8080"]
