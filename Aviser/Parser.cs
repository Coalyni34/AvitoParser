using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using HtmlAgilityPack;
using Knapcode.TorSharp;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

public class Good
{
    public int? Cost { get; }
    public string? Name { get; }
    public string? Description { get; }
    public string? Link { get; }

    public Good(int? cost, string? name, string? description, string? link)
    {
        Cost = cost;
        Name = name;
        Description = description;
        Link = link;
    }

    public override string ToString() =>
        $"{Name} — {Cost} ₽\n{Description}\n{Link}\n";
}

public class Parser : IDisposable
{
    private const string BaseUrl = "https://www.avito.ru/all?bt=1&cd=1";
    private readonly TorSharpProxy _torProxy;
    private readonly FirefoxDriver _driver;
    private readonly Random _random = new Random();
    private readonly TorSharpSettings _settings;

    public Parser()
    {
        string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string torSharpPath = Path.Combine(homePath, ".tor-sharp");
        
        _settings = new TorSharpSettings
        {
            PrivoxySettings = { Disable = true },
            TorSettings =
            {
                SocksPort = 19050,
                ControlPort = 19051,
                ExitNodes = "{ru}",
            },
            ZippedToolsDirectory = Path.Combine(torSharpPath, "Zipped"),
            ExtractedToolsDirectory = Path.Combine(torSharpPath, "Extracted"),
            OSPlatform = TorSharpOSPlatform.Linux,
            Architecture = TorSharpArchitecture.X64
        };

        Directory.CreateDirectory(_settings.ZippedToolsDirectory);
        Directory.CreateDirectory(_settings.ExtractedToolsDirectory);

        using var httpClient = new HttpClient();
        var fetcher = new TorSharpToolFetcher(_settings, httpClient);
               
        SetExecutePermissions(_settings.ExtractedToolsDirectory);

        _torProxy = new TorSharpProxy(_settings);
        try
        {
            _torProxy.ConfigureAndStartAsync().GetAwaiter().GetResult();
            Console.WriteLine("Tor успешно запущен");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка запуска Tor: {ex.Message}");
            Console.WriteLine("Попробуйте выполнить в терминале:");
            Console.WriteLine($"sudo chmod -R 755 {_settings.ExtractedToolsDirectory}");
            throw;
        }

        var firefoxOptions = new FirefoxOptions
        {
            LogLevel = FirefoxDriverLogLevel.Fatal
        };
        
        firefoxOptions.AddArgument("--headless");
        firefoxOptions.AddArgument("--disable-gpu");
        firefoxOptions.AddArgument("--no-sandbox");
        firefoxOptions.AddArgument("--disable-dev-shm-usage");
        
        firefoxOptions.SetPreference("network.proxy.type", 1);
        firefoxOptions.SetPreference("network.proxy.socks", "127.0.0.1");
        firefoxOptions.SetPreference("network.proxy.socks_port", _settings.TorSettings.SocksPort);
        firefoxOptions.SetPreference("network.proxy.socks_version", 5);
        firefoxOptions.SetPreference("network.proxy.socks_remote_dns", true);

        var firefoxService = FirefoxDriverService.CreateDefaultService();
        firefoxService.HideCommandPromptWindow = true;
        firefoxService.SuppressInitialDiagnosticInformation = true;

        try
        {
            _driver = new FirefoxDriver(firefoxService, firefoxOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка запуска FirefoxDriver: {ex.Message}");
            Console.WriteLine("Убедитесь, что установлен Firefox:");
            Console.WriteLine("sudo apt-get install firefox-esr");
            throw;
        }
    }

    private void SetExecutePermissions(string directoryPath)
    {
        if (Environment.OSVersion.Platform != PlatformID.Unix) 
            return;

        try
        {
            var process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"chmod -R 755 '{directoryPath}'\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit();
            Console.WriteLine("Права доступа установлены");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка установки прав: {ex.Message}");
        }
    }

    public List<Good> ParseGoods(string query, int count, bool isDostavka)
    {
        try
        {
            var allGoods = new List<Good>();
            int page = 1;
            int requestCount = 0;

            while (page <= count)
            {
                if (requestCount > 0 && requestCount % 3 == 0)
                {
                    RenewTorIdentity();
                    Console.WriteLine("IP адрес изменен");
                }

                var address = BuildRequestUrl(query, page, isDostavka);
                Console.WriteLine($"Парсим страницу {page}: {address}");

                try
                {
                    _driver.Navigate().GoToUrl(address);
                    
                    if (_driver.PageSource.Contains("Доступ ограничен") || 
                        _driver.PageSource.Contains("security check"))
                    {
                        Console.WriteLine("Обнаружена блокировка! Меняем IP...");
                        RenewTorIdentity();
                        continue;
                    }
                    
                    var newGoods = ParseGoodsFromHtml(_driver.PageSource);
                    allGoods.AddRange(newGoods);
                    page++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при загрузке страницы: {ex.Message}");
                    RenewTorIdentity();
                }

                requestCount++;
                RandomDelay(20, 40);
            }

            return allGoods;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Критическая ошибка: {ex}");
            return new List<Good>();
        }
    }

    private void RenewTorIdentity()
    {
        try
        {
            _torProxy.GetNewIdentityAsync().GetAwaiter().GetResult();
            Console.WriteLine("Цепь Tor обновлена");
            RandomDelay(8, 15);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка смены IP: {ex.Message}");
        }
    }

    private void RandomDelay(int minSeconds, int maxSeconds)
    {
        int delay = _random.Next(minSeconds * 1000, maxSeconds * 1000);
        Console.WriteLine($"Задержка: {delay/1000} сек");
        Thread.Sleep(delay);
    }

    private List<Good> ParseGoodsFromHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var goods = new List<Good>();
        var items = doc.DocumentNode.SelectNodes("//div[@data-marker='item']");

        if (items == null)
        {
            Console.WriteLine("Объявления не найдены");
            return goods;
        }

        foreach (var item in items)
        {
            try
            {
                var nameNode = item.SelectSingleNode(".//h3[@itemprop='name']");
                var priceNode = item.SelectSingleNode(".//meta[@itemprop='price']");
                var descNode = item.SelectSingleNode(".//meta[@itemprop='description']");
                var linkNode = item.SelectSingleNode(".//a[@data-marker='item-title']");

                if (nameNode == null || priceNode == null || linkNode == null) continue;

                var name = nameNode.InnerText.Trim();
                var href = linkNode.GetAttributeValue("href", "");

                if (!string.IsNullOrEmpty(href) && !href.StartsWith("http"))
                    href = "https://www.avito.ru" + href;

                var priceText = priceNode.GetAttributeValue("content", "");
                var digits = new string(priceText.Where(char.IsDigit).ToArray());
                int? price = int.TryParse(digits, out var p) ? p : (int?)null;

                var description = descNode?.GetAttributeValue("content", "")?.Trim() ?? "";

                goods.Add(new Good(price, name, description, href));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка парсинга объявления: {ex.Message}");
            }
        }

        Console.WriteLine($"Найдено объявлений: {goods.Count}");
        return goods;
    }

    private static string BuildRequestUrl(string query, int page, bool isDostavka)
    {
        var encoded = Uri.EscapeDataString(query);
        if (isDostavka)
        {
            return $"{BaseUrl}&d=1&p={page}&q={encoded}";
        }
        return $"{BaseUrl}&p={page}&q={encoded}";
    }

    public void CheckCurrentIp()
    {
        try
        {
            _driver.Navigate().GoToUrl("https://api.ipify.org");
            var ip = _driver.FindElement(By.TagName("body")).Text;
            Console.WriteLine($"Текущий IP: {ip}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка проверки IP: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _driver?.Quit();
        _driver?.Dispose();
        _torProxy?.Stop();
        _torProxy?.Dispose();
    }
}

