# Sử dụng image chuẩn của Microsoft dành cho .NET 9 SDK để build code
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy file .csproj và restore các packages
COPY *.csproj ./
RUN dotnet restore

# Copy toàn bộ source code vào container và build ra thư mục /out
COPY . ./
RUN dotnet publish -c Release -o out

# Chuyển sang image nhẹ hơn (chỉ chứa Runtime) để chạy app (giúp tối ưu dung lượng)
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

# Chạy ứng dụng trên cổng 8080 (cổng mặc định của Docker container)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Chạy ứng dụng
ENTRYPOINT ["dotnet", "DUANCHAMCONG.dll"]
