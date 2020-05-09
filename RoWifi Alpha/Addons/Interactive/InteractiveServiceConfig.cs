using System;

namespace RoWifi_Alpha.Addons.Interactive
{
    public class InteractiveServiceConfig
    {
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(300);
    }
}
