using System.Linq;
using System.Windows.Controls;
using AvalonDock.Layout;
using MonoGameEditor.ViewModels;

namespace MonoGameEditor.Views
{
    public partial class EditorLayoutView : UserControl
    {
        public EditorLayoutView()
        {
            InitializeComponent();
            Loaded += EditorLayoutView_Loaded;
        }

        private void EditorLayoutView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
             // Optional: Initial layout setup if needed, or rely on AvalonDock serialization/initial state
             // For MVVM, usually we want to ensure the initial items are put in the right panes.
             // But AvalonDock's "LayoutInitializer" logic is complex. 
             // For this step, we will assume standard docking or let the user arrange.
             // OR, better: We manually pre-populate the layout in XAML with ContentIds matching ViewModels.
             
             // XAML layout definition is static, but binding source is dynamic. 
             // To ensure they snap to the defined LayoutAnchorables in XAML, 
             // the LayoutAnchorables in XAML need ContentIds matching the VM ContentIds.
             
             // Let's refine the XAML to include empty LayoutAnchorables with IDs.
        }
    }
}
