using System.Reflection;

namespace DnDMapHelper.Help;

public static class AboutContent
{
    public const string WindowTitle = "О программе";
    public const string AppName = "Карта приключений";
    public const string Tagline = "Помощник мастера для игры по карте";
    public const string Summary =
        "Карта у мастера, экран для игроков — метки, маршруты, квесты и свитки с описаниями.";
    public const string ContactLabel = "По всем вопросам:";
    public const string GitHubUrl = "https://github.com/LebedevVI";
    public const string GitHubDisplay = "github.com/LebedevVI";
    public const string Dedication =
        "Посвящается моему другу и прекрасному разработчику MinaSpero, без чьих идей этой программы скорее всего не существовало бы.";

    public static readonly string[] Highlights =
    [
        "Мастер готовит карту и ведёт отряд",
        "Игроки смотрят на свой экран",
        "Подробности — кнопка «?» внизу"
    ];

    public static string VersionLabel
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "Версия 1.0" : $"Версия {version.Major}.{version.Minor}";
        }
    }
}
