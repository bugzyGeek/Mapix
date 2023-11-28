using System;
using System.Reflection;

namespace Mapix
{
    internal class MapperReflection
    {
        // Declare a property to store the properties information of the type
        internal PropertyInfo[] PropertiesInfo { get; private set; }
        // Declare a property to store the type
        internal Type ObjectType { get; private set; }

        // Define a constructor that takes a type as a parameter
        public MapperReflection(Type obj)
        {
            // Set the ObjectType property with the type
            ObjectType = obj;
            // Set the PropertiesInfo property with the properties of the type
            PropertiesInfo = ObjectType.GetProperties();
        }
    }
}
