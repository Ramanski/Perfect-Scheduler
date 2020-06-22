using Backup.Service.Helpers;
using Backup.Service.Helpers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using System.Configuration;

namespace Backup.Service
{
    public class BackupService : IHostedService
    {
        private static ILogger _logger;
        public BackupService()
        {
            SetUpNLog();
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var scheduler = await GetScheduler();
                var serviceProvider = GetConfiguredServiceProvider();
                scheduler.JobFactory = new CustomJobFactory(serviceProvider);
                await scheduler.Start();
                await ConfigureDailyJob(scheduler);
                await ConfigureMinutelyJob(scheduler);
                await ConfigureHourlyJob(scheduler);
            }
            catch (Exception ex)
            {
                _logger.Error(new CustomConfigurationException(ex.Message));
            }
        }

        private async Task ConfigureDailyJob(IScheduler scheduler)
        {
            var dailyJob = GetDailyJob();
            if (await scheduler.CheckExists(dailyJob.Key))
            {
                await scheduler.ResumeJob(dailyJob.Key);
                _logger.Info($"The job key {dailyJob.Key} was already existed, thus resuming the same");
            }
            else
            {
                await scheduler.ScheduleJob(dailyJob, GetDailyJobTrigger());
            }
        }

        private async Task ConfigureMinutelyJob(IScheduler scheduler)
        {
            var weklyJob = GetMinutelyJob();
            if (await scheduler.CheckExists(weklyJob.Key))
            {
                await scheduler.ResumeJob(weklyJob.Key);
                _logger.Info($"The job key {weklyJob.Key} was already existed, thus resuming the same");
            }
            else
            {
                await scheduler.ScheduleJob(weklyJob, GetMinutelyJobTrigger());
            }
        }

        private async Task ConfigureHourlyJob(IScheduler scheduler)
        {
            var HourlyJob = GetHourlyJob();
            if (await scheduler.CheckExists(HourlyJob.Key))
            {
                await scheduler.ResumeJob(HourlyJob.Key);
                _logger.Info($"The job key {HourlyJob.Key} was already existed, thus resuming the same");
            }
            else
            {
                await scheduler.ScheduleJob(HourlyJob, GetHourlyJobTrigger());
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        #region "Private Functions"
        private IServiceProvider GetConfiguredServiceProvider()
        {
            var services = new ServiceCollection()
                .AddScoped<IDailyJob, DailyJob>()
                .AddScoped<IMinutelyJob, MinutelyJob>()
                .AddScoped<IHourlyJob, HourlyJob>()
                .AddScoped<IHelperService, HelperService>();
            return services.BuildServiceProvider();
        }
        private IJobDetail GetDailyJob()
        {
            return JobBuilder.Create<IDailyJob>()
                .WithIdentity("dailyjob", "dailygroup")
                .Build();
        }
        private ITrigger GetDailyJobTrigger()
        {
            return TriggerBuilder.Create()
                 .WithIdentity("dailytrigger", "dailygroup")
                 .StartNow()
                 .WithSimpleSchedule(x => x
                     .WithIntervalInHours(24)
                     .RepeatForever())
                 .Build();
        }
        private IJobDetail GetMinutelyJob()
        {
            return JobBuilder.Create<IMinutelyJob>()
                .WithIdentity("Minutelyjob", "Minutelygroup")
                .Build();
        }
        private ITrigger GetMinutelyJobTrigger()
        {
            return TriggerBuilder.Create()
                 .WithIdentity("Minutelytrigger", "Minutelygroup")
                 .StartNow()
                 .WithSimpleSchedule(x => x
                     .WithIntervalInMinutes(5)
                     .RepeatForever())
                 .Build();
        }
        private IJobDetail GetHourlyJob()
        {
            return JobBuilder.Create<IHourlyJob>()
                .WithIdentity("Hourlyjob", "Hourlygroup")
                .Build();
        }
        private ITrigger GetHourlyJobTrigger()
        {
            return TriggerBuilder.Create()
                 .WithIdentity("Hourlytrigger", "Hourlygroup")
                 .StartNow()
                 .WithSimpleSchedule(x => x
                     .WithIntervalInHours(1)
                     .RepeatForever())
                 .Build();
        }
        private static async Task<IScheduler> GetScheduler()
        {
            // Comment this if you don't want to use database start
            //var config = (NameValueCollection)ConfigurationManager.GetSection("quartz");
            //var factory = new StdSchedulerFactory(config);
            // Comment this if you don't want to use database end

            // Uncomment this if you want to use RAM instead of database start
            var props = new NameValueCollection { { "quartz.serializer.type", "binary" } };
            var factory = new StdSchedulerFactory(props);
            // Uncomment this if you want to use RAM instead of database end
            var scheduler = await factory.GetScheduler();
            return scheduler;
        }
        private void SetUpNLog()
        {
            var config = new NLog.Config.LoggingConfiguration();
            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "backupclientlogfile_backupservice.txt" };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            // Rules for mapping loggers to targets            
            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logconsole);
            config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logfile);
            // Apply config           
            LogManager.Configuration = config;
            _logger = LogManager.GetCurrentClassLogger();
        }
        #endregion
    }
}
