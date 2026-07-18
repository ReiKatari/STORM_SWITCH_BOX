using System;

namespace StormSwitchBox.Views;

public class TasksStartupArgs
{
    public string Action { get; set; } = "";
    public string[] Paths { get; set; } = Array.Empty<string>();
    public string? Format { get; set; }
}
