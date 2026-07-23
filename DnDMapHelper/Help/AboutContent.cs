using System.Reflection;

namespace DnDMapHelper.Help;

public static class AboutContent
{
    public const string WindowTitle = "О программе";
    public const string AppName = "Карта приключений";
    public const string Tagline = "Помощник мастера для игры по карте";
    public const string DisplayVersion = "0.99beta";
    public const string Summary =
        "Карта у мастера, экран для игроков — метки, маршруты, сетка, квесты и свитки с описаниями.";
    public const string ContactLabel = "По всем вопросам:";
    public const string ContactEmail = "dndtools.lebedev@proton.me";
    public const string ContactEmailUrl = "mailto:dndtools.lebedev@proton.me";
    public const string Dedication =
        "Посвящается моему другу и прекрасному разработчику MinaSpero, без чьих идей этой программы скорее всего не существовало бы.";

    public static readonly string[] Highlights =
    [
        "Мастер готовит карту, сетку и ведёт отряд",
        "Игроки смотрят на свой экран без служебной разметки",
        "Подробности — кнопка «?» внизу"
    ];

    public static string VersionLabel
    {
        get
        {
            var info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                var label = plus >= 0 ? info[..plus] : info;
                return $"Версия {label}";
            }

            return $"Версия {DisplayVersion}";
        }
    }
}
