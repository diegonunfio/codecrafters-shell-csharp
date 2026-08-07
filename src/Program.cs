using CodeCrafters.Shell;

class Program
{
    private static Dictionary<string, Command> commands = new();
    private static bool runProgram = true;

    static void Main()
    {
        Init();

        while (runProgram)
        {
            Console.Write("$ ");

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            List<string> inputList = input
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            string commandName = inputList[0];

            if (commands.TryGetValue(commandName, out Command? command))
            {
                command.Execute(inputList.Skip(1).ToArray());
                continue;
            }

            Console.WriteLine($"{commandName}: command not found");
        }
    }

    static void Init()
    {
        commands.Add(
            "exit",
            new Command(
                "exit",
                "Exits the program.",
                "exit",
                args =>
                {
                    runProgram = false;
                }
            )
        );

        commands.Add(
            "echo",
            new Command(
                "echo",
                "Repeats the string after echo",
                "echo <>",
                args =>
                {
                    Console.WriteLine(string.Join(" ", args));
                }
            )
        );

        commands.Add(
            "type",
            new Command(
                "type",
                "Shows whether a command is a shell builtin or executable.",
                "type <command>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        Console.WriteLine("Usage: " + commands["type"].Usage);
                        return;
                    }

                    string commandName = args[0];

                    // 1. Check builtins first
                    if (commands.ContainsKey(commandName))
                    {
                        Console.WriteLine($"{commandName} is a shell builtin");
                        return;
                    }

                    // 2. Search PATH
                    string? path = Environment.GetEnvironmentVariable("PATH");

                    if (string.IsNullOrEmpty(path))
                    {
                        Console.WriteLine($"{commandName}: not found");
                        return;
                    }

                    string[] directories = path.Split(Path.PathSeparator);

                    foreach (string directory in directories)
                    {
                        if (string.IsNullOrEmpty(directory))
                        {
                            continue;
                        }

                        string fullPath = Path.Combine(directory, commandName);

                        if (!File.Exists(fullPath))
                        {
                            continue;
                        }

                        try
                        {
                            UnixFileMode mode = File.GetUnixFileMode(fullPath);

                            bool executable =
                                mode.HasFlag(UnixFileMode.UserExecute) ||
                                mode.HasFlag(UnixFileMode.GroupExecute) ||
                                mode.HasFlag(UnixFileMode.OtherExecute);

                            if (executable)
                            {
                                Console.WriteLine(
                                    $"{commandName} is {fullPath}"
                                );
                                return;
                            }
                        }
                        catch
                        {
                            // Ignore inaccessible/non-Unix files and
                            // continue searching the next PATH directory.
                        }
                    }

                  
                    Console.WriteLine($"{commandName}: not found");
                }
            )
        );
    }
}