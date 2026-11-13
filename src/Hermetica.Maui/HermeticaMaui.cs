#nullable enable
using System;
using System.Collections.Generic;
using Hermetica.Mobile;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Storage;

namespace Hermetica.Maui
{
    /// <summary>MAUI Preferences as the SDK's store (a private shared name, so app keys never collide).</summary>
    public sealed class PreferencesStore : IKeyValueStore
    {
        private const string Shared = "hermetica";
        public string? Get(string key) => Preferences.Default.Get<string?>(key, null, Shared);
        public void Set(string key, string value) => Preferences.Default.Set(key, value, Shared);
        public void Remove(string key) => Preferences.Default.Remove(key, Shared);
    }

    public static class HermeticaMaui
    {
        /// <summary>The client from <see cref="UseHermetica"/>.</summary>
        public static HermeticaClient Client => _client ?? throw new InvalidOperationException("Call builder.UseHermetica(...) in MauiProgram first");
        private static HermeticaClient? _client;

        /// <summary>
        /// Creates the client (also registered as a singleton), fills the device details from MAUI Essentials, and flushes the
        /// queue when the app goes to the background. <paramref name="siteKey"/> is the public site key.
        /// </summary>
        public static MauiAppBuilder UseHermetica(this MauiAppBuilder builder, string siteKey, string baseUrl, Action<HermeticaOptions>? configure = null)
        {
            var o = new HermeticaOptions
            {
                Platform = DeviceInfo.Current.Platform == DevicePlatform.iOS ? "ios" : "android",
                SdkVersion = "maui-0.4.0",
                Device = new DeviceMetadata
                {
                    DeviceModel = DeviceInfo.Current.Model, OsName = DeviceInfo.Current.Platform.ToString().ToLowerInvariant(), OsVersion = DeviceInfo.Current.VersionString,
                    AppName = AppInfo.Current.Name, AppVersion = AppInfo.Current.VersionString, AppBuild = AppInfo.Current.BuildString, AppId = AppInfo.Current.PackageName,
                },
            };
            configure?.Invoke(o);
            var client = new HermeticaClient(siteKey, baseUrl, new PreferencesStore(), o);
            _client = client;
            builder.Services.AddSingleton(client);
            builder.ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(a => a.OnStop(activity => _ = client.FlushAsync()));
#elif IOS
                events.AddiOS(i => i.DidEnterBackground(app => _ = client.FlushAsync()));
#endif
            });
            return builder;
        }

        /// <summary>Opens a push or in-app deep link with the OS. False when nothing can open it.</summary>
        public static async System.Threading.Tasks.Task<bool> OpenDeepLinkAsync(string link)
        {
            try { return await Launcher.Default.TryOpenAsync(new Uri(link)); }
            catch (UriFormatException) { return false; }
        }

        /// <summary>Converts a notification's data (Android intent extras, iOS userInfo) to the dictionary the client reads.</summary>
        public static IDictionary<string, object?> PushData(IEnumerable<KeyValuePair<string, object?>> data)
        {
            var d = new Dictionary<string, object?>();
            foreach (var kv in data) d[kv.Key] = kv.Value?.ToString();
            return d;
        }
    }
}
