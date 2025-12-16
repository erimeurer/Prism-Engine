using System.Reflection;
using System.Collections.Generic;

namespace MonoGameEditor.ViewModels
{
    public class ScriptPropertyViewModel : ViewModelBase
    {
        private object _targetScript;
        private PropertyInfo _propertyInfo;

        public string Name => _propertyInfo.Name;
        public System.Type PropertyType => _propertyInfo.PropertyType;
        
        public object Value
        {
            get => _propertyInfo.GetValue(_targetScript);
            set
            {
                _propertyInfo.SetValue(_targetScript, value);
                OnPropertyChanged();
            }
        }

        // Helper properties for different types
        public float FloatValue
        {
            get => Value is float f ? f : 0f;
            set => Value = value;
        }

        public int IntValue
        {
            get => Value is int i ? i : 0;
            set => Value = value;
        }

        public bool BoolValue
        {
            get => Value is bool b && b;
            set => Value = value;
        }

        public string StringValue
        {
            get => Value as string ?? "";
            set => Value = value;
        }

        public Microsoft.Xna.Framework.Vector3 Vector3Value
        {
            get => Value is Microsoft.Xna.Framework.Vector3 v ? v : Microsoft.Xna.Framework.Vector3.Zero;
            set => Value = value;
        }

        public ScriptPropertyViewModel(object targetScript, PropertyInfo propertyInfo)
        {
            _targetScript = targetScript;
            _propertyInfo = propertyInfo;
        }
    }
}
