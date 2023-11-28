using System;

namespace Mapix.DataAnnotation
{

    public partial class Mapper
    {
        // Define a custom attribute that indicates whether to ignore the mapping of a property
        [AttributeUsage(AttributeTargets.Property)]
        public class IgnoreMapAttribute : Attribute
        {
            // Define a constructor that takes no parameters
            public IgnoreMapAttribute()
            {
            }
        }

    }
}
