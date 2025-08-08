using HtmlAgilityPack;
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
public class Parser
{
    static string BaseUrl = "https://www.avito.ru/all?bt=1&cd=1";
    public List<Good> ParseGoods(string query, int count, bool isDostavka)
    {
        try
        {
            var driver = new FirefoxDriver();

            var allGoods = new List<Good>();
            for (int i = 1; i < count+1; i++)
            {
                var address = BuildRequestUrl(query, i, isDostavka);
                driver.Navigate().GoToUrl(address);
                var newGoods = ParseGoodsFromHtml(driver.PageSource);
                allGoods.AddRange(newGoods);
                Thread.Sleep(TimeSpan.FromSeconds(25));
            }

            return allGoods;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return new List<Good>();
        }
    }
    private static List<Good> ParseGoodsFromHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var goods = new List<Good>();
        var items = doc.DocumentNode.SelectNodes("//div[@data-marker='item']");

        if (items == null) return goods;

        foreach (var item in items)
        {
            var nameNode = item.SelectSingleNode(".//a[@data-marker='item-title']");
            var priceNode = item.SelectSingleNode(".//p[@data-marker='item-price']");
            var descNode = item.SelectSingleNode(".//meta[@itemprop='description']");

            if (nameNode == null || priceNode == null) continue;

            var name = nameNode.InnerText.Trim();
            var href = nameNode.GetAttributeValue("href", "");

            if (!string.IsNullOrEmpty(href) && !href.StartsWith("http"))
                href = "https://www.avito.ru" + href;

            var priceText = priceNode.InnerText;
            var digits = new string(priceText.Where(char.IsDigit).ToArray());
            int? price = int.TryParse(digits, out var p) ? p : (int?)null;

            var description = descNode?.GetAttributeValue("content", "")?.Trim() ?? "";

            goods.Add(new Good(price, name, description, href));
        }

        return goods;
    }
    private static string BuildRequestUrl(string query, int page, bool isDostavka)
    {
        var encoded = string.Join("+", query.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries));
        if (isDostavka)
        {
            return $"{BaseUrl}&d=1&p={page}&q={encoded}";
        }
        else
        {
            return $"{BaseUrl}&p={page}&q={encoded}";
        }        
    }
}