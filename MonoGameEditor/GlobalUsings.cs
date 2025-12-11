// Global using directives to resolve WPF/WinForms ambiguity
// When both UseWPF and UseWindowsForms are true, many types become ambiguous

global using UserControl = System.Windows.Controls.UserControl;
global using Application = System.Windows.Application;
global using Button = System.Windows.Controls.Button;
global using TextBox = System.Windows.Controls.TextBox;
global using Label = System.Windows.Controls.Label;
global using Panel = System.Windows.Controls.Panel;
global using Control = System.Windows.Controls.Control;
global using Cursor = System.Windows.Input.Cursor;
global using Cursors = System.Windows.Input.Cursors;
global using Color = Microsoft.Xna.Framework.Color;
