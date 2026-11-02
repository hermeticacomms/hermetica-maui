# Hermetica for .NET MAUI

Events, identity, consent, push tokens and in-app messages for .NET MAUI apps (Android and iOS, .NET 10). Events are queued on
the device (MAUI Preferences), sent in batches, retried with backoff using one `Idempotency-Key` per batch, kept while offline,
and flushed when the app goes to the background.

```sh
dotnet add package Hermeticacomms.Hermetica.Maui
```

```csharp
// MauiProgram.cs
builder.UseMauiApp<App>().UseHermetica("hm_pk_...", "https://api.example.com");

// anywhere (also injectable as HermeticaClient)
var hm = HermeticaMaui.Client;
hm.SetConsent(analytics: true, marketing: true);
hm.Identify("user-42", email: "ada@example.com", traits: new Dictionary<string, object?> { ["plan"] = "pro" });
hm.Screen("Cart");
hm.Track("added_to_cart", new Dictionary<string, object?> { ["sku"] = "A1" }, value: 1999, currency: "USD");
hm.Reset(); // logout
```

## Push

Get the token from your push plugin (Plugin.Firebase, or the native APNs / FCM APIs) and hand it over:

```csharp
await hm.SetPushTokenAsync(new PushToken(token, DeviceInfo.Platform == DevicePlatform.iOS ? "apns" : "fcm") { Environment = "release" });
var link = hm.HandlePushOpened(HermeticaMaui.PushData(notificationData)); // tracks $opened_push
if (link != null) await HermeticaMaui.OpenDeepLinkAsync(link);
await hm.UnregisterPushTokenAsync(); // logout, before Reset
```

The token follows the profile: `Identify` and `Reset` re-register it.

## In-app messages

`hm.InAppTriggered` fires on every `Screen` and `Track`. Fetch the messages for that moment, render them with your own views,
and report what happened:

```csharp
hm.InAppTriggered += async t =>
{
    var m = (await hm.FetchInAppMessagesAsync(t)).FirstOrDefault();
    if (m == null) return;
    MainThread.BeginInvokeOnMainThread(() => ShowMessage(m)); // m.Layout, m.Content["blocks"], m.Style
    await hm.ReportInAppEventAsync(m, "impression");
};
```

Targeting and frequency caps are applied by the server per session (`hm.SessionId`).

## Consent

`SetTrackingOptOut(true)` (or `SetConsent(analytics: false, ...)`) drops the queue and stops all tracking until opted back in;
requests carry `Sec-GPC: 1` meanwhile. Consent and suppression are enforced by the server too. Use the public site key only; a
secret `hm_sk_` key is refused.

The client core (`src/Hermetica.Maui/Core`) is shared with the Unity SDK and tested on plain .NET: `dotnet test tests/Hermetica.Maui.Tests`.
