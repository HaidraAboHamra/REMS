using Newtonsoft.Json.Bson;
using REMS.Data;
using Telegram.Bot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Telegram.Bot.Exceptions;
using MudBlazor;

namespace REMS.Services
{
    public class TelegramService
    {
        private readonly TelegramBotClient _client;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<TelegramMessageScheduler> _logger;


        public TelegramService(IConfiguration configuration, AppDbContext dbContext, ILogger<TelegramMessageScheduler> logger)
        {
            var token = configuration["TelegramBotToken"];
            _client = new TelegramBotClient(token);
            _dbContext = dbContext;
            _logger = logger;
        }

       
        public async Task SendMessagesToAllUsers(string message)
        {
            try
            {

            var users = _dbContext.Users.ToList(); 
            foreach (var user in users)
            {
                if (!string.IsNullOrEmpty(user.ChatId.ToString()))
                {
                    try {
                        await _client.SendTextMessageAsync(user.ChatId.ToString(), message);
                    }
                    catch(ChatNotFoundException e) 
                    {
                        _logger.LogInformation($"{e.Message}");
                    }
                    
                }
            }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message);
            }

        }

        public async Task SendDailyAssignedTasks(CancellationToken cancellationToken = default)
        {
            var users = await _dbContext.Users
                .AsNoTracking()
                .Where(x => x.ChatId.HasValue && x.ChatId.Value > 0)
                .ToListAsync(cancellationToken);

            var userIds = users.Select(x => x.Id).ToList();
            var tasks = await _dbContext.FollowUpReports
                .AsNoTracking()
                .Where(x =>
                    x.AssignedEmployeeId.HasValue &&
                    userIds.Contains(x.AssignedEmployeeId.Value) &&
                    !x.IsDone)
                .OrderBy(x => x.DueDate)
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                var assignedTasks = tasks
                    .Where(x => x.AssignedEmployeeId == user.Id)
                    .ToList();

                var message = assignedTasks.Count == 0
                    ? $"صباح الخير {user.FullName}. لا توجد مهام غير مكتملة مكلفة لك حاليًا."
                    : $"صباح الخير {user.FullName}.\nالمهام المكلفة لك: {assignedTasks.Count}\n\n" +
                      string.Join("\n", assignedTasks.Select((task, index) =>
                          $"{index + 1}. {task.Content}\nالحالة: {task.IsDoneOrNot ?? "لم تبدأ"}\nالموعد: {(task.DueDate?.ToString("yyyy-MM-dd") ?? "غير محدد")}"));

                try
                {
                    await _client.SendTextMessageAsync(
                        user.ChatId!.Value.ToString(),
                        message,
                        cancellationToken: cancellationToken);
                }
                catch (ChatNotFoundException exception)
                {
                    _logger.LogWarning(exception, "Telegram chat not found for user {UserId}.", user.Id);
                }
            }
        }
    }
}
