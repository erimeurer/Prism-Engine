using System.Configuration;
using System.Data;
using System.Windows;

using MonoGameEditor.Core;
namespace MonoGameEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Initialize logger for Core
            Core.Logger.Initialize(new ViewModels.EditorLogger());
        }
    }

}
