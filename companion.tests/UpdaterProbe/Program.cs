if (args.Length > 0 && args[0] == "wait") Thread.Sleep(60000);
else File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "restarted.txt"), "restarted");
