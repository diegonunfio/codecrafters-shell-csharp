using System.Collections;
using System.ComponentModel.Design;
using CodeCrafters.Shell;

class Program {
  static private Dictionary<string, Command> commands;
  static private bool runProgram = true;

  static void Main() {
    init();
    while (runProgram) {
      Console.Write("$ ");
      String? input = Console.ReadLine();
      input = input == null ? "" : input;
      if (input == "") {
        continue;
      }

      List<string> inputList = input.Split(" ").ToList();
      Command? command = null;
      if (commands.TryGetValue(inputList[0], out command)) {
        command.Execute(inputList.Skip(1).ToList().ToArray());
        continue;
      }
      Console.Write(input + ": command not found\n");
    }
  }

  static void init() {
    commands = new Dictionary<string, Command>();
    commands.Add("exit", new Command("exit", "Exits the program.", "exit",
                                     strings => { runProgram = false; }));
    commands.Add("echo", new Command("echo", "Repeats the string after echo",
                                     "echo <>", strings => {
                                       foreach (string input in strings) {
                                         Console.Write(input + " ");
                                       }
                                       Console.Write("\n");
                                     }));
    commands.Add(
        "type",
        new Command("type", "Writes the type of the variable.", "type <>",
                    strings => {
                      if (strings.Length == 0) {
                        Console.WriteLine("Usage: " + commands["type"].Usage);
                        return;
                      }

                      string command = strings[0];
                      if (commands.ContainsKey(command)) {

                        Console.WriteLine(commands[command].Name +
                                          " is a shell builtin");
                        return;
                      }
                      var path = Environment.GetEnvironmentVariable("PATH");
                      char separator = Path.PathSeparator;
                      string[] folders = path!.Split(separator);
                      foreach (var folder in folders) {
                        var filePath = Path.Combine(folder, command);
                        if (File.Exists(filePath) &&
                            File.GetUnixFileMode(filePath).HasFlag(
                                UnixFileMode.UserExecute)) {
                          Console.WriteLine($"{command} is {filePath}");
                          return;
                        }
                      }
                      Console.Write(strings[0] + ": not found\n");
                    }));
  }
}
