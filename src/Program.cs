class Program {
  static void Main(string[] args) {

    while (true) {
      var exit = "exit";
      var echo = "echo";
      var type = "type";
      var valid = new[] { exit, echo, type };

      Console.Write("$ ");
      var Test = Console.ReadLine();
      if (valid.Any(Test.Contains)) {
        if (Test == "exit") {
          return;
        } else if (Test.StartsWith("echo")) {
          Console.WriteLine(Test[5..]);
        } else if (Test.Contains("exit")) {
          Console.WriteLine($"{Test[5..]} is a shell builtin");
        } else if (Test.Contains("echo")) {
          Console.WriteLine($"{Test[5..]} is a shell builtin");
        }

        else if (Test.EndsWith("type")) {
          Console.WriteLine($"{Test[5..]} is a shell builtin");

        } else {
          Console.WriteLine($"{Test[5..]}: not found");
        }

      } else {
        Console.WriteLine($"{Test}: command not found");
      }
    }
  }
}
