using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

class Program
{
    static bool runProgram = true;
    static int nextJobNumber = 1;

    static readonly HashSet<string> builtins = new()
    {
        "exit",
        "echo",
        "type",
        "pwd",
        "cd",
        "complete",
        "jobs"
    };

    static readonly Dictionary<string, string> completionSpecs = new();

    static void Main()
    {
        while (runProgram)
        {
            Console.Write("$ ");

            string input = ReadInput();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            List<string> parts = ParseInput(input);

            if (parts.Count == 0)
                continue;

            bool runInBackground = false;

            if (parts.Count > 0 && parts[^1] == "&")
            {
                runInBackground = true;
                parts.RemoveAt(parts.Count - 1);
            }

            if (parts.Count == 0)
                continue;

            string? outputFile = null;
            string? errorFile = null;
            bool appendOutput = false;
            bool appendError = false;

            List<string> commandParts = new();

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == ">" || parts[i] == "1>")
                {
                    if (i + 1 < parts.Count)
                    {
                        outputFile = parts[i + 1];
                        appendOutput = false;
                        i++;
                    }

                    continue;
                }

                if (parts[i] == ">>" || parts[i] == "1>>")
                {
                    if (i + 1 < parts.Count)
                    {
                        outputFile = parts[i + 1];
                        appendOutput = true;
                        i++;
                    }

                    continue;
                }

                if (parts[i] == "2>")
                {
                    if (i + 1 < parts.Count)
                    {
                        errorFile = parts[i + 1];
                        appendError = false;
                        i++;
                    }

                    continue;
                }

                if (parts[i] == "2>>")
                {
                    if (i + 1 < parts.Count)
                    {
                        errorFile = parts[i + 1];
                        appendError = true;
                        i++;
                    }

                    continue;
                }

                commandParts.Add(parts[i]);
            }

            if (commandParts.Count == 0)
                continue;

            string command = commandParts[0];
            string[] args = commandParts.Skip(1).ToArray();

