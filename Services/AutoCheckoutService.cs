using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DUANCHAMCONG.Data;

namespace DUANCHAMCONG.Services
{
    public class AutoCheckoutService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoCheckoutService> _logger;

        public AutoCheckoutService(IServiceProvider serviceProvider, ILogger<AutoCheckoutService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoCheckoutService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // Lấy các ca đang mở (chưa checkout, không bị InvalidLocation)
                        var openAttendances = dbContext.Attendances
                            .Where(a => a.CheckOutTime == null && !a.Status.Contains("InvalidLocation"))
                            .ToList();

                        bool hasChanges = false;

                        foreach (var att in openAttendances)
                        {
                            bool shouldClose = false;

                            // Nếu là ca của ngày hôm nay và đã qua 23h
                            if (att.CheckInTime.Date == now.Date && now.Hour >= 23)
                            {
                                shouldClose = true;
                            }
                            // Nếu là ca của các ngày trước đó bị treo (do tắt server lúc 23h)
                            else if (att.CheckInTime.Date < now.Date)
                            {
                                shouldClose = true;
                            }

                            if (shouldClose)
                            {
                                // Đặt giờ check-out là 23:00 của ngày hôm đó
                                att.CheckOutTime = att.CheckInTime.Date.AddHours(23);

                                // Gắn cờ ForgetCheckOut
                                if (att.Status != null && !att.Status.Contains("ForgetCheckOut"))
                                {
                                    att.Status += " - ForgetCheckOut";
                                }
                                else if (att.Status == null)
                                {
                                    att.Status = "ForgetCheckOut";
                                }

                                hasChanges = true;
                                _logger.LogInformation($"Auto-closed attendance {att.Id} for User {att.UserId} on {att.CheckInTime.Date:yyyy-MM-dd}.");
                            }
                        }

                        if (hasChanges)
                        {
                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing AutoCheckoutService.");
                }

                // Chạy ngầm quét mỗi 30 phút (1800000 ms)
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
            
            _logger.LogInformation("AutoCheckoutService is stopping.");
        }
    }
}
