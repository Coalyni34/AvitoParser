public class Program
{
    private static List<Good> goods;
    public static void Main(string[] args)
    {
        try
        {
            StartProgram();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
    static void StartProgram()
    {
        Console.Clear();

        string msg = "Это Авито Парсер. Выберите, что вы хотите сделать (введите нужную цифру ниже):\n"
        + "1. Спарсить товары по вашему запросу\n"
        + "2. Показать все спарсенные товары\n"
        + "3. Показать все товары в порядке возрастания цены\n"
        + "4. Показать все товары в порядке убывания цены\n"
        + "5. Узнать среднюю цену товара по рынку\n"
        + "6. Узнать медианную цену товара по рынку";
        Console.WriteLine(msg);

        short choice = Convert.ToInt16(Console.ReadLine());
        short[] choices = new short[] { 1, 2, 3, 4, 5, 6};
        if (choices.Contains(choice))
        {
            switch (choice)
            {
                case 1:
                    ParseGoods();
                    break;
                case 2:
                    WriteAllGoods();
                    break;
                case 3:
                    BigGoods();
                    break;
                case 4:
                    SmallGoods();
                    break;
                case 5:
                    MeanGoodCost();
                    break;
                case 6:
                    MedianGoodCost();
                    break;
            }
            Console.ReadLine();
            StartProgram();
        }
        else
        {
            Console.WriteLine("Некорректное значение");
        }
    }

    private static void MedianGoodCost()
    {
        var sortedAsc = goods.Where(g => g.Cost.HasValue).ToList();
        int midIndex = sortedAsc.Count / 2;

        var midCost = (sortedAsc.Count % 2 == 1) ? sortedAsc[midIndex].Cost : (sortedAsc[midIndex - 1].Cost + sortedAsc[midIndex].Cost) / 2;
        Console.WriteLine($"Медианная цена товара по рынку из списка: {midCost} рублей");
        Console.WriteLine("Нажмите Enter для продолжения");
    }

    private static void MeanGoodCost()
    {
        var sortedAsc = goods.Where(g => g.Cost.HasValue).ToList();
        double? sumCost = 0;
        foreach (var g in sortedAsc)
        {
            sumCost += g.Cost;
        }

        double? meanCost = sumCost / sortedAsc.Count;

        Console.WriteLine($"Средняя цена товара по рынку из списка: {meanCost} рублей");
        Console.WriteLine("Нажмите Enter для продолжения");
    }
    private static void SmallGoods()
    {
        var sortedAsc = goods.Where(g => g.Cost.HasValue).OrderByDescending(g => g.Cost).ToList();

        Console.WriteLine("Товары:");
        foreach (var g in sortedAsc)
            Console.WriteLine("-------------------------------------------\n" +
            $"Ссылка: {g.Link}\n" +
            $"Название: {g.Name}\n" +
            $"Цена: {g.Cost}\n" +
            $"Описание: {g.Description}\n" +
            "-------------------------------------------\n" +
            "\n");
        Console.WriteLine("Нажмите Enter для продолжения");
    }

    private static void BigGoods()
    {
        var sortedAsc = goods.Where(g => g.Cost.HasValue).OrderBy(g => g.Cost).ToList();

        Console.WriteLine("Товары:");
        foreach (var g in sortedAsc)
            Console.WriteLine("-------------------------------------------\n" +
            $"Ссылка: {g.Link}\n" +
            $"Название: {g.Name}\n" +
            $"Цена: {g.Cost}\n" +
            $"Описание: {g.Description}\n" +
            "-------------------------------------------\n" +
            "\n");
        Console.WriteLine("Нажмите Enter для продолжения");
    }

    private static void WriteAllGoods()
    {
        Console.WriteLine("Товары:");

        foreach (var good in goods)
        {
            var stringGood = "-------------------------------------------\n" +
            $"Ссылка: {good.Link}\n" +
            $"Название: {good.Name}\n" +
            $"Цена: {good.Cost}\n" +
            $"Описание: {good.Description}\n" +
            "-------------------------------------------\n" +
            "\n";

            Console.WriteLine(stringGood);
        }
        Console.WriteLine("Нажмите Enter для продолжения");
    }

    private static void ParseGoods()
    {
        Console.WriteLine("Введите свой запрос ниже:");      
        var request = Console.ReadLine();

        Console.WriteLine("Сколько страниц пропарсить?");
        var count = Convert.ToInt32(Console.ReadLine());

        Console.WriteLine("Нужна ли авито доставка? (да/нет)");
        var avidost = Console.ReadLine();
        bool isDost = false;
        switch (avidost)
        {
            case "да":
                isDost = true;
                break;
            case "нет":
                isDost = false;
                break;
            
        }

        if (request != null && count > 0)
        {
            Parser parser = new Parser();
            goods = parser.ParseGoods(request.ToLower(), count, isDost);
            Console.WriteLine($"Найдено товаров: {goods.Count}");
        }
        else
        {
            Console.WriteLine("Некорректное значение");
        }
        Console.WriteLine("Нажмите Enter для продолжения");
    }
}