            ExecuteCommand(
                command,
                args,
                outputFile,
                errorFile,
                appendOutput,
                appendError,
                runInBackground
            );
        }
    }

    static string ReadInput()
    {
        StringBuilder input = new();

        int consecutiveTabs = 0;
        string lastTabInput = "";

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return input.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                consecutiveTabs = 0;
                lastTabInput = "";

                if (input.Length > 0)
                {
                    input.Length--;
                    Console.Write("\b \b");
                }

                continue;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                string current = input.ToString();
                int tokenStart = FindCurrentTokenStart(current);

                if (tokenStart > 0)
                {
                    string commandName = GetCommandName(current);

                    if (completionSpecs.TryGetValue(
                        commandName,
                        out string? completerPath))
                    {
                        string currentWord =
                            current.Substring(tokenStart);

                        string previousWord =
                            GetPreviousWord(current, tokenStart);

                        List<string> candidates =
                            RunCompleter(
                                completerPath,
                                commandName,
                                currentWord,
                                previousWord,
                                current
                            );

                        if (candidates.Count == 0)
                        {
                            Console.Write('\x07');
                            consecutiveTabs = 0;
                            lastTabInput = "";
                            continue;
                        }

                        if (candidates.Count == 1)
                        {
                            string candidate = candidates[0];

                            ReplaceCurrentToken(
                                input,
                                currentWord,
                                candidate
                            );

                            consecutiveTabs = 0;
                            lastTabInput = "";
                            continue;
                        }

                        string programmableCommonPrefix =
                            GetLongestCommonPrefix(candidates);

                        if (programmableCommonPrefix.Length >
                            currentWord.Length)
                        {
                            string remaining =
                                programmableCommonPrefix.Substring(
                                    currentWord.Length
                                );

                            input.Append(remaining);
                            Console.Write(remaining);

                            consecutiveTabs = 0;
                            lastTabInput = "";
                            continue;
                        }

                        if (lastTabInput == current)
                            consecutiveTabs++;
                        else
                            consecutiveTabs = 1;

                        lastTabInput = current;

                        if (consecutiveTabs == 1)
                        {
                            Console.Write('\x07');
                            continue;
                        }

                        Console.WriteLine();

                        Console.WriteLine(
                            string.Join(
                                "  ",
                                candidates
                                    .OrderBy(
                                        x => x,
                                        StringComparer.Ordinal
                                    )
                            )
                        );

                        Console.Write("$ ");
                        Console.Write(input.ToString());

                        consecutiveTabs = 0;
                        lastTabInput = "";
                        continue;
                    }

                    string partialPath =
                        current.Substring(tokenStart);

                    List<PathCompletion> matches =
                        FindPathCompletions(partialPath);

                    if (matches.Count == 0)
                    {
                        Console.Write('\x07');
                        consecutiveTabs = 0;
                        lastTabInput = "";
                        continue;
                    }

                    if (matches.Count == 1)
                    {
                        PathCompletion match = matches[0];

                        string remaining =
                            match.Value.Substring(
                                partialPath.Length
                            );

                        input.Append(remaining);
                        Console.Write(remaining);

                        if (match.IsDirectory)
                        {
                            input.Append('/');
                            Console.Write("/");
                        }
                        else
                        {
                            input.Append(' ');
                            Console.Write(" ");
                        }

                        consecutiveTabs = 0;
                        lastTabInput = "";
                        continue;
                    }

                    List<string> values = matches
                        .Select(x => x.Value)
                        .ToList();

                    string commonPrefix =
                        GetLongestCommonPrefix(values);

                    if (commonPrefix.Length > partialPath.Length)
                    {
                        string remaining =
                            commonPrefix.Substring(
                                partialPath.Length
                            );

                        input.Append(remaining);
                        Console.Write(remaining);

                        consecutiveTabs = 0;
                        lastTabInput = "";
                        continue;
                    }

                    if (lastTabInput == current)
                        consecutiveTabs++;
                    else
                        consecutiveTabs = 1;

                    lastTabInput = current;

                    if (consecutiveTabs == 1)
                    {
                        Console.Write('\x07');
                        continue;
                    }

                    Console.WriteLine();

                    Console.WriteLine(
                        string.Join(
                            "  ",
                            matches.Select(m =>
                                m.IsDirectory
                                    ? m.Value + "/"
                                    : m.Value
                            )
                        )
                    );

                    Console.Write("$ ");
                    Console.Write(input.ToString());

                    consecutiveTabs = 0;
                    lastTabInput = "";
                    continue;
                }

                List<string> commandMatches =
                    FindCommandCompletions(current);

                if (commandMatches.Count == 0)
                {
                    Console.Write('\x07');
                    consecutiveTabs = 0;
                    lastTabInput = "";
                    continue;
                }

                if (commandMatches.Count == 1)
                {
                    string match = commandMatches[0];

                    string remaining =
                        match.Substring(current.Length);

                    input.Append(remaining);
                    input.Append(' ');

                    Console.Write(remaining);
                    Console.Write(" ");

                    consecutiveTabs = 0;
                    lastTabInput = "";
                    continue;
                }

                string commandCommonPrefix =
                    GetLongestCommonPrefix(commandMatches);

                if (commandCommonPrefix.Length > current.Length)
                {
                    string remaining =
                        commandCommonPrefix.Substring(
                            current.Length
                        );

                    input.Append(remaining);
                    Console.Write(remaining);

                    consecutiveTabs = 0;
                    lastTabInput = "";
                    continue;
                }

                if (lastTabInput == current)
                    consecutiveTabs++;
                else
                    consecutiveTabs = 1;

                lastTabInput = current;

                if (consecutiveTabs == 1)
                {
                    Console.Write('\x07');
                    continue;
                }

                Console.WriteLine();

                Console.WriteLine(
                    string.Join(
                        "  ",
                        commandMatches
                    )
                );

                Console.Write("$ ");
                Console.Write(input.ToString());

                consecutiveTabs = 0;
                lastTabInput = "";
                continue;
            }

            consecutiveTabs = 0;
            lastTabInput = "";

            if (!char.IsControl(key.KeyChar))
            {
                input.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }
    }

    static void ReplaceCurrentToken(
        StringBuilder input,
        string currentWord,
        string candidate)
    {
        if (candidate.StartsWith(
            currentWord,
            StringComparison.Ordinal))
        {
            string remaining =
                candidate.Substring(currentWord.Length);

            input.Append(remaining);
            input.Append(' ');

            Console.Write(remaining);
            Console.Write(" ");

            return;
        }

        for (int i = 0; i < currentWord.Length; i++)
            Console.Write("\b \b");

        if (currentWord.Length > 0)
            input.Length -= currentWord.Length;

        input.Append(candidate);
        input.Append(' ');

        Console.Write(candidate);
        Console.Write(" ");
    }

    static string GetCommandName(string input)
    {
        int firstWhitespace = -1;

        for (int i = 0; i < input.Length; i++)
        {
            if (char.IsWhiteSpace(input[i]))
            {
                firstWhitespace = i;
                break;
            }
        }

        if (firstWhitespace == -1)
            return input;

        return input.Substring(0, firstWhitespace);
    }

    static string GetPreviousWord(
        string input,
        int currentTokenStart)
    {
        if (currentTokenStart <= 0)
            return "";

        string beforeCurrent =
            input.Substring(0, currentTokenStart)
                .TrimEnd();

        if (beforeCurrent.Length == 0)
            return "";

        List<string> words =
            SplitWords(beforeCurrent);

        if (words.Count == 0)
            return "";

        return words[^1];
    }

    static List<string> SplitWords(string input)
    {
        return input
            .Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
            )
            .ToList();
    }

    static List<string> RunCompleter(
        string completerPath,
        string commandName,
        string currentWord,
        string previousWord,
        string fullCommandLine)
    {
        List<string> candidates = new();

        try
        {
            ProcessStartInfo processInfo = new()
            {
                FileName = completerPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            processInfo.ArgumentList.Add(commandName);
            processInfo.ArgumentList.Add(currentWord);
            processInfo.ArgumentList.Add(previousWord);

            processInfo.Environment.Clear();

            processInfo.Environment["COMP_LINE"] =
                fullCommandLine;

            processInfo.Environment["COMP_POINT"] =
                Encoding.UTF8
                    .GetByteCount(fullCommandLine)
                    .ToString();

            using Process? process =
                Process.Start(processInfo);

            if (process == null)
                return candidates;

            string output =
                process.StandardOutput.ReadToEnd();

            process.StandardError.ReadToEnd();

            process.WaitForExit();

            candidates = output
                .Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Where(x =>
                    x.StartsWith(
                        currentWord,
                        StringComparison.Ordinal
                    )
                )
                .Distinct(StringComparer.Ordinal)
                .OrderBy(
                    x => x,
                    StringComparer.Ordinal
                )
                .ToList();
        }
        catch
        {
        }

        return candidates;
    }

    static int FindCurrentTokenStart(string input)
    {
        for (int i = input.Length - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(input[i]))
                return i + 1;
        }

        return 0;
    }

    class PathCompletion
    {
        public string Value { get; set; } = "";
        public bool IsDirectory { get; set; }
    }

    static List<PathCompletion> FindPathCompletions(
        string partialPath)
    {
        List<PathCompletion> matches = new();

        string directoryPart;
        string prefix;

        int lastSlash = partialPath.LastIndexOf('/');

        if (lastSlash >= 0)
        {
            directoryPart =
                partialPath.Substring(
                    0,
                    lastSlash + 1
                );

            prefix =
                partialPath.Substring(
                    lastSlash + 1
                );
        }
        else
        {
            directoryPart = "";
            prefix = partialPath;
        }

        string searchDirectory;

        if (string.IsNullOrEmpty(directoryPart))
        {
            searchDirectory =
                Directory.GetCurrentDirectory();
        }
        else if (Path.IsPathRooted(directoryPart))
        {
            searchDirectory = directoryPart;
        }
        else
        {
            searchDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                directoryPart
            );
        }

        if (!Directory.Exists(searchDirectory))
            return matches;

        try
        {
            foreach (
                string entry in
                Directory.EnumerateFileSystemEntries(
                    searchDirectory
                )
            )
            {
                string trimmed =
                    entry.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    );

                string name =
                    Path.GetFileName(trimmed);

                if (!name.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                bool isDirectory =
                    Directory.Exists(entry);

                string completedPath =
                    directoryPart + name;

                matches.Add(
                    new PathCompletion
                    {
                        Value = completedPath,
                        IsDirectory = isDirectory
                    }
                );
            }
        }
        catch
        {
        }

        return matches
            .OrderBy(
                x => x.Value,
                StringComparer.Ordinal
            )
            .ToList();
    }

    static List<string> FindCommandCompletions(
        string prefix)
    {
        HashSet<string> matches =
            new(StringComparer.Ordinal);

        foreach (string builtin in builtins)
        {
            if (builtin.StartsWith(
                prefix,
                StringComparison.Ordinal))
            {
                matches.Add(builtin);
            }
        }

        string? path =
            Environment.GetEnvironmentVariable("PATH");

        if (!string.IsNullOrEmpty(path))
        {
            foreach (
                string directory in
                path.Split(Path.PathSeparator)
            )
            {
                if (string.IsNullOrEmpty(directory))
                    continue;

                if (!Directory.Exists(directory))
                    continue;

                try
                {
                    foreach (
                        string file in
                        Directory.EnumerateFiles(directory)
                    )
                    {
                        string name =
                            Path.GetFileName(file);

                        if (!name.StartsWith(
                            prefix,
                            StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (IsExecutable(file))
                            matches.Add(name);
                    }
                }
                catch
                {
                }
            }
        }

        return matches
            .OrderBy(
                x => x,
                StringComparer.Ordinal
            )
            .ToList();
    }

    static string GetLongestCommonPrefix(
        List<string> values)
    {
        if (values.Count == 0)
            return "";

        string prefix = values[0];

        for (int i = 1; i < values.Count; i++)
        {
            int length =
                Math.Min(
                    prefix.Length,
                    values[i].Length
                );

            int j = 0;

            while (
                j < length &&
                prefix[j] == values[i][j]
            )
            {
                j++;
            }

            prefix =
                prefix.Substring(0, j);

            if (prefix.Length == 0)
                break;
        }

        return prefix;
    }

    static bool IsExecutable(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            UnixFileMode mode =
                File.GetUnixFileMode(filePath);

            return
                mode.HasFlag(
                    UnixFileMode.UserExecute
                ) ||
                mode.HasFlag(
                    UnixFileMode.GroupExecute
                ) ||
                mode.HasFlag(
                    UnixFileMode.OtherExecute
                );
        }
        catch
        {
            return false;
        }
    }

    static void ExecuteCommand(
        string command,
        string[] args,
        string? outputFile,
        string? errorFile,
        bool appendOutput,
        bool appendError,
        bool runInBackground)
    {
        switch (command)
        {
            case "exit":
                runProgram = false;
                break;

            case "echo":
                WriteOutput(
                    string.Join(" ", args) +
                    Environment.NewLine,
                    outputFile,
                    appendOutput
                );

                PrepareErrorFile(
                    errorFile,
                    appendError
                );
                break;

            case "type":
                HandleType(
                    args,
                    outputFile,
                    appendOutput
                );

                PrepareErrorFile(
                    errorFile,
                    appendError
                );
                break;

            case "pwd":
                WriteOutput(
                    Directory.GetCurrentDirectory() +
                    Environment.NewLine,
                    outputFile,
                    appendOutput
                );

                PrepareErrorFile(
                    errorFile,
                    appendError
                );
                break;

            case "cd":
                HandleCd(
                    args,
                    errorFile,
                    appendError
                );
                break;

            case "complete":
                HandleComplete(
                    args,
                    outputFile,
                    appendOutput
                );

                PrepareErrorFile(
                    errorFile,
                    appendError
                );
                break;

            case "jobs":
                PrepareOutputFile(
                    outputFile,
                    appendOutput
                );

                PrepareErrorFile(
                    errorFile,
                    appendError
                );
                break;

            default:
                RunExternalCommand(
                    command,
                    args,
                    outputFile,
                    errorFile,
                    appendOutput,
                    appendError,
                    runInBackground
                );
                break;
        }
    }

    static void HandleComplete(
        string[] args,
        string? outputFile,
        bool appendOutput)
    {
        if (args.Length >= 3 && args[0] == "-C")
        {
            string completerPath = args[1];
            string commandName = args[2];

            completionSpecs[commandName] =
                completerPath;

            return;
        }

        if (args.Length >= 2 && args[0] == "-r")
        {
            string commandName = args[1];

            completionSpecs.Remove(commandName);

            return;
        }

        if (args.Length >= 2 && args[0] == "-p")
        {
            string commandName = args[1];

            if (completionSpecs.TryGetValue(
                commandName,
                out string? completerPath))
            {
                WriteOutput(
                    $"complete -C '{completerPath}' {commandName}{Environment.NewLine}",
                    outputFile,
                    appendOutput
                );

                return;
            }

            WriteOutput(
                $"complete: {commandName}: no completion specification{Environment.NewLine}",
                outputFile,
                appendOutput
            );

            return;
        }
    }

    static void WriteOutput(
        string text,
        string? outputFile,
        bool append)
    {
        if (outputFile == null)
        {
            Console.Write(text);
            return;
        }

        if (append)
            File.AppendAllText(outputFile, text);
        else
            File.WriteAllText(outputFile, text);
    }

    static void WriteError(
        string text,
        string? errorFile,
        bool append)
    {
        if (errorFile == null)
        {
            Console.Error.Write(text);
            return;
        }

        if (append)
            File.AppendAllText(errorFile, text);
        else
            File.WriteAllText(errorFile, text);
    }

    static void PrepareOutputFile(
        string? outputFile,
        bool appendOutput)
    {
        if (outputFile == null)
            return;

        if (appendOutput)
        {
            if (!File.Exists(outputFile))
                File.WriteAllText(outputFile, "");
        }
        else
        {
            File.WriteAllText(outputFile, "");
        }
    }

    static void PrepareErrorFile(
        string? errorFile,
        bool appendError)
    {
        if (errorFile == null)
            return;

        if (appendError)
        {
            if (!File.Exists(errorFile))
                File.WriteAllText(errorFile, "");
        }
        else
        {
            File.WriteAllText(errorFile, "");
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

            if (inSingleQuotes)
            {
                if (c == '\'')
                {
                    inSingleQuotes = false;
                }
                else
                {
                    current.Append(c);
                }

                argumentStarted = true;
                continue;
            }

            if (inDoubleQuotes)
            {
                if (c == '"')
                {
                    inDoubleQuotes = false;
                    argumentStarted = true;
                    continue;
                }

                if (c == '\\')
                {
                    if (i + 1 < input.Length)
                    {
                        char next =
                            input[i + 1];

                        if (
                            next == '"' ||
                            next == '\\'
                        )
                        {
                            current.Append(next);
                            i++;
                        }
                        else
                        {
                            current.Append('\\');
                        }
                    }
                    else
                    {
                        current.Append('\\');
                    }

                    argumentStarted = true;
                    continue;
                }

                current.Append(c);
                argumentStarted = true;
                continue;
            }

            if (c == '\\')
            {
                argumentStarted = true;

                if (i + 1 < input.Length)
                {
                    current.Append(
                        input[i + 1]
                    );

                    i++;
                }

                continue;
            }

            if (c == '\'')
            {
                inSingleQuotes = true;
                argumentStarted = true;
                continue;
            }

            if (c == '"')
            {
                inDoubleQuotes = true;
                argumentStarted = true;
                continue;
            }

            if (c == '>')
            {
                string prefix =
                    current.ToString();

                bool isAppend =
                    i + 1 < input.Length &&
                    input[i + 1] == '>';

                if (isAppend)
                {
                    i++;

                    if (prefix == "1")
                    {
                        args.Add("1>>");
                    }
                    else if (prefix == "2")
                    {
                        args.Add("2>>");
                    }
                    else
                    {
                        if (argumentStarted)
                            args.Add(prefix);

                        args.Add(">>");
                    }
                }
                else
                {
                    if (prefix == "1")
                    {
                        args.Add("1>");
                    }
                    else if (prefix == "2")
                    {
                        args.Add("2>");
                    }
                    else
                    {
                        if (argumentStarted)
                            args.Add(prefix);

                        args.Add(">");
                    }
                }

                current.Clear();
                argumentStarted = false;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (argumentStarted)
                {
                    args.Add(
                        current.ToString()
                    );

                    current.Clear();
                    argumentStarted = false;
                }

                continue;
            }

            current.Append(c);
            argumentStarted = true;
        }

        if (argumentStarted)
            args.Add(current.ToString());

        return args;
    }

    static void HandleCd(
        string[] args,
        string? errorFile,
        bool appendError)
    {
        if (args.Length == 0)
        {
            PrepareErrorFile(
                errorFile,
                appendError
            );

            return;
        }

        string directory = args[0];

        if (directory == "~")
        {
            string? home =
                Environment.GetEnvironmentVariable(
                    "HOME"
                );

            if (
                string.IsNullOrEmpty(home) ||
                !Directory.Exists(home)
            )
            {
                WriteError(
                    $"cd: {directory}: No such file or directory{Environment.NewLine}",
                    errorFile,
                    appendError
                );

                return;
            }

            Directory.SetCurrentDirectory(home);

            PrepareErrorFile(
                errorFile,
                appendError
            );

            return;
        }

        string targetPath =
            Path.IsPathRooted(directory)
                ? directory
                : Path.Combine(
                    Directory.GetCurrentDirectory(),
                    directory
                );

        if (!Directory.Exists(targetPath))
        {
            WriteError(
                $"cd: {directory}: No such file or directory{Environment.NewLine}",
                errorFile,
                appendError
            );

            return;
        }

        Directory.SetCurrentDirectory(
            Path.GetFullPath(targetPath)
        );

        PrepareErrorFile(
            errorFile,
            appendError
        );
    }

    static void HandleType(
        string[] args,
        string? outputFile,
        bool appendOutput)
    {
        if (args.Length == 0)
            return;

        string command = args[0];

        if (builtins.Contains(command))
        {
            WriteOutput(
                $"{command} is a shell builtin{Environment.NewLine}",
                outputFile,
                appendOutput
            );

            return;
        }

        string? executablePath =
            FindExecutable(command);

        if (executablePath != null)
        {
            WriteOutput(
                $"{command} is {executablePath}{Environment.NewLine}",
                outputFile,
                appendOutput
            );

            return;
        }

        WriteOutput(
            $"{command}: not found{Environment.NewLine}",
            outputFile,
            appendOutput
        );
    }

    static string? FindExecutable(
        string command)
    {
        string? path =
            Environment.GetEnvironmentVariable(
                "PATH"
            );

        if (string.IsNullOrEmpty(path))
            return null;

        foreach (
            string directory in
            path.Split(Path.PathSeparator)
        )
        {
            if (string.IsNullOrEmpty(directory))
                continue;

            string fullPath =
                Path.Combine(
                    directory,
                    command
                );

            if (IsExecutable(fullPath))
                return fullPath;
        }

        return null;
    }

    static void RunExternalCommand(
        string command,
        string[] args,
        string? outputFile,
        string? errorFile,
        bool appendOutput,
        bool appendError,
        bool runInBackground)
    {
        string? executablePath =
            FindExecutable(command);

        if (executablePath == null)
        {
            WriteError(
                $"{command}: command not found{Environment.NewLine}",
                errorFile,
                appendError
            );

            return;
        }

        ProcessStartInfo processInfo = new()
        {
            FileName = "/usr/bin/env",
            UseShellExecute = false,

            // If there is no shell redirection, leave these false.
            // The child then inherits the shell's terminal stdout/stderr.
            RedirectStandardOutput = outputFile != null,
            RedirectStandardError = errorFile != null
        };

        processInfo.ArgumentList.Add(command);

        foreach (string arg in args)
            processInfo.ArgumentList.Add(arg);

        Process? process = Process.Start(processInfo);

        if (process == null)
            return;

        if (runInBackground)
        {
            int jobNumber = nextJobNumber++;

            Console.WriteLine(
                $"[{jobNumber}] {process.Id}"
            );

            if (outputFile == null &&
                errorFile == null)
            {
                // Do not wait and do not redirect.
                // stdout/stderr remain inherited from this shell.
                return;
            }

            HandleBackgroundRedirection(
                process,
                outputFile,
                errorFile,
                appendOutput,
                appendError
            );

            return;
        }

        using (process)
        {
            string? stdout = null;
            string? stderr = null;

            if (outputFile != null)
            {
                stdout =
                    process.StandardOutput
                        .ReadToEnd();
            }

            if (errorFile != null)
            {
                stderr =
                    process.StandardError
                        .ReadToEnd();
            }

            process.WaitForExit();

            if (outputFile != null)
            {
                if (appendOutput)
                {
                    File.AppendAllText(
                        outputFile,
                        stdout ?? ""
                    );
                }
                else
                {
                    File.WriteAllText(
                        outputFile,
                        stdout ?? ""
                    );
                }
            }

            if (errorFile != null)
            {
                if (appendError)
                {
                    File.AppendAllText(
                        errorFile,
                        stderr ?? ""
                    );
                }
                else
                {
                    File.WriteAllText(
                        errorFile,
                        stderr ?? ""
                    );
                }
            }
        }
    }

    static void HandleBackgroundRedirection(
        Process process,
        string? outputFile,
        string? errorFile,
        bool appendOutput,
        bool appendError)
    {
        if (outputFile != null)
            PrepareOutputFile(
                outputFile,
                appendOutput
            );

        if (errorFile != null)
            PrepareErrorFile(
                errorFile,
                appendError
            );

        if (outputFile != null)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null)
                    return;

                try
                {
                    File.AppendAllText(
                        outputFile,
                        e.Data + Environment.NewLine
                    );
                }
                catch
                {
                }
            };

            process.BeginOutputReadLine();
        }

        if (errorFile != null)
        {
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                    return;

                try
                {
                    File.AppendAllText(
                        errorFile,
                        e.Data + Environment.NewLine
                    );
                }
                catch
                {
                }
            };

            process.BeginErrorReadLine();
        }
    }
}