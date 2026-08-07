using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

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
                    RunExternalCommand(command, args);
                    break;
            }
        }
    }

    static void HandleType(string[] args)
    {
        if (args.Length == 0)
            return;

        string command = args[0];

        // Primero revisar los builtins
        if (builtins.Contains(command))
        {
            Console.WriteLine($"{command} is a shell builtin");
            return;
        }

        // Después buscar en PATH
        string? executablePath = FindExecutable(command);

        if (executablePath != null)
        {
            Console.WriteLine($"{command} is {executablePath}");
            return;
        }

        Console.WriteLine($"{command}: not found");
    }

    static string? FindExecutable(string command)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(path))
            return null;

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
                    return fullPath;
            }
            catch
            {
                // Si no podemos leer los permisos,
                // seguimos buscando en PATH.
            }
        }

        return null;
    }

    static void RunExternalCommand(string command, string[] args)
    {
        string? executablePath = FindExecutable(command);

        if (executablePath == null)
        {
            Console.WriteLine($"{command}: command not found");
            return;
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false
        };

        foreach (string arg in args)
        {
            processInfo.ArgumentList.Add(arg);
        }

        using Process? process = Process.Start(processInfo);

        process?.WaitForExit();
    }
}