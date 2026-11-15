using System.IO;
using System.Windows;

namespace ShootProcs.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShootProcs");
            Directory.CreateDirectory(log);
            File.AppendAllText(Path.Combine(log, "errors.log"), $"{DateTimeOffset.Now:u} {args.Exception}\n");
            MessageBox.Show($"ShootProcs encountered an error:\n{args.Exception.Message}\n\nDetails: {log}",
                "ShootProcs", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
    }
}
