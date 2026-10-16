using System.Collections.Generic;

namespace MDPro3.Utility
{
    /// <summary>Parses player launch options without changing configuration or UI state.</summary>
    public sealed class CommandLineOptions
    {
        public string Nickname { get; private set; }
        public string Host { get; private set; }
        public string Port { get; private set; }
        public string Password { get; private set; }
        public string Deck { get; private set; }
        public string Replay { get; private set; }
        public string Puzzle { get; private set; }
        public bool Join { get; private set; }

        public static CommandLineOptions Parse(IReadOnlyList<string> arguments)
        {
            var options = new CommandLineOptions();
            for (int i = 0; i < arguments.Count; i++)
            {
                switch (arguments[i]?.ToLowerInvariant())
                {
                    case "-j": options.Join = true; break;
                    case "-n": if (i + 1 < arguments.Count) options.Nickname = arguments[++i]; break;
                    case "-h": if (i + 1 < arguments.Count) options.Host = arguments[++i]; break;
                    case "-p": if (i + 1 < arguments.Count) options.Port = arguments[++i]; break;
                    case "-w": if (i + 1 < arguments.Count) options.Password = arguments[++i]; break;
                    case "-d": if (i + 1 < arguments.Count) options.Deck = arguments[++i]; break;
                    case "-r": if (i + 1 < arguments.Count) options.Replay = arguments[++i]; break;
                    case "-s": if (i + 1 < arguments.Count) options.Puzzle = arguments[++i]; break;
                }
            }
            return options;
        }
    }
}
