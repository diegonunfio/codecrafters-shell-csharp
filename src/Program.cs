using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

class Program
{
    static bool runProgram = true;

    static readonly HashSet<string> builtins = new()
    {
        "exit",
        "echo",
        "type"
    };

    static void Main()
    {
        while (runProgram)
        {
            Console.Write("$ ");

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            string[] parts = input.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries
            );

            string command = parts[0];
            string[] args = parts.Skip(1).ToArray();

            switch (command)
            {
                case "exit":
                    runProgram = false;
                    break;

                case "echo":
                    Console.WriteLine(string.Join(" ", args));
                    break;

                case "type":
                    HandleType(args);
                    break;

                default:
                    Console.WriteLine($"{command}: command not found");
                    break;
            }
        }
    }

    static void HandleType(string[] args)
    {
        if (args.Length == 0)
            return;

        string command = args[0];

        // 1. Check shell builtins
        if (builtins.Contains(command))
        {
            Console.WriteLine($"{command} is a shell builtin");
            return;
        }

        // 2. Search PATH
        string? path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(path))
        {
            Console.WriteLine($"{command}: not found");
            return;
        }

        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(directory))
                continue;

            string fullPath = Path.Combine(directory, command);

            if (!File.Exists(fullPath))
                continue;

            try
            {
                UnixFileMode mode = File.GetUnixFileMode(fullPath);

                bool executable =
                    mode.HasFlag(UnixFileMode.UserExecute) ||
                    mode.HasFlag(UnixFileMode.GroupExecute) ||
                    mode.HasFlag(UnixFileMode.OtherExecute);

                if (executable)
                {
                    Console.WriteLine($"{command} is {fullPath}");
                    return;
                }
            }
            catch
            {
                // Skip files we can't inspect.
            }
        }

        // 3. Command wasn't found
        Console.WriteLine($"{command}: not found");
    }
}