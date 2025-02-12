// See https://aka.ms/new-console-template for more information

using Autoscaler.Persistence.ScaleSettingsRepository;

Console.WriteLine("Creating runner");
var runner = new Runner("something", "http://forecaster", "http://kubernetes", "http://prometheus", new ScaleSettingsRepository());
Console.WriteLine("Running mainloop");
runner.MainLoop();
Console.WriteLine("Finished mainloop");
