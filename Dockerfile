FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy file .csproj and restore packages
COPY *.csproj ./
RUN dotnet restore

# Copy toàn bộ source code vào container và build ra thư mục /out
COPY . ./
RUN dotnet publish -c Release -o out

# Chuyển sang image nhẹ hơn (chỉ chứa Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

# Chạy ứng dụng trên cổng 8080 
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DUANCHAMCONG.dll"]
