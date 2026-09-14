namespace Vitality.Models.Repos.Zoom
{
    public sealed class ZoomOptions
    {
        public bool Enabled { get; set; } = true;

        public string AccountId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        public string DefaultHostEmail { get; set; } = string.Empty;

        public string DefaultTimeZone { get; set; } = "Asia/Karachi";
        public bool WaitingRoom { get; set; } = true;
        public bool JoinBeforeHost { get; set; } = false;
        public string AutoRecording { get; set; } = "none";

        public bool AutoInviteMissingProviders { get; set; } = false;
    }
}
