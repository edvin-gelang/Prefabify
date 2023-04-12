using UnityEditor;

namespace Gelang.Prefabify.Editor
{
    public class ModifiedProperty
    {
        public SerializedProperty Property { get; set; }
        public ObjectPair ComponentPair { get; set; }

        public ModifiedProperty(SerializedProperty property, ObjectPair componentPair)
        {
            Property = property.Copy();
            ComponentPair = componentPair;
        }

        public override string ToString()
        {
            return Property.propertyPath;
        }
    }
}