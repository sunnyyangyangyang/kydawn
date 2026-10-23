using System.Net;
using Avalonia.Media.Imaging;
using DownKyi.Core.BiliApi.Login.Models;
using DownKyi.Core.Logging;
using DownKyi.Core.Settings;
using DownKyi.Core.Storage;
using DownKyi.Core.Utils;
using Newtonsoft.Json;
using Console = DownKyi.Core.Utils.Debugging.Console;

namespace DownKyi.Core.BiliApi.Login;

public static class LoginQr
{
    /// <summary>
    /// 申请二维码URL及扫码密钥（web端）
    /// </summary>
    /// <returns></returns>
    public static LoginUrlOrigin? GetLoginUrl()
    {
        const string getLoginUrl = "https://passport.bilibili.com/x/passport-login/web/qrcode/generate";
        var response = WebClient.RequestWeb(getLoginUrl);
        try
        {
            return JsonConvert.DeserializeObject<LoginUrlOrigin>(response);
        }
        catch (Exception e)
        {
            Console.PrintLine("GetLoginUrl()发生异常: {0}", e);
            LogManager.Error("LoginQR", e);
            return null;
        }
    }

    /// <summary>
    /// 使用扫码登录（web端）
    /// </summary>
    /// <param name="qrcodeKey"></param>
    /// <returns></returns>
    public static LoginStatus? GetLoginStatus(string qrcodeKey)
    {
        var url = $"https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key={qrcodeKey}";

        try
        {
            var cookieContainer = new CookieContainer();
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                CookieContainer = cookieContainer,
                UseCookies = true
            };
            using var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.GetInstance().GetUserAgent());
            httpClient.DefaultRequestHeaders.Add("accept-language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7");
            httpClient.DefaultRequestHeaders.Add("origin", "https://www.bilibili.com");

            using var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var loginStatus = JsonConvert.DeserializeObject<LoginStatus>(json);
            if (loginStatus?.Data != null)
            {
                loginStatus.Data.Cookies = ExtractCookies(cookieContainer);
            }

            return loginStatus;
        }
        catch (Exception e)
        {
            Console.PrintLine("GetLoginInfo()发生异常: {0}", e);
            LogManager.Error("LoginQR", e);
            return null;
        }
    }

    /// <summary>
    /// 获得登录二维码
    /// </summary>
    /// <returns></returns>
    public static Bitmap? GetLoginQrCode()
    {
        try
        {
            var loginUrl = GetLoginUrl()?.Data?.Url;
            return GetLoginQrCode(loginUrl);
        }
        catch (Exception e)
        {
            Console.PrintLine("GetLoginQrCode()发生异常: {0}", e);
            LogManager.Error("LoginQR", e);
            return null;
        }
    }

    /// <summary>
    /// 根据输入url生成二维码
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public static Bitmap? GetLoginQrCode(string? url)
    {
        if (url == null) return null;
        // 设置的参数影响app能否成功扫码
        var qrCode = QrCode.EncodeQrCode(url, 11, 10, null, 0, 0, false);

        return qrCode;
    }

    private static List<DownKyiCookie> ExtractCookies(CookieContainer cookieContainer)
    {
        var result = new List<DownKyiCookie>();
        var uris = new[]
        {
            new Uri("https://passport.bilibili.com"),
            new Uri("https://www.bilibili.com"),
            new Uri("https://api.bilibili.com"),
            new Uri("https://account.bilibili.com")
        };

        foreach (var uri in uris)
        {
            foreach (Cookie cookie in cookieContainer.GetCookies(uri))
            {
                if (string.IsNullOrWhiteSpace(cookie.Name) || string.IsNullOrWhiteSpace(cookie.Value))
                {
                    continue;
                }

                result.Add(new DownKyiCookie(
                    cookie.Name,
                    cookie.Value,
                    string.IsNullOrWhiteSpace(cookie.Domain) ? ".bilibili.com" : cookie.Domain));
            }
        }

        return result
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();
    }
}
