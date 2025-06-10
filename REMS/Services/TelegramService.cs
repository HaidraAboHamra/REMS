using Newtonsoft.Json.Bson;
using REMS.Data;
using Telegram.Bot;
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
    }
}
