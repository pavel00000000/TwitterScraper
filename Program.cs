using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class TwitterScraper
{
    static readonly string telegramBotToken = "8115116446:AAH9kLHUMRw5JInRtsnp5BpASNZiR91KbRc";
    static readonly string telegramChatId = "7949346094";
    static readonly HttpClient httpClient = new HttpClient();
    static HashSet<string> sentTweets = new HashSet<string>();
    static Dictionary<string, string> userNicknames = new Dictionary<string, string>(); // Хранит старый ник -> текущий ник
    static Dictionary<string, string> userNames = new Dictionary<string, string>(); // Хранит старое имя -> текущее имя
    static Dictionary<string, string> userBios = new Dictionary<string, string>(); // Хранит старое описание -> текущее описание
    static Random random = new Random();

    static async Task Main()
    {
        string usersFilePath = "users.txt";
        ChromeOptions options = new ChromeOptions();
        options.AddArgument("--headless");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1280x800");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        using (IWebDriver driver = new ChromeDriver(options))
        {
            List<string> users = LoadUsersFromFile(usersFilePath);

            try
            {
                // Инициализация никнеймов, имен и описания из файла при первом запуске
                foreach (var user in users)
                {
                    if (!userNicknames.ContainsKey(user))
                    {
                        userNicknames[user] = user; // Изначально старый ник = текущему
                    }
                    if (!userNames.ContainsKey(user))
                    {
                        userNames[user] = string.Empty; // Изначально имя не известно
                    }
                    if (!userBios.ContainsKey(user))
                    {
                        userBios[user] = string.Empty; // Изначально описание не известно
                    }
                }

                await LoginToTwitter(driver);

                while (true)
                {
                    users = LoadUsersFromFile(usersFilePath);

                    foreach (var user in users)
                    {
                        try
                        {
                            Console.WriteLine($"[🔍] Проверяю @{user}...");
                            await NavigateToUserPage(driver, user);
                            await RandomScroll(driver);

                            // Проверка смены никнейма
                            string currentNickname = GetCurrentNickname(driver);
                            if (!string.IsNullOrEmpty(currentNickname) && currentNickname != userNicknames[user])
                            {
                                string oldNickname = userNicknames[user];
                                userNicknames[user] = currentNickname;
                                Console.WriteLine($"[✏️] Пользователь @{oldNickname} сменил ник на @{currentNickname}");
                                await SendTelegramMessage($"Пользователь @{oldNickname} сменил ник на @{currentNickname}");
                            }

                            // Проверка смены имени (например, "Павел")
                            string currentName = GetCurrentName(driver);
                            if (!string.IsNullOrEmpty(currentName) && currentName != userNames[user])
                            {
                                string oldName = userNames[user];
                                userNames[user] = currentName;
                                Console.WriteLine($"[✏️] Пользователь @{user} сменил имя с '{oldName}' на '{currentName}'");
                                await SendTelegramMessage($"Пользователь @{user} сменил имя с '{oldName}' на '{currentName}'");
                            }

                            // Проверка изменений в разделе "О себе"
                            string currentBio = GetCurrentBio(driver);
                            if (!string.IsNullOrEmpty(currentBio) && currentBio != userBios[user])
                            {
                                string oldBio = userBios[user];
                                userBios[user] = currentBio;
                                Console.WriteLine($"[✏️] Пользователь @{user} обновил раздел 'О себе':\nСтарое: '{oldBio}'\nНовое: '{currentBio}'");
                                await SendTelegramMessage($"Пользователь @{user} обновил раздел 'О себе':\nСтарое: '{oldBio}'\nНовое: '{currentBio}'");
                            }

                            // Проверка новых твитов
                            var tweets = driver.FindElements(By.CssSelector("article div[lang]"));
                            if (tweets.Count > 0)
                            {
                                string tweetText = tweets[0].Text;
                                if (!sentTweets.Contains(tweetText))
                                {
                                    Console.WriteLine($"[📢] Новый твит от @{user}: {tweetText}");
                                    await SendTelegramMessage($"Новый твит от @{user}:\n{tweetText}\nСсылка: https://twitter.com/{user}");
                                    sentTweets.Add(tweetText);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[❌] Ошибка при обработке {user}: {ex.Message}");
                        }

                        await Task.Delay(random.Next(5000, 15000));
                    }

                    int delay = random.Next(60000, 120000);
                    Console.WriteLine($"[⏳] Ожидание {delay / 1000} секунд...");
                    await Task.Delay(delay);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[❌] Ошибка: {ex.Message}");
            }
        }
    }

    static async Task LoginToTwitter(IWebDriver driver)
    {
        driver.Navigate().GoToUrl("https://twitter.com/login");
        await Task.Delay(random.Next(2000, 4000));

        var usernameField = driver.FindElement(By.Name("text"));
        foreach (char c in "dejneka8902")
        {
            usernameField.SendKeys(c.ToString());
            await Task.Delay(random.Next(50, 150));
        }
        usernameField.SendKeys(Keys.Enter);
        await Task.Delay(random.Next(2000, 4000));

        var passwordField = driver.FindElement(By.Name("password"));
        foreach (char c in "9379992Zaq")
        {
            passwordField.SendKeys(c.ToString());
            await Task.Delay(random.Next(50, 150));
        }
        passwordField.SendKeys(Keys.Enter);
        await Task.Delay(random.Next(4000, 6000));
    }

    static async Task NavigateToUserPage(IWebDriver driver, string user)
    {
        driver.Navigate().GoToUrl($"https://twitter.com/{user}");
        await Task.Delay(random.Next(2000, 5000));
    }

    static async Task RandomScroll(IWebDriver driver)
    {
        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
        int scrollCount = random.Next(1, 4);
        for (int i = 0; i < scrollCount; i++)
        {
            js.ExecuteScript($"window.scrollBy(0, {random.Next(200, 600)});");
            await Task.Delay(random.Next(1000, 3000));
        }
    }

    static string GetCurrentNickname(IWebDriver driver)
    {
        try
        {
            var nicknameElement = driver.FindElement(By.CssSelector("div[data-testid='UserName'] span"));
            string nickname = nicknameElement.Text.Trim();
            if (nickname.StartsWith("@"))
            {
                return nickname.Substring(1);
            }
            return nickname;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    static string GetCurrentName(IWebDriver driver)
    {
        try
        {
            // Находим элемент с именем пользователя (например, "Павел" рядом с никнеймом)
            var nameElement = driver.FindElement(By.CssSelector("div[data-testid='UserName'] div[dir='ltr']"));
            string name = nameElement.Text.Trim();
            return name; // Возвращаем имя, например, "Павел"
        }
        catch (Exception)
        {
            return string.Empty; // Если не удалось найти имя, возвращаем пустую строку
        }
    }

    static string GetCurrentBio(IWebDriver driver)
    {
        try
        {
            // Находим элемент с разделом "О себе" (Bio)
            var bioElement = driver.FindElement(By.CssSelector("div[data-testid='UserDescription']"));
            string bio = bioElement.Text.Trim();
            return bio; // Возвращаем текст раздела "О себе"
        }
        catch (Exception)
        {
            return string.Empty; // Если не удалось найти описание, возвращаем пустую строку
        }
    }

    static List<string> LoadUsersFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[⚠] Файл {filePath} не найден.");
            return new List<string>();
        }

        return File.ReadAllLines(filePath).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
    }

    static async Task SendTelegramMessage(string message)
    {
        try
        {
            string url = $"https://api.telegram.org/bot{telegramBotToken}/sendMessage?chat_id={telegramChatId}&text={Uri.EscapeDataString(message)}";
            await httpClient.GetAsync(url);
            Console.WriteLine("[📩] Уведомление отправлено в Telegram.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[❌] Ошибка отправки в Telegram: {ex.Message}");
        }
    }
}