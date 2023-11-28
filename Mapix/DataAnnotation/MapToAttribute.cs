using System;

namespace Mapix.DataAnnotation
{

    public partial class Mapper
    {
        // Define a custom attribute that specifies the name of the matching property in the target type
        [AttributeUsage(AttributeTargets.Property)]
        public class MapToAttribute : Attribute
        {
            // Declare a property to store the name of the matching property
            public string Name { get; private set; }

            // Define a constructor that takes the name of the matching property as a parameter
            public MapToAttribute(string name)
            {
                // Set the Name property with the name of the matching property
                Name = name;
            }
        }

    }
}
