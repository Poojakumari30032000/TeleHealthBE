namespace Vitality.Models.Schedulers
{

    public class SchedulerSettings
    {

        public int IntakeReminderIntervalSeconds { get; set; } = 60;

        public int MonthlyEventIntervalHours { get; set; } = 24;

        public int RecurringPaymentIntervalHours { get; set; } = 24;

        public int? RecurringPaymentIntervalMinutes { get; set; }

        public int InvoicePdfS3UploadIntervalHours { get; set; } = 24;

        public int S3UploadMaxRetries { get; set; } = 3;

        public int S3UploadRetryBaseSeconds { get; set; } = 2;

        public int IntakeReminderLookbackDays { get; set; } = 1;

        public int RecurringMaxRetries { get; set; } = 14;

        public int[] RecurringRetryNotificationCounts { get; set; } = new[] { 1, 3, 7 };

        public int ReconciliationIntervalHours { get; set; } = 24;

        public int ProviderHoursMaterializationIntervalHours { get; set; } = 24;

        public int? ProviderHoursMaterializationIntervalMinutes { get; set; }

        public int ProviderHoursHorizonDays { get; set; } = 30;
    }
}
