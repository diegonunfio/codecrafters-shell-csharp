using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

class Program
{
    static bool runProgram = true;

    static readonly HashSet<string> builtins = new()
    {
        "exit",
        "echo",
        "type",
        "pwd",
        "cd"
    };

    static void Main()
    {
        while (runProgram)
        {
            Console.Write("$ ");

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            List<string> parts = ParseInput(input);

            if (parts.Count == 0)
                continue;

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

                case "pwd":
                    Console.WriteLine(Directory.GetCurrentDirectory());
                    break;

                case "cd":
                    HandleCd(args);
                    break;

                default:
                    RunExternalCommand(command, args);
                    break;
            }
        }
    }

    static List<string> ParseInput(string input)
    {
        List<string> args = new();
        StringBuilder current = new();

        bool inSingleQuotes = false;
        bool inDoubleQuotes = false;
        bool argumentStarted = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '\\' && !inSingleQuotes && !inDoubleQuotes)
            {
                argumentStarted = true;

                if (i + 1 < input.Length)
                {
                    current.Append(input[i + 1]);
                    i++;
                }

                continue;
            }

            if (c == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                argumentStarted = true;
                continue;
            }

            if (c == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                argumentStarted = true;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inSingleQuotes && !inDoubleQuotes)
            {
                if (argumentStarted)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    argumentStarted = false;
                }

                continue;
            }

            current.Append(c);
            argumentStarted = true;
        }

        if (argumentStarted)
        {
            args.Add(current.ToString());
        }

        return args;
    }

    static void HandleCd(string[] args)
    {
        if (args.Length == 0)
            return;

        string directory = args[0];

        if (directory == "~")
        {
            string? home = Environment.GetEnvironmentVariable("HOME");

            if (string.IsNullOrEmpty(home) || !Directory.Exists(home))
            {
                Console.WriteLine($"cd: {directory}: No such file or directory");
                return;
            }

            Directory.SetCurrentDirectory(home);
            return;
        }

        string targetPath = Path.IsPathRooted(directory)
            ? directory
            : Path.Combine(Directory.GetCurrentDirectory(), directory);

        if (!Directory.Exists(targetPath))
        {
            Console.WriteLine($"cd: {directory}: No such file or directory");
            return;
        }

        Directory.SetCurrentDirectory(Path.GetFullPath(targetPath));
    }

    static void HandleType(string[] args)
    {
        if (args.Length == 0)
            return;

        string command = args[0];

        if (builtins.Contains(command))
        {
            Console.WriteLine($"{command} is a shell builtin");
            return;
        }

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
                continue;
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
            FileName = "/usr/bin/env",
            UseShellExecute = false
        };

        processInfo.ArgumentList.Add(command);

        foreach (string arg in args)
        {
            processInfo.ArgumentList.Add(arg);
        }

        using Process? process = Process.Start(processInfo);

        process?.WaitForExit();
    }
}