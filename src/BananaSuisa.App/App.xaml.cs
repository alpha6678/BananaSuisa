using System;
using System.IO;
using System.Windows;

namespace BananaSuisa.App;

public partial class App : Application
{
    public App()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            LogCrash(e.Exception);
            MessageBox.Show($"Unhandled Exception:\n\n{e.Exception.Message}\n\nCheck crash.log for details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash(ex);
                MessageBox.Show($"AppDomain Unhandled Exception:\n\n{ex.Message}\n\nCheck crash.log for details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    private void LogCrash(Exception ex)
    {
        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), ex.ToString());
            // Tenta salvar também na raiz do projeto para ficar mais visível
            File.WriteAllText(@"C:\Users\Usuário\Desenvolvimento\projetos\pessoais\BananaSuisa\crash.log", ex.ToString());
        }
        catch { }
    }
}